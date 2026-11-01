using Iced.Intel;
using MBBSEmu.CPU;
using MBBSEmu.Extensions;
using Xunit;

namespace MBBSEmu.Tests.CPU
{
    public class F2XM1_Tests : CpuTestBase
    {
        /// <summary>
        ///     Expected values: an x87 coprocessor, compared to 52 bits
        /// </summary>
        [Theory]
        [InlineData(1d, (ushort)0x3FFF, 0x8000000000000000UL)]
        [InlineData(-1d, (ushort)0xBFFE, 0x8000000000000000UL)]
        [InlineData(0.5d, (ushort)0x3FFD, 0xD413CCCFE7799211UL)]
        [InlineData(0d, (ushort)0x0000, 0x0000000000000000UL)]
        public void F2XM1_Test(double ST0Value, ushort expectedSignExponent, ulong expectedSignificand)
        {
            Reset();

            mbbsEmuCpuRegisters.Fpu.SetStackTop(0);
            mbbsEmuCpuCore.FpuStack[0] = ST0Value;

            var instructions = new Assembler(16);
            instructions.f2xm1();
            CreateCodeSegment(instructions);

            mbbsEmuCpuCore.Tick();

            var result = mbbsEmuCpuCore.FpuStack[mbbsEmuCpuRegisters.Fpu.GetStackTop()];
            Assert.Equal(expectedSignExponent, result.SignExponent);
            //52 bits of the expected value: exact for a zero or a denormal shorter than that
            var tolerance = (long)(expectedSignificand >> 51);
            Assert.InRange((long)(result.Significand - expectedSignificand), -tolerance, tolerance);
        }

        /// <summary>
        ///     A NaN is made quiet (IE only if it was signalling) and an unsupported encoding gives the real indefinite
        ///     (IE); C1 is cleared, C2 left as it was.
        ///     Expected values: an x87 coprocessor, with C0 to C3 set beforehand.
        /// </summary>
        [Theory]
        [InlineData((ushort)0x7FFF, 0xC000000000000001UL, (ushort)0x7FFF, 0xC000000000000001UL, (ushort)0x00)] //+quiet NaN
        [InlineData((ushort)0xFFFF, 0xE000000000000000UL, (ushort)0xFFFF, 0xE000000000000000UL, (ushort)0x00)] //-quiet NaN, larger significand
        [InlineData((ushort)0x7FFF, 0xA000000000000000UL, (ushort)0x7FFF, 0xE000000000000000UL, (ushort)0x01)] //+signalling NaN
        [InlineData((ushort)0xFFFF, 0x8000000000000001UL, (ushort)0xFFFF, 0xC000000000000001UL, (ushort)0x01)] //-signalling NaN, smaller significand
        [InlineData((ushort)0x4000, 0x4000000000000000UL, (ushort)0xFFFF, 0xC000000000000000UL, (ushort)0x01)] //an unnormal
        [InlineData((ushort)0x7FFF, 0x0000000000000000UL, (ushort)0xFFFF, 0xC000000000000000UL, (ushort)0x01)] //a pseudo-infinity
        [InlineData((ushort)0x7FFF, 0x4000000000000000UL, (ushort)0xFFFF, 0xC000000000000000UL, (ushort)0x01)] //a pseudo-NaN
        public void F2XM1_NaN(ushort ST0SignExponent, ulong ST0Significand, ushort expectedSignExponent,
            ulong expectedSignificand, ushort expectedExceptions)
        {
            Reset();

            mbbsEmuCpuRegisters.Fpu.SetStackTop(1);
            mbbsEmuCpuCore.FpuStack[1] = new Float80(ST0SignExponent, ST0Significand);
            mbbsEmuCpuRegisters.Fpu.SetFlag(EnumFpuStatusFlags.Code1);
            mbbsEmuCpuRegisters.Fpu.SetFlag(EnumFpuStatusFlags.Code2);

            var instructions = new Assembler(16);
            instructions.f2xm1();
            CreateCodeSegment(instructions);

            mbbsEmuCpuCore.Tick();

            Assert.Equal(1, mbbsEmuCpuRegisters.Fpu.GetStackTop());
            Assert.Equal(new Float80(expectedSignExponent, expectedSignificand), mbbsEmuCpuCore.FpuStack[1]);
            Assert.Equal(expectedExceptions, mbbsEmuCpuRegisters.Fpu.StatusWord & 0x3F);
            Assert.False(mbbsEmuCpuRegisters.Fpu.StatusWord.IsFlagSet((ushort)EnumFpuStatusFlags.Code1));
            Assert.True(mbbsEmuCpuRegisters.Fpu.StatusWord.IsFlagSet((ushort)EnumFpuStatusFlags.Code2));
        }

