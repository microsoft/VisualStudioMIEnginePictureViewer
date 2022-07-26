# PictureViewer
Sample MIEngine debugger UIVisualizer for RasberryPi cameras. 

This repo contains two projects, in /src is the PictureView VSIX project and in /simpletest_raspicam is a sample CMake 
project that can be used to test the PictureViewer. These instructions assume you are already familiar with the Linux
build and debugging features of Visual Studio and have already set up your raspberryPi with a camera and 
installed installed gdb.

## PictureViewer Project
This is a VSIX project. You will need to edit the PictureView.csproj and change this line:

&lt;HintPath&gt;..\..\..\MIEngine\bin\Lab.Debug\Microsoft.DebugEngineHost.dll&lt;/HintPath&gt;

to point to your instance of Visual Studio. Something like:

&lt;HintPath&gt;my-VS-root-path\Common7\IDE\CommonExtensions\Microsoft\MDD\Debugger\Microsoft.DebugEngineHost.dll&lt;/HintPath&gt;

Open the .sln file in Visual Studio. Build and debug PictureViewer. Once running VS will open a new experimental version of
Visual Studio with your VSIX installed. When it opens then open the folder simpletest_raspicam. Build the project and 
configure for debugging according to the instructions below. 

In order to test the PictureViewer:
1. Set a breakpoint on simpletest_raspicam.cpp at the line containing "Camera.grab()".
2. Select the "DebugOnPi" debug target.
3. Hit F5. 

At this point gdb will fire up on the remote target and VS will eventually stop at the breakpoint.

4. Open the Locals window.
5. On the line containing the value for variable "Camera" you should see a small magifying glass icon in the "Value" column.
   Click on the icon.

At this point VS will open a window which will initially be blank, but will eventually contain a picture from the camera on
your Pi. Hitting the "next" button below the picture will refresh the view with another shot from the camera.

## simpletest_raspicam Project

This project builds using raspberry pi cross compiler tools. You will need to adjust the values of the environment variables
RASPIAN_ROOTFS and PATH in CMAKESettings.json for the location of your tools. You will also need to set the value of cmakeToolchain
to point to your raspberryPi toolchain file.

The project depends upon two other packages: raspicam and opencv. You will need to copy, build, and install these projects also using the
raspberryPi toolset. So you will have to make similar CMakeSettings configuration changes when building those projects as well.

The simpletest_raspicam project needs to know where to find the above two packages. Adjust the values of OpenCV_DIR and raspicam_DIR in
CMakeSettings .json to point to your package installations.

Once you have successfully built simpletest_raspicam copy the shared libraries from your package installations to some directory (e.g. ~/camera)
on your raspberryPi.

~/camera:

libopencv_calib3d.so.405     libopencv_gapi.so.405       libopencv_objdetect.so.405  libraspicam_cv.so
libopencv_core.so.405        libopencv_highgui.so.405    libopencv_photo.so.405      libraspicam.so.0.1
libopencv_dnn.so.405         libopencv_imgcodecs.so.405  libopencv_stitching.so.405  
libopencv_features2d.so.405  libopencv_imgproc.so.405    libopencv_videoio.so.405
libopencv_flann.so.405       libopencv_ml.so.405         libopencv_video.so.405

Add a Linux Launch (gdb) debugging configuration to your project. It should look something like this:

```
{
  "version": "0.2.1",
  "defaults": {},
  "configurations": [
    {
      "type": "cppgdb",
      "name": "DebugOnPi",
      "project": "CMakeLists.txt",
      "projectTarget": "simpletest_raspicam",
      "debuggerConfiguration": "gdb",
      "MIMode": "gdb",
      "args": [],
      "env": {},
      "deployDirectory": "~/camera",
      "remoteMachineName": "-1836894802;xxx.yyy.zz.ppp (username=pi, port=22, authentication=Password)",
      "preDebugCommand": "export LD_LIBRARY_PATH=~/camera"
    }
  ]
}
```