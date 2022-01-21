#pragma once

#ifdef __cplusplus
extern "C" {
#endif

int __stdcall BTRCALL(WORD wOperation, LPVOID lpPositionBlock, LPVOID lpDataBuffer, LPDWORD dwDataBufferLength, LPVOID lpKeyBuffer, BYTE bKeyLength, CHAR bKeyNumber);

#ifdef __cplusplus
}
#endif
