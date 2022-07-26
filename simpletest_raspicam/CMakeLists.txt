#####################################
cmake_minimum_required (VERSION 3.0) 
project (raspicam_test)
find_package(raspicam REQUIRED)
find_package(OpenCV REQUIRED)
add_executable (simpletest_raspicam simpletest_raspicam.cpp)  
target_link_libraries (simpletest_raspicam ${OpenCV_LIBS} ${raspicam_LIBS} ${raspicam_CV_LIBS} )
#####################################

