using MBBSEmu.Memory;
using System;

namespace MBBSEmu.HostProcess.Structs
{
    /// <summary>
    ///     Struct used to define entry points for a MajorBBS/Worldgroup Module
    /// </summary>
    public class ModuleStruct
    {
        public byte[] descrp;
        public IntPtr16 lonrou { get; set; }
        public IntPtr16 sttrou { get; set; }
        public IntPtr16 stsrou { get; set; }
        public IntPtr16 injrou { get; set; }
        public IntPtr16 lofrou { get; set; }
        public IntPtr16 huprou { get; set; }
        public IntPtr16 mcurou { get; set; }
        public IntPtr16 dlarou { get; set; }
        public IntPtr16 finrou { get; set; }

        public const ushort Size = 61;

        private readonly byte[] _data = new byte[Size];

        public byte[] Data
        {
            get
            {
                
                Array.Copy(descrp, 0, _data, 0, descrp.Length);
                Array.Copy(lonrou.Data, 0, _data, 25, IntPtr16.Size);
                Array.Copy(sttrou.Data, 0, _data, 29, IntPtr16.Size);
                Array.Copy(stsrou.Data, 0, _data, 33, IntPtr16.Size);
                Array.Copy(injrou.Data, 0, _data, 37, IntPtr16.Size);
                Array.Copy(lofrou.Data, 0, _data, 41, IntPtr16.Size);
                Array.Copy(huprou.Data, 0, _data, 45, IntPtr16.Size);
                Array.Copy(mcurou.Data, 0, _data, 49, IntPtr16.Size);
                Array.Copy(dlarou.Data, 0, _data, 53, IntPtr16.Size);
                Array.Copy(finrou.Data, 0, _data, 57, IntPtr16.Size);
                return _data;
            }
            set
            {
                var valueSpan = new ReadOnlySpan<byte>(value);
                descrp = valueSpan.Slice(0, 25).ToArray();
                lonrou.FromSpan(valueSpan.Slice(25, IntPtr16.Size));
                sttrou.FromSpan(valueSpan.Slice(29, IntPtr16.Size));
                stsrou.FromSpan(valueSpan.Slice(33, IntPtr16.Size));
                injrou.FromSpan(valueSpan.Slice(37, IntPtr16.Size));
                lofrou.FromSpan(valueSpan.Slice(41, IntPtr16.Size));
                huprou.FromSpan(valueSpan.Slice(45, IntPtr16.Size));
                mcurou.FromSpan(valueSpan.Slice(49, IntPtr16.Size));
                dlarou.FromSpan(valueSpan.Slice(53, IntPtr16.Size));
                finrou.FromSpan(valueSpan.Slice(57, IntPtr16.Size));
            }
        }

        public ModuleStruct()
        {
            descrp = new byte[25];
            lonrou = new IntPtr16();
            sttrou = new IntPtr16();
            stsrou = new IntPtr16();
            injrou = new IntPtr16();
            lofrou = new IntPtr16();
            huprou = new IntPtr16();
            mcurou = new IntPtr16();
            dlarou = new IntPtr16();
            finrou = new IntPtr16();
        }

        public ModuleStruct(ReadOnlySpan<byte> data) : this()
        {
            Data = data.ToArray();
        }
    }
}
