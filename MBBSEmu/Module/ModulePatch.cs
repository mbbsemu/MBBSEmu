using MBBSEmu.Memory;
using System;
using System.Collections.Generic;
using System.Text;

namespace MBBSEmu.Module
{

    public class ModulePatch
    {
        public enum EnumModulePatchType
        {
            Text,
            Hex
        }

        public string Name { get; set; }
        public string Description { get; set; }
        public List<FarPtr> Addresses { get; set; }
        public FarPtr Address { get; set; }
        public uint AbsoluteOffset { get; set; }
        public EnumModulePatchType PatchType { get; set; }
        public string Patch { get; set; }

        /// <summary>
        ///     Returns bytes for the patch depending on the Patch Type
        /// </summary>
        /// <returns></returns>
        public ReadOnlySpan<byte> GetBytes()
        {
            switch (PatchType)
            {
                case EnumModulePatchType.Text:
                    return Encoding.ASCII.GetBytes(Patch);
                case EnumModulePatchType.Hex:
                {
                    if (Patch.Length % 2 != 0)
                        throw new ArgumentException(
                            $"Patch {Name} has an invalid number of bytes for a HEX patch. Must be an even number of characters.");

                    return Convert.FromHexString(Patch);
                }
                default:
                    throw new ArgumentOutOfRangeException();
            }
        }
    }
}
