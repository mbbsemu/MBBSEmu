using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading.Tasks;

namespace MBBSEmu.Btrieve {
  public partial class Wbtrv32 {
    static Wbtrv32() {
      NativeLibrary.SetDllImportResolver(typeof(Wbtrv32).Assembly, ResolveWbtrv32);
    }

    // wbtrv32.dll isn't installed system-wide -- it's built out-of-tree by the wbtrv32
    // native project. Point WBTRV32_PATH at that build output directory (the folder
    // containing wbtrv32.dll) to let MBBSEmu find it. If that's not set (or the DLL
    // isn't there), fall back to the standard OS search -- e.g. PATH on Windows --
    // in case it's been installed/registered some other way.
    private static IntPtr ResolveWbtrv32(string libraryName, Assembly assembly, DllImportSearchPath? searchPath) {
      if (!string.Equals(libraryName, "wbtrv32.dll", StringComparison.OrdinalIgnoreCase))
        return IntPtr.Zero;

      var wbtrv32Directory = Environment.GetEnvironmentVariable("WBTRV32_PATH");
      if (!string.IsNullOrEmpty(wbtrv32Directory)) {
        var wbtrv32Path = Path.Combine(wbtrv32Directory, "wbtrv32.dll");
        if (NativeLibrary.TryLoad(wbtrv32Path, out var handle))
          return handle;
      }

      return NativeLibrary.TryLoad("wbtrv32.dll", out var fallbackHandle) ? fallbackHandle : IntPtr.Zero;
    }

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
