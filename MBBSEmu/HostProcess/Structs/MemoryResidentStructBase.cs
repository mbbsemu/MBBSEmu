using System;
using System.IO;
using MBBSEmu.Memory;

namespace MBBSEmu.HostProcess.Structs
{
    /// <summary>
    ///     Abstract Base Class for Memory Resident Structs that are stored in the MemoryCore
    ///
    ///     This class is used to provide a common interface for accessing memory resident structs
    /// </summary>
    /// <param name="structName"></param>
    /// <param name="size"></param>
    public abstract class MemoryResidentStructBase(string structName, ushort size)
    {
        /// <summary>
        ///     Gets a reference to the system memory
        /// </summary>
        private readonly IMemoryCore MemoryCore = ProtectedModeMemoryCore.GetInstance(null);

        /// <summary>
        ///     Name of the Struct to be used in the MemoryCore
        /// </summary>
        private string StructName { get; } = structName;

        /// <summary>
        ///     Channel Number for this User
        /// 
        ///     Used to calculate the offset of the users data in memory. We set -1 to denote that it hasn't been set yet
        ///
        ///     If there is only one copy of this struct in memory, ChannelNumber should be set to 0
        /// </summary>
        public short ChannelNumber { get; set; } = -1;

        /// <summary>
        ///     Size of the Struct in Bytes
        /// </summary>
        private ushort Size { get; } = size;

        /// <summary>
        ///     Essentially a pointer to the data in memory for the specified struct
        /// </summary>
        public Span<byte> Data
        {
            get
            {
                if (ChannelNumber == -1)
                    throw new InvalidDataException("Channel Number must be set before accessing Data");

                //Get the pointer to the specified Struct by Variable Name of the same name as the Struct
                var userPointer = MemoryCore.GetVariablePointer(StructName);

                //Calculate any channel offset if this struct exists for each channel
                userPointer.Offset += (ushort)(Size * ChannelNumber);

                return MemoryCore.GetArray(userPointer, Size);
            }
            set => value.CopyTo(Data);
        }
    }
}
