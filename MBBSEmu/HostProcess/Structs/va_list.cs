using System;
using System.IO;
using MBBSEmu.Memory;

namespace MBBSEmu.HostProcess.Structs
{
    /*  typedef struct {
                unsigned int gp_offset;
                unsigned int fp_offset;
                void *overflow_arg_Area;
                void *reg_save_area;
            } va_list[1];
        */

    /// <summary>
    ///     Class to represent the va_list Struct
    /// </summary>
    public class va_list
    {
        public ushort gp_offset;
        public ushort fp_offset;
        public IntPtr16 overflow_arg_area;
        public IntPtr16 reg_save_area;

        public va_list(ReadOnlySpan<byte> vaListData)
        {
            overflow_arg_area = new IntPtr16();
            reg_save_area = new IntPtr16();
            FromSpan(vaListData);
        }

        public void FromSpan(ReadOnlySpan<byte> vaListData)
        {
            gp_offset = BitConverter.ToUInt16(vaListData.Slice(0, 2));
            fp_offset = BitConverter.ToUInt16(vaListData.Slice(2, 2));
            overflow_arg_area.FromSpan(vaListData.Slice(4,4));
            reg_save_area.FromSpan(vaListData.Slice(8,4));
        }

        public ReadOnlySpan<byte> ToSpan()
        {
            using var output = new MemoryStream();
            output.Write(BitConverter.GetBytes(gp_offset));
            output.Write(BitConverter.GetBytes(fp_offset));
            output.Write(overflow_arg_area.ToSpan());
            output.Write(reg_save_area.ToSpan());
            return output.ToArray();
        }
    }

   
}
