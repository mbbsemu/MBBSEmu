// wbtrv32_test.cpp : This file contains the 'main' function. Program execution begins and ends there.
//

#include <iostream>
#include <Windows.h>
#include "wbtrv32.h"

int main()
{
    unsigned char positionBlock[256];
    const char *fileName = "C:\\Users\\tcj\\WCCUSER2.DAT";

    // open is 0
    BTRCALL(0, positionBlock, NULL, 0, const_cast<char*>(fileName), static_cast<BYTE>(strlen(fileName) + 1), 0);
    std::cout << "Hello World!\n";
}

