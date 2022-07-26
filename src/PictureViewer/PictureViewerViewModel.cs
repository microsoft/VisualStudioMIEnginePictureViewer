using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;
using System.Windows.Input;
using System.Windows.Media.Imaging;
using EnvDTE;
using Microsoft.VisualStudio;
using Microsoft.VisualStudio.Debugger.Evaluation;
using Microsoft.VisualStudio.Debugger.Interop;
using Microsoft.VisualStudio.Debugger.Interop.MI;
using Microsoft.VisualStudio.PlatformUI;
using Microsoft.VisualStudio.Shell;
using Microsoft.VisualStudio.Threading;

namespace PictureViewer
{
    internal class PictureViewerViewModel : IDisposable, INotifyPropertyChanged
    {
        private IDebugProperty3 property;
        private IDebugExpressionContext2 context;
        private string fullName;
        private string propertyType;
        private BitmapImage image;
        private CAMERA_TYPE camera_type;

        private string call(string method)
        {
            return $"{this.fullName}{(this.propertyType.EndsWith("*") ? "->" : ".")}{method}";
        }

        private static string RefFromAddr(string address, string type)
        {
            return $"*({type}*){address}";
        }

        private string MakeHeapObject(string type, string constructor, string cparams = "")
        {
            var address = this.EvalExpression($"malloc(sizeof({type}))");
            if (!address.bstrValue.StartsWith("0x"))
            {
                throw new ApplicationException(address.bstrValue);
            }

            var init = this.EvalExpression($"(({type} *){address.bstrValue})->{constructor}({cparams})");
            return address.bstrValue;
        }

        private void FreeHeapObject(string address, string type = null, string destructor = null)
        {
            if (address == null)
                return;

            if (type != null && destructor != null)
            {
                _ = this.EvalExpression($"(({type} *){address})->{destructor}()");
            }

            _ = this.EvalExpression($"free({address})");
        }

        public enum CAMERA_TYPE
        {
            RASPICAM = 1,
            RASPICAM_CV = 2,
            RASPICAM_STILL = 3,
            RASPICAM_STILL_CV = 4,
        };

        public int Width { get; set; }
        public int Height { get; set; }

        public BitmapImage Image
        {
            get { return image; }
            set
            {
                image = value;
                RaisePropertyChanged(nameof(Image));
            }

        }
        public PictureViewerViewModel()
        {
        }

        public event PropertyChangedEventHandler PropertyChanged;

        public void VisualizeInstance(IDebugProperty3 result, CAMERA_TYPE type)
        {
            this.property = result;
            var miProp = result as IDebugMIEngineProperty;

            miProp.GetExpressionContext(out this.context);
            DEBUG_PROPERTY_INFO[] nameProp = new DEBUG_PROPERTY_INFO[1];
            result.GetPropertyInfo(enum_DEBUGPROP_INFO_FLAGS.DEBUGPROP_INFO_FULLNAME| enum_DEBUGPROP_INFO_FLAGS.DEBUGPROP_INFO_TYPE, 10, 0, null, 0, nameProp);
            this.fullName = nameProp[0].bstrFullName;
            this.propertyType = nameProp[0].bstrType;
            this.camera_type = type;

            Task.Run(() => this.LoadImage()).FileAndForget("LoadImage");

            var window = new VisualizerWindow()
            {
                DataContext = this
            };

            window.ShowDialog();
        }

        private DEBUG_PROPERTY_INFO EvalExpression(string expr)
        {
            return EvalExpression(expr, out _);
        }

