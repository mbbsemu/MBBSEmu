using System;
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
        ///     Location of the Structs Underlying Data
        /// </summary>
        enum DataSource
        {
            /// <summary>
            ///     Underlying Data stored locally within the class
            /// </summary>
            Local,

            /// <summary>
            ///     Underlying Data stored in the MemoryCore at the appropriate location based on the ChannelNumber
            /// </summary>
            Memory
        }

        /// <summary>
        ///     Current Source of the underlying data
        /// </summary>
        private DataSource _dataSource = DataSource.Local;

        /// <summary>
        ///     Gets a reference to the system memory
        /// </summary>
        private readonly IMemoryCore _memoryCore = ProtectedModeMemoryCore.GetInstance(null);

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
        public short ChannelNumber
        {
            get => _channelNumber;
            set
            {
                //Going from Local to Memory
                if (_channelNumber == -1 && value > -1)
                {
                    //Get the pointer to the specified Struct by Variable Name of the same name as the Struct
                    _structPointer = _memoryCore.GetVariablePointer(StructName);

                    //Calculate any channel offset if this struct exists for each channel
                    _structPointer.Offset += (ushort)(Size * value);

                    //Copy Local Value to Memory
                    _memoryCore.SetArray(_structPointer, Data);

                }
                //Going from Memory to Local
                else if (_channelNumber > -1 && value == -1)
                {
                    //Get the pointer to the specified Struct by Variable Name of the same name as the Struct
                    _structPointer = _memoryCore.GetVariablePointer(StructName);

                    //Calculate any channel offset if this struct exists for each channel
                    _structPointer.Offset += (ushort)(Size * ChannelNumber);

                    _localData = _memoryCore.GetArray(_structPointer, Size).ToArray();
                }

                //Set the new Data Source
                _dataSource = value > -1 ? DataSource.Memory : DataSource.Local;

                //Set the new Channel Number
                _channelNumber = value;

            }

        }

        private short _channelNumber = -1;

        /// <summary>
        ///     Size of the Struct in Bytes
        /// </summary>
        private ushort Size { get; } = size;

        /// <summary>
        ///     Cached Pointer to the Struct in Memory
        /// </summary>
        private FarPtr _structPointer;

        private byte[] _localData = new byte[size];

        /// <summary>
        ///     Essentially a pointer to the data in memory for the specified struct
        /// </summary>
        public Span<byte> Data => _dataSource == DataSource.Memory ? _memoryCore.GetArray(_structPointer, Size) : _localData;
    }
}
