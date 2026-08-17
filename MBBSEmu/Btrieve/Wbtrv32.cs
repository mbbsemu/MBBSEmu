using System;
using System.IO;
using System.Reflection;
using System.Runtime.InteropServices;

namespace MBBSEmu.Btrieve
{
    public partial class Wbtrv32
    {
        static Wbtrv32()
        {
            NativeLibrary.SetDllImportResolver(typeof(Wbtrv32).Assembly, ResolveWbtrv32);
        }

        // wbtrv32.dll isn't installed system-wide -- it's built out-of-tree by the wbtrv32
        // native project. Point WBTRV32_PATH at that build output directory (the folder
        // containing wbtrv32.dll) to let MBBSEmu find it. If that's not set (or the DLL
        // isn't there), fall back to the standard OS search -- e.g. PATH on Windows --
        // in case it's been installed/registered some other way.
        private static IntPtr ResolveWbtrv32(string libraryName, Assembly assembly, DllImportSearchPath? searchPath)
        {
            var nativeLibraryName =
                    RuntimeInformation.IsOSPlatform(OSPlatform.Windows) ? "wbtrv32.dll" :
                    RuntimeInformation.IsOSPlatform(OSPlatform.OSX) ? "wbtrv32.dylib" :
                    "wbtrv32.so";

            if (!string.Equals(libraryName, "wbtrv32.dll", StringComparison.OrdinalIgnoreCase))
                return IntPtr.Zero;

            nint handle;
            var wbtrv32Directory = Environment.GetEnvironmentVariable("WBTRV32_PATH");
            if (!string.IsNullOrEmpty(wbtrv32Directory))
            {
                var wbtrv32Path = Path.Combine(wbtrv32Directory, "wbtrv32.dll");
                if (NativeLibrary.TryLoad(wbtrv32Path, out handle))
                    return handle;
            }

            return NativeLibrary.TryLoad(nativeLibraryName, out handle) ? handle : IntPtr.Zero;
        }

        // lpDataBuffer/lpKeyBuffer are byte[] and lpdwDataBufferLength is ref int rather than
        // raw nint -- the source-generated marshalling pins these arguments directly against
        // the caller's managed arrays/locals for the duration of the call instead of copying
        // them into separately allocated unmanaged memory, so wbtrv32.dll reads and writes
        // land straight back in the caller's own buffers with no extra alloc/copy/free.
        [LibraryImport("wbtrv32.dll", EntryPoint = "BTRCALL")]
        private static partial int BTRCALL(ushort wOperation, nint lpPositionBlock, byte[] lpDataBuffer,
                                           ref int lpdwDataBufferLength, byte[] lpKeyBuffer, byte bKeyLength,
                                           byte sbKeyNumber);

        public static int managedBtrcall(ushort operation, IntPtr unmanagedPosBlock, byte[] dataBuffer,
                                         ref int dwDataBufferLength, byte[] keyBuffer,
                                         byte sbKeyNumber)
        {
            var keyBufferLength = keyBuffer != null ? (byte)Math.Min(255, keyBuffer.Length) : (byte)0;

            return BTRCALL(operation, unmanagedPosBlock, dataBuffer, ref dwDataBufferLength, keyBuffer,
                           keyBufferLength, sbKeyNumber);
        }
    }
}
