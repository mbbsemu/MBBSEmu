#include "pch.h"
#include <vcclr.h>
#include <msclr\auto_gcroot.h>
#include "wbtrv32.h"

using namespace MBBSEmu::Btrieve;
using namespace MBBSEmu::DOS::Interrupts;
using namespace MBBSEmu::IO;
using namespace MBBSEmu::Memory;
using namespace System;

msclr::auto_gcroot<Int7Bh^> _int7b;

#define ushort unsigned short
#define uint unsigned int

public ref class ProtectedMode32Bit : public ArrayBasedMemoryCore
{
public:
    ProtectedMode32Bit() {}
    ~ProtectedMode32Bit() {}

    virtual void __clrcall FillArray(ushort segment, ushort offset, int count, byte value) sealed override { memset(GetAddress(segment, offset), value, count); }
        
    virtual byte __clrcall GetByte(ushort segment, ushort offset) sealed override { return *reinterpret_cast<LPBYTE>(GetAddress(segment, offset)); }
    virtual uint __clrcall GetDWord(ushort segment, ushort offset) sealed override { return *reinterpret_cast<LPDWORD>(GetAddress(segment, offset)); }
    virtual ushort __clrcall GetWord(ushort segment, ushort offset) sealed override { return *reinterpret_cast<LPWORD>(GetAddress(segment, offset)); }
    virtual void __clrcall SetByte(ushort segment, ushort offset, byte value) sealed override { *reinterpret_cast<LPBYTE>(GetAddress(segment, offset)) = value; }
    virtual void __clrcall SetDWord(ushort segment, ushort offset, uint value) sealed override { *reinterpret_cast<LPDWORD>(GetAddress(segment, offset)) = value; }
    virtual void __clrcall SetWord(ushort segment, ushort offset, ushort value) sealed override { *reinterpret_cast<LPWORD>(GetAddress(segment, offset)) = value; }

    virtual array<byte>^ __clrcall GetByteArray(ushort segment, ushort offset, ushort count) sealed override {
        return ToManagedArray(GetAddress(segment, offset), count);
    }

    virtual void __clrcall SetByteArray(ushort segment, ushort offset, array<byte>^ _array) sealed override {
        LPBYTE dest = reinterpret_cast<LPBYTE>(GetAddress(segment, offset));
        for (int i = 0; i < _array->Length; ++i) {
            *(dest++) = _array[i];
        }
    }

    virtual array<byte>^ __clrcall GetByteString(ushort segment, ushort offset, bool stripNull) sealed override {
        LPSTR str = reinterpret_cast<LPSTR>(GetAddress(segment, offset));
        int len = strlen(str);
        if (!stripNull) {
            ++len;
        }
        return ToManagedArray(str, len);
    }
private:
    static LPVOID GetAddress(ushort segment, ushort offset) { return reinterpret_cast<LPVOID>(segment << 16 | offset); }

    static array<byte>^ ToManagedArray(LPVOID buffer, uint count) {
        LPBYTE src = reinterpret_cast<LPBYTE>(buffer);
        array<byte>^ ret = gcnew array<byte>(count);
        for (uint i = 0; i < count; ++i) {
            ret[i] = *(src++);
        }
        return ret;
    }
};

int BTRV_API BTRCALL(WORD wOperation, LPVOID lpPositionBlock, LPVOID lpDataBuffer, DWORD dwDataBufferLength, LPVOID lpKeyBuffer, WORD bKeyLength, SHORT bKeyNumber) {
    if (!_int7b) {
        BtrieveFileProcessor::InitSqlite();
        _int7b = gcnew MBBSEmu::DOS::Interrupts::Int7Bh(FileUtility::CreateForTest(), gcnew ProtectedMode32Bit());
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

    b.key_number = static_cast<char>(bKeyNumber);
    b.key_buffer_length = static_cast<unsigned char>(bKeyLength);

    return static_cast<int>(_int7b->Handle(b));
}
