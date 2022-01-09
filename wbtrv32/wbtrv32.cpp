#include "pch.h"
#include <vcclr.h>
#include <msclr\auto_gcroot.h>
#include "wbtrv32.h"

using namespace MBBSEmu::DOS::Interrupts;

msclr::auto_gcroot<MBBSEmu::DOS::Interrupts::Int7Bh^> _int7b;

private class ProtectedMode32Bit : MBBSEmu::Memory::IMemoryCore
{

};

int BTRV_API BTRCALL(WORD wOperation, LPVOID lpPositionBlock, LPVOID lpDataBuffer, DWORD dwDataBufferLength, LPVOID lpKeyBuffer, BYTE bKeyLength, BYTE bKeyNumber) {
    if (!_int7b) {
        _int7b = gcnew MBBSEmu::DOS::Interrupts::Int7Bh(nullptr, nullptr);
    }

    BtrieveCommand b = BtrieveCommand();
    b.operation = static_cast<MBBSEmu::Btrieve::Enums::EnumBtrieveOperationCodes>(wOperation);
    b.data_buffer_segment = reinterpret_cast<DWORD>(lpDataBuffer) >> 16;
    b.data_buffer_offset = reinterpret_cast<DWORD>(lpDataBuffer) & 0xFFFF;

    b.position_block_segment = reinterpret_cast<DWORD>(lpPositionBlock) >> 16;
    b.position_block_offset = reinterpret_cast<DWORD>(lpPositionBlock) & 0xFFFF;

    b.data_buffer_length = (WORD)dwDataBufferLength;

    b.key_buffer_segment = reinterpret_cast<DWORD>(lpKeyBuffer) >> 16;
    b.key_buffer_offset = reinterpret_cast<DWORD>(lpKeyBuffer) & 0xFFFF;

    b.key_number = bKeyNumber;
    b.key_buffer_length = bKeyLength;

    _int7b->Handle(b);
    return 0;
}
