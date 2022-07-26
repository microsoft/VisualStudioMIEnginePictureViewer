using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace PictureViewer
{
    using System;
    using System.Diagnostics;
    using System.Reflection;
    using System.Reflection.Emit;

    using Microsoft.VisualStudio;
    using Microsoft.VisualStudio.Debugger.Evaluation;
    using Microsoft.VisualStudio.Debugger.Interop;

    /// <summary>
    /// Picture Viewer service implementation
    /// </summary>
    internal class PictureViewerService : IPictureViewerService, IVsCppDebugUIVisualizer
    {
        /// <summary>
        /// Create a new <see cref="PictureViewerService"> instance.
        /// </summary>
        public PictureViewerService()
        {
        }

        /// <inheritdoc />
        int IVsCppDebugUIVisualizer.DisplayValue(uint ownerHwnd, uint visualizerId, IDebugProperty3 debugProperty)
        {
            try
            {

                using (var viewModel = new PictureViewerViewModel())
                {
                    viewModel.VisualizeInstance(debugProperty, (PictureViewerViewModel.CAMERA_TYPE)visualizerId);
                }
            }
            catch (Exception e)
            {
                Debug.Fail("Visualization failed: " + e.Message);
                return e.HResult;
            }

            return VSConstants.S_OK;
        }
    }
}
