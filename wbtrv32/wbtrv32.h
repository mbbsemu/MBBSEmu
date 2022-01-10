#pragma once

#ifdef BTRV_EXPORTS
  #define BTRV_API __declspec(dllexport) __stdcall
#else
  #define BTRV_API __declspec(dllimport) __stdcall
#endif

int BTRV_API BTRCALL(WORD wOperation, LPVOID lpPositionBlock, LPVOID lpDataBuffer, DWORD dwDataBufferLength, LPVOID lpKeyBuffer, WORD bKeyLength, SHORT bKeyNumber);
