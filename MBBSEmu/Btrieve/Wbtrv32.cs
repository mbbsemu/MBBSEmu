using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading.Tasks;

namespace MBBSEmu.Btrieve {
  public partial class Wbtrv32 {
    [LibraryImport("wbtrv32.dll", EntryPoint = "BTRCALL")]
    private static partial int BTRCALL(ushort wOperation, nint lpPositionBlock, nint lpDataBuffer,
                                       nint lpdwDataBufferLength, nint lpKeyBuffer, byte bKeyLength,
                                       byte sbKeyNumber);

    public static int managedBtrcall(ushort operation, IntPtr unmanagedPosBlock, byte[] dataBuffer,
                                     ref int dwDataBufferLength, byte[] keyBuffer,
                                     byte sbKeyNumber) {
      IntPtr unmanagedDataBuffer = 0;
      IntPtr unmanagedDataBufferLength = Marshal.AllocHGlobal(sizeof(int));
      IntPtr unmanagedKeyBuffer = 0;
      byte keyBufferLength = 0;

      try {
        if (dataBuffer != null && dataBuffer.Length > 0) {
          unmanagedDataBuffer = Marshal.AllocHGlobal(dataBuffer.Length);
          Marshal.Copy(dataBuffer, 0, unmanagedDataBuffer, dataBuffer.Length);
        }

        int[] dataBufferLengthArray = new int[] { dwDataBufferLength };
        Marshal.Copy(dataBufferLengthArray, 0, unmanagedDataBufferLength, 1);

        if (keyBuffer != null && keyBuffer.Length > 0) {
          keyBufferLength = (byte)Math.Min(255, keyBuffer.Length);
          unmanagedKeyBuffer = Marshal.AllocHGlobal(keyBuffer.Length);
          Marshal.Copy(keyBuffer.ToArray(), 0, unmanagedKeyBuffer, keyBufferLength);
        }

        int response =
            BTRCALL(operation, unmanagedPosBlock, unmanagedDataBuffer, unmanagedDataBufferLength,
                    unmanagedKeyBuffer, keyBufferLength, sbKeyNumber);

        Marshal.Copy(unmanagedDataBufferLength, dataBufferLengthArray, 0, 1);
        dwDataBufferLength = dataBufferLengthArray[0];

        // did we request data, if so return it
        if (dataBuffer != null && dwDataBufferLength > 0) {
          Marshal.Copy(unmanagedDataBuffer, dataBuffer, 0, dwDataBufferLength);
        }

        if (keyBuffer != null && keyBufferLength > 0) {
          Marshal.Copy(unmanagedKeyBuffer, keyBuffer, 0, keyBufferLength);
        }

        return response;
      } finally {
        if (unmanagedDataBuffer != 0) {
          Marshal.FreeHGlobal(unmanagedDataBuffer);
        }
        Marshal.FreeHGlobal(unmanagedDataBufferLength);
        if (unmanagedKeyBuffer != 0) {
          Marshal.FreeHGlobal(unmanagedKeyBuffer);
        }
      }
    }
  }
}
