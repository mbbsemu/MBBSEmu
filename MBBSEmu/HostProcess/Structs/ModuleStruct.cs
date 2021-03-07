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
        public FarPtr lonrou { get; set; }
        public FarPtr sttrou { get; set; }
        public FarPtr stsrou { get; set; }
        public FarPtr injrou { get; set; }
        public FarPtr lofrou { get; set; }
        public FarPtr huprou { get; set; }
        public FarPtr mcurou { get; set; }
        public FarPtr dlarou { get; set; }
        public FarPtr finrou { get; set; }

        public const ushort Size = 61;
        public const ushort DESCRP_SIZE = 25;

        private readonly byte[] _data = new byte[Size];

        public byte[] Data
        {
            get
            {

                Array.Copy(descrp, 0, _data, 0, descrp.Length);
                Array.Copy(lonrou.Data, 0, _data, 25, FarPtr.Size);
                Array.Copy(sttrou.Data, 0, _data, 29, FarPtr.Size);
                Array.Copy(stsrou.Data, 0, _data, 33, FarPtr.Size);
                Array.Copy(injrou.Data, 0, _data, 37, FarPtr.Size);
                Array.Copy(lofrou.Data, 0, _data, 41, FarPtr.Size);
                Array.Copy(huprou.Data, 0, _data, 45, FarPtr.Size);
                Array.Copy(mcurou.Data, 0, _data, 49, FarPtr.Size);
                Array.Copy(dlarou.Data, 0, _data, 53, FarPtr.Size);
                Array.Copy(finrou.Data, 0, _data, 57, FarPtr.Size);
                return _data;
            }
            set
            {
                var valueSpan = new ReadOnlySpan<byte>(value);
                descrp = valueSpan.Slice(0, 25).ToArray();
                lonrou.FromSpan(valueSpan.Slice(25, FarPtr.Size));
                sttrou.FromSpan(valueSpan.Slice(29, FarPtr.Size));
                stsrou.FromSpan(valueSpan.Slice(33, FarPtr.Size));
                injrou.FromSpan(valueSpan.Slice(37, FarPtr.Size));
                lofrou.FromSpan(valueSpan.Slice(41, FarPtr.Size));
                huprou.FromSpan(valueSpan.Slice(45, FarPtr.Size));
                mcurou.FromSpan(valueSpan.Slice(49, FarPtr.Size));
                dlarou.FromSpan(valueSpan.Slice(53, FarPtr.Size));
                finrou.FromSpan(valueSpan.Slice(57, FarPtr.Size));
            }
        }

        public ModuleStruct()
        {
            descrp = new byte[25];
            lonrou = new FarPtr();
            sttrou = new FarPtr();
            stsrou = new FarPtr();
            injrou = new FarPtr();
            lofrou = new FarPtr();
            huprou = new FarPtr();
            mcurou = new FarPtr();
            dlarou = new FarPtr();
            finrou = new FarPtr();
        }

        public ModuleStruct(ReadOnlySpan<byte> data) : this()
        {
            Data = data.ToArray();
        }
    }
}
