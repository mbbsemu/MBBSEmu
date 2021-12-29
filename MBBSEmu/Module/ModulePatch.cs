using System;
using System.Text;
using System.Text.Json.Serialization;

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
        public ushort Segment { get; set; }
        public ushort Offset { get; set; }
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