        /// <summary>
        ///     2^ST(0) - 1 keeps a small ST(0)'s precision, down to the denormals (x * ln(2)), and -0 stays -0.
        ///     Expected values: an x87 coprocessor, compared to 52 bits (the logarithm is computed through double).
        /// </summary>
        [Theory]
        [InlineData((ushort)0x3FFE, 0xFBFEC596FC2605B7UL, (ushort)0x3FFE, 0xFA7A68EF1D19986CUL)] //|x| in [2^-1, 2^-0)
        [InlineData((ushort)0x3FFD, 0xE28D6D0B33F4ED50UL, (ushort)0x3FFD, 0xB7C769F7228D0727UL)] //|x| in [2^-2, 2^-1)
        [InlineData((ushort)0x3FFB, 0x9981551BCD439B62UL, (ushort)0x3FFA, 0xDA6DAA42696973B6UL)] //|x| in [2^-4, 2^-3)
        [InlineData((ushort)0xBFF9, 0xC5C148CC91382195UL, (ushort)0xBFF9, 0x87EED2F73FCD864DUL)] //|x| in [2^-6, 2^-5)
        [InlineData((ushort)0xBFF4, 0xF3ABDFAC6B3F3720UL, (ushort)0xBFF4, 0xA8D881DC072E02E0UL)] //|x| in [2^-11, 2^-10)
        [InlineData((ushort)0x3FEA, 0xF4F1E429D389432FUL, (ushort)0x3FEA, 0xA9C86CEC3151ED54UL)] //|x| in [2^-21, 2^-20)
        [InlineData((ushort)0x3FE0, 0xF4704ECA6C46C9C1UL, (ushort)0x3FE0, 0xA96E9760A30D90A4UL)] //|x| in [2^-31, 2^-30)
        [InlineData((ushort)0x3FD6, 0xBFF7097D3D015F22UL, (ushort)0x3FD6, 0x850F5B8AD79E5CEBUL)] //|x| in [2^-41, 2^-40)
        [InlineData((ushort)0x3FCC, 0xA4C946886675531DUL, (ushort)0x3FCB, 0xE47135A6A457E717UL)] //|x| in [2^-51, 2^-50)
        [InlineData((ushort)0x3FC9, 0xD4FF468D51F40D53UL, (ushort)0x3FC9, 0x93A36D6640CD0A2FUL)] //|x| in [2^-54, 2^-53)
        [InlineData((ushort)0xBFC7, 0xF6A84A02F35FC253UL, (ushort)0xBFC7, 0xAAF84934E4C28DFFUL)] //|x| in [2^-56, 2^-55)
        [InlineData((ushort)0xBFC2, 0xBC0A8E4DF0FDB545UL, (ushort)0xBFC2, 0x82571AB23D6A0D3DUL)] //|x| in [2^-61, 2^-60)
        [InlineData((ushort)0xBFBF, 0xBD1FB3EEEECC3CADUL, (ushort)0xBFBF, 0x8317353B4142C84EUL)] //|x| in [2^-64, 2^-63)
        [InlineData((ushort)0x3FBE, 0xBD5C5164E4BCE3D0UL, (ushort)0x3FBE, 0x8341391D9BA8E49FUL)] //|x| in [2^-65, 2^-64)
        [InlineData((ushort)0x3FB8, 0xBB54F6F948EAB0A1UL, (ushort)0x3FB8, 0x81D93C22511F9D12UL)] //|x| in [2^-71, 2^-70)
        [InlineData((ushort)0xBF9A, 0x9DC06CE828F23083UL, (ushort)0xBF99, 0xDAB0B38406DB231AUL)] //|x| in [2^-101, 2^-100)
        [InlineData((ushort)0xBC16, 0xB05AE2E5655E5AD3UL, (ushort)0xBC15, 0xF47ADFB90FB41199UL)] //|x| in [2^-1001, 2^-1000)
        [InlineData((ushort)0x817E, 0xC7B953D087DEF749UL, (ushort)0x817E, 0x8A70262D7ED56FE0UL)] //|x| in [2^-16001, 2^-16000)
        [InlineData((ushort)0x0000, 0xD9DD125AA1B6EA15UL, (ushort)0x0001, 0x9702F28899AC6290UL)] //|x| in [2^-16383, 2^-16382)
        [InlineData((ushort)0x0000, 0x0000000000000001UL, (ushort)0x0000, 0x0000000000000001UL)] //a denormal: x * ln(2)
        [InlineData((ushort)0x0000, 0x4000000000000000UL, (ushort)0x0000, 0x2C5C85FDF473DE6BUL)] //a denormal: x * ln(2)
        [InlineData((ushort)0x0000, 0x7FFFFFFFFFFFFFFFUL, (ushort)0x0000, 0x58B90BFBE8E7BCD5UL)] //a denormal: x * ln(2)
        [InlineData((ushort)0x8000, 0x0000000000000001UL, (ushort)0x8000, 0x0000000000000001UL)] //a denormal: x * ln(2)
        [InlineData((ushort)0x0000, 0x8000000000000000UL, (ushort)0x0000, 0x58B90BFBE8E7BCD6UL)] //a denormal: x * ln(2)
        [InlineData((ushort)0x8000, 0x0000000000000000UL, (ushort)0x8000, 0x0000000000000000UL)] //-0
        public void F2XM1_SmallOperand(ushort ST0SignExponent, ulong ST0Significand, ushort expectedSignExponent,
            ulong expectedSignificand)
        {
            Reset();

            mbbsEmuCpuRegisters.Fpu.SetStackTop(1);
            mbbsEmuCpuCore.FpuStack[1] = new Float80(ST0SignExponent, ST0Significand);

            var instructions = new Assembler(16);
            instructions.f2xm1();
            CreateCodeSegment(instructions);

            mbbsEmuCpuCore.Tick();

            var result = mbbsEmuCpuCore.FpuStack[1];
            Assert.Equal(expectedSignExponent, result.SignExponent);
            //52 bits of the expected value: exact for a zero or a denormal shorter than that
            var tolerance = (long)(expectedSignificand >> 51);
            Assert.InRange((long)(result.Significand - expectedSignificand), -tolerance, tolerance);
        }
    }
}
