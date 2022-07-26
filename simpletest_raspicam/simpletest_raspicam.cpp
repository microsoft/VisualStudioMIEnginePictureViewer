
/**
*/
#include <ctime>
#include <fstream>
#include <iostream>
#include <unistd.h>
#include <raspicam/raspicam.h>
#include <raspicam/raspicam_cv.h>
#include <raspicam/raspicam_still_cv.h>
using namespace std;

int main(int argc, char** argv)
{
    raspicam::RaspiCam_Still_Cv Camera;
    cv::Mat image;

    Camera.open();
    Camera.grab();
    Camera.retrieve(image);

    cv::imwrite("raspicam_cv_image.jpg", image);

    printf("Image saved at raspicam_cv_image.jpg\n");
    return 0;
}