        private DEBUG_PROPERTY_INFO EvalExpression(string expr, out IDebugMemoryContext2 ppMemory)
        {
            if (context.ParseText(expr, enum_PARSEFLAGS.PARSE_EXPRESSION, 10, out IDebugExpression2 ppExpr, out string error, out uint errorCode) != VSConstants.S_OK)
            {
                throw new ApplicationException($"Failed to parse expression '{expr}'.");
            }

            if (ppExpr.EvaluateSync(0, 0, null, out IDebugProperty2 prop) != VSConstants.S_OK)
            {
                throw new ApplicationException($"Failed to evaluate expression '{expr}'.");
            }

            DEBUG_PROPERTY_INFO[] value = new DEBUG_PROPERTY_INFO[1]; ;
            if (prop.GetPropertyInfo(enum_DEBUGPROP_INFO_FLAGS.DEBUGPROP_INFO_VALUE, 10, 0, null, 0, value) != VSConstants.S_OK)
            {
                throw new ApplicationException($"Failed to get expression value for '{expr}'.");
            }

            // get memory context if available
            if (prop.GetMemoryContext(out ppMemory) != VSConstants.S_OK)
            {
                ppMemory = null;
            }

            return value[0];
        }

        private ICommand _clickCommand;
        public ICommand ClickCommand
        {
            get
            {
                return _clickCommand ?? (_clickCommand = new CommandHandler(() => LoadNextCommand(), () => CanExecute));
            }
        }
        public bool CanExecute
        {
            get
            {
                // check if executing is allowed, i.e., validate, check if a process is running, etc. 
                return true;
            }
        }

        public void LoadNextCommand()
        {
            this.LoadImage();
        }

        private void LoadImage()
        {
            switch (this.camera_type)
            {
                case CAMERA_TYPE.RASPICAM:
                    this.LoadImageCam();
                    break;
                case CAMERA_TYPE.RASPICAM_CV:
                    this.LoadImageCamCV();
                    break;
                case CAMERA_TYPE.RASPICAM_STILL:
                    this.LoadImageCamStill();
                    break;
                case CAMERA_TYPE.RASPICAM_STILL_CV:
                    this.LoadImageCamStillCV();
                    break;
            }
        }

        private void ReadJpgAtAddress(IDebugMemoryContext2 memory, uint n)
        {
            if (this.property.GetMemoryBytes(out IDebugMemoryBytes2 bytes) != VSConstants.S_OK)
            {
                throw new ApplicationException($"Memory bytes not available.");
            }

            var theImage = new byte[n];
            uint unreadable = 0;
            if (bytes.ReadAt(memory, n, theImage, out uint pread, ref unreadable) != VSConstants.S_OK)
            {
                throw new ApplicationException("Cannot read image memory.");
            }

            if (n == 0)
            {
                Image = null;
            }

            using (var stream = new MemoryStream(theImage))
            {
                var bitmap = new BitmapImage();
                bitmap.BeginInit();
                bitmap.CacheOption = BitmapCacheOption.OnLoad;
                bitmap.StreamSource = stream;
                bitmap.EndInit();
                bitmap.Freeze();

                this.Image = bitmap;
            }
        }

        private void LoadImageCam()
        {
            var format = this.EvalExpression(this.call("getFormat()"));
            var size = this.EvalExpression(this.call("getImageTypeSize({format.bstrValue})"));
            uint n = uint.Parse(size.bstrValue);
            if (n == 0)
            {
                throw new ApplicationException("Bad image size.");
            }

            var imageAvailable = this.EvalExpression(this.call("grab()"));
            if (imageAvailable.bstrValue.Equals("false", StringComparison.OrdinalIgnoreCase))
            {
                Image = null;
                return;
            }

            var data = this.EvalExpression($"malloc({size.bstrValue})", out IDebugMemoryContext2 memory);
            _ = this.EvalExpression(this.call("retrieve({data.bstrValue})"));
            if (this.property.GetMemoryBytes(out IDebugMemoryBytes2 bytes) != VSConstants.S_OK)
            {
                throw new ApplicationException($"Memory bytes not available.");
            }

            var theImage = new byte[n];
            uint unreadable = 0;
            if (bytes.ReadAt(memory, n, theImage, out uint pread, ref unreadable) != VSConstants.S_OK)
            {
                throw new ApplicationException("Cannot read image memory.");
            }

            var width = this.EvalExpression(this.call("getWidth()"));
            var height = this.EvalExpression(this.call("getHeight()"));

            Width = int.Parse(width.bstrValue);
            Height = int.Parse(height.bstrValue);
            //Image = theImage; -- need to convert this bitmap format (ppm) into something that WPF can deal with.
            _ = this.EvalExpression($"free({data.bstrValue})");
        }

        private void LoadImageCamStill()
        {
            // not implemented
        }

        private void LoadImageCamCV()
        {
            // not implemented
        }

        private void LoadImageCamStillCV()
        {
            /* Perform these steps using the debugger to evaluate the necessary expressions
             *    vector<uchar> jpgImage;
             *    vector<int> jpgParams;
             *    cv::InputArray ia = cv::_InputArray(image);
             *    string jpg = ".jpg";
             *    cv::imencode(jpg, ia, jpgImage, jpgParams);
             *    start = &jpgImage.at(0);
             *    int size = jpgImage.size();
             */
            string vectorType = "std::vector<unsigned char, std::allocator<unsigned char> >";
            string constructor = "vector";
            string vectorTypeInt = "std::vector<int, std::allocator<int> >";
            string mat = null;
            string allocator = null;
            string jpg = null;
            string jpgImage = null;
            string intParams = null;
            string inputArray = null;

            try
            {
                mat = this.MakeHeapObject("cv::Mat", "Mat");

                var imageAvailable = this.EvalExpression(this.call("grab()"));
                if (imageAvailable.bstrValue.Equals("false", StringComparison.OrdinalIgnoreCase))
                {
                    Image = null;
                    return;
                }

                _ = this.EvalExpression(this.call($"retrieve({RefFromAddr(mat, "cv::Mat")})"));

                allocator = this.MakeHeapObject("std::allocator<char>", "allocator");
                jpg = this.MakeHeapObject("std::__cxx11::basic_string<char, std::char_traits<char>, std::allocator<char> >", "basic_string", $"\".jpg\", {RefFromAddr(allocator, "std::allocator<char>")}");
                jpgImage = this.MakeHeapObject(vectorType, constructor);
                intParams = this.MakeHeapObject(vectorTypeInt, constructor);
                inputArray = this.MakeHeapObject("cv::_InputArray", "init", $"0x1010000, (void*){mat}");

                var encodeJpg = this.EvalExpression($"cv::imencode({RefFromAddr(jpg, "std::__cxx11::basic_string<char, std::char_traits<char>, std::allocator<char> >")}, {RefFromAddr(inputArray, "cv::_InputArray")}, {RefFromAddr(jpgImage, vectorType)}, {RefFromAddr(intParams, vectorTypeInt)})");
                var start = this.EvalExpression($"&(({vectorType}*){jpgImage})->at(0)", out IDebugMemoryContext2 memory);
                var size = this.EvalExpression($"(({vectorType}*){jpgImage})->size()");
                uint n = uint.Parse(size.bstrValue);
                if (n == 0)
                {
                    throw new ApplicationException("Bad image size.");
                }

                ReadJpgAtAddress(memory, n);
            }
            catch (Exception)
            {
                FreeHeapObject(inputArray, "cv::_InputArray", "~_InputArray");
                FreeHeapObject(intParams, vectorTypeInt, "~" + constructor);
                FreeHeapObject(jpgImage, vectorType, "~" + constructor);
                FreeHeapObject(jpg, "std::__cxx11::basic_string<char, std::char_traits<char>, std::allocator<char> >", "~basic_string");
                FreeHeapObject(allocator, "std::allocator<char>", "~allocator");
                FreeHeapObject(mat, "cv::Mat", "~Mat");
                throw;
            }
        }

        private void RaisePropertyChanged([CallerMemberName] String name = null)
        {
            var h = this.PropertyChanged;
            if (h != null)
            {
                h(this, new PropertyChangedEventArgs(name));
            }
        }

        public void Dispose()
        {
        }
    }
}
