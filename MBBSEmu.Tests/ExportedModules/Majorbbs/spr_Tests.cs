using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using MBBSEmu.Memory;
using Xunit;

namespace MBBSEmu.Tests.ExportedModules.Majorbbs
{

    /// <summary>
    ///     Tests specifically for SPR, for printf formatting, please use prf
    /// </summary>
    public class spr_Tests : ExportedModuleTestBase
    {
        private const int SPR_ORDINAL = 559;

        private List<ushort> _parameters = new List<ushort>();

        [Theory]
        [InlineData("%d", "1", (ushort)1)]
        [InlineData("%s", "Test", "Test")]
        public void spr_Test(string inputString, string expectedString, params object[] values)
        {
            Reset();

            var inputStingParameterPointer = mbbsEmuMemoryCore.AllocateVariable(Guid.NewGuid().ToString(), (ushort)(inputString.Length + 1));
            mbbsEmuMemoryCore.SetArray(inputStingParameterPointer, Encoding.ASCII.GetBytes(inputString));
            _parameters.Add(inputStingParameterPointer.Offset);
            _parameters.Add(inputStingParameterPointer.Segment);

            foreach (var v in values)
            {
                switch (v)
                {
                    case string @parameterString:
                        {
                            var stringParameterPointer = mbbsEmuMemoryCore.AllocateVariable(Guid.NewGuid().ToString(), (ushort)(@parameterString.Length + 1));
                            mbbsEmuMemoryCore.SetArray(stringParameterPointer, Encoding.ASCII.GetBytes(@parameterString));
                            _parameters.Add(stringParameterPointer.Offset);
                            _parameters.Add(stringParameterPointer.Segment);
                            break;
                        }
                    case uint @parameterULong:
                        {
                            var longBytes = BitConverter.GetBytes(@parameterULong);
                            _parameters.Add(BitConverter.ToUInt16(longBytes, 0));
                            _parameters.Add(BitConverter.ToUInt16(longBytes, 2));
                            break;
                        }
                    case int @parameterLong:
                        {
                            var longBytes = BitConverter.GetBytes(@parameterLong);
                            _parameters.Add(BitConverter.ToUInt16(longBytes, 0));
                            _parameters.Add(BitConverter.ToUInt16(longBytes, 2));
                            break;
                        }
                    case ushort @parameterInt:
                        _parameters.Add(@parameterInt);
                        break;
                }
            }

            ExecuteApiTest(HostProcess.ExportedModules.Majorbbs.Segment, SPR_ORDINAL, _parameters);

            Assert.Equal(expectedString, Encoding.ASCII.GetString(mbbsEmuMemoryCore.GetString(mbbsEmuCpuRegisters.GetPointer(), true)));
        }

        [Fact]
        public void spr_Limit_Test()
        {
            Reset();

            var inputString = "%s";
            var parameterString = new string('X', 2000);

            var inputStingParameterPointer = mbbsEmuMemoryCore.AllocateVariable(Guid.NewGuid().ToString(), (ushort)(inputString.Length + 1));
            mbbsEmuMemoryCore.SetArray(inputStingParameterPointer, Encoding.ASCII.GetBytes(inputString));
            _parameters.Add(inputStingParameterPointer.Offset);
            _parameters.Add(inputStingParameterPointer.Segment);

            var stringParameterPointer = mbbsEmuMemoryCore.AllocateVariable(Guid.NewGuid().ToString(), (ushort)(@parameterString.Length + 1));
            mbbsEmuMemoryCore.SetArray(stringParameterPointer, Encoding.ASCII.GetBytes(@parameterString));
            _parameters.Add(stringParameterPointer.Offset);
            _parameters.Add(stringParameterPointer.Segment);


            Assert.Throws<OutOfMemoryException>(() => ExecuteApiTest(HostProcess.ExportedModules.Majorbbs.Segment, SPR_ORDINAL, _parameters));
        }

        [Fact]
        public void spr_Increment_Test()
        {
            Reset();

            var inputString = "%s";
            var parameterString = new string('X', 100);

            var inputStingParameterPointer = mbbsEmuMemoryCore.AllocateVariable(Guid.NewGuid().ToString(), (ushort)(inputString.Length + 1));
            mbbsEmuMemoryCore.SetArray(inputStingParameterPointer, Encoding.ASCII.GetBytes(inputString));
            _parameters.Add(inputStingParameterPointer.Offset);
            _parameters.Add(inputStingParameterPointer.Segment);

            var stringParameterPointer = mbbsEmuMemoryCore.AllocateVariable(Guid.NewGuid().ToString(), (ushort)(@parameterString.Length + 1));
            mbbsEmuMemoryCore.SetArray(stringParameterPointer, Encoding.ASCII.GetBytes(@parameterString));
            _parameters.Add(stringParameterPointer.Offset);
            _parameters.Add(stringParameterPointer.Segment);

            var pointers = new List<IntPtr16>();
            ExecuteApiTest(HostProcess.ExportedModules.Majorbbs.Segment, SPR_ORDINAL, _parameters);
            pointers.Add(mbbsEmuCpuRegisters.GetPointer());
            ExecuteApiTest(HostProcess.ExportedModules.Majorbbs.Segment, SPR_ORDINAL, _parameters);
            pointers.Add(mbbsEmuCpuRegisters.GetPointer()); 
            ExecuteApiTest(HostProcess.ExportedModules.Majorbbs.Segment, SPR_ORDINAL, _parameters);
            pointers.Add(mbbsEmuCpuRegisters.GetPointer()); 
            ExecuteApiTest(HostProcess.ExportedModules.Majorbbs.Segment, SPR_ORDINAL, _parameters);
            pointers.Add(mbbsEmuCpuRegisters.GetPointer());
            Assert.Equal(pointers.Count, pointers.GroupBy(x=> x).Count());
            
            //Test the variable pointer rolls over to the first
            ExecuteApiTest(HostProcess.ExportedModules.Majorbbs.Segment, SPR_ORDINAL, _parameters);
            pointers.Add(mbbsEmuCpuRegisters.GetPointer());
            Assert.NotEqual(pointers.Count, pointers.GroupBy(x => x).Count()); //count should be higher than the aggregate
        }

        protected override void Reset()
        {
            _parameters = new List<ushort>();
            base.Reset();
        }
    }
}
