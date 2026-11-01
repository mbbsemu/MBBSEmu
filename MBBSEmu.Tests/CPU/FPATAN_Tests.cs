using Iced.Intel;
using MBBSEmu.CPU;
using MBBSEmu.Extensions;
using Xunit;

namespace MBBSEmu.Tests.CPU
{
    public class FPATAN_Tests : CpuTestBase
    {
        [Theory]
        [InlineData(1d, 2d, 1.1071487177940904d)]
        [InlineData(1d, 3d, 1.2490457723982544d)]
        [InlineData(double.NegativeInfinity, double.PositiveInfinity, 2.356194490192345d)]
        [InlineData(double.NaN, double.NaN, double.NaN)]
        [InlineData(double.NegativeInfinity, double.NaN, double.NaN)]
        [InlineData(1d, double.NaN, double.NaN)]
        [InlineData(double.NaN, 1d, double.NaN)] // Switch correct, math.Atan2 result still double.NaN
        [InlineData(double.NaN, -1d, double.NaN)] // Switch correct, math.Atan2 result still double.NaN
        [InlineData(0d, 0d, 0d)]
        public void FPATAN_Test(double ST0ValueX, double ST1ValueY, double expectedValue)
        {
            Reset();

            mbbsEmuCpuRegisters.Fpu.SetStackTop(1);
            mbbsEmuCpuCore.FpuStack[1] = ST0ValueX; //ST0
            mbbsEmuCpuCore.FpuStack[0] = ST1ValueY; //ST1

            var instructions = new Assembler(16);
            instructions.fpatan();
            CreateCodeSegment(instructions);

            mbbsEmuCpuCore.Tick();

            Assert.Equal(expectedValue, mbbsEmuCpuCore.FpuStack[mbbsEmuCpuRegisters.Fpu.GetStackTop()]);
        }

        /// <summary>
        ///     Finite operands outside double's range keep their ratio; a positive ST(0) more than 2^40 times ST(1)
        ///     gives the quotient, which a double may not hold. Expected values: an x87 coprocessor, compared to 52 bits
        ///     (FPATAN computes through double).
        /// </summary>
        [Theory]
        [InlineData((ushort)0x4400, 0x8000000000000000UL, (ushort)0x43FF, 0x8000000000000000UL, (ushort)0x3FFD, 0xED63382B0DDA7B45UL)] //atan(2^1024 / 2^1025)
        [InlineData((ushort)0x7FFF, 0x8000000000000000UL, (ushort)0x47CF, 0x8000000000000000UL, (ushort)0x0000, 0x0000000000000000UL)] //2^2000 / +infinity: +0
        [InlineData((ushort)0x3BB3, 0x8000000000000000UL, (ushort)0x3BB3, 0x8000000000000000UL, (ushort)0x3FFE, 0xC90FDAA22168C235UL)] //2^-1100 / 2^-1100: pi/4
        [InlineData((ushort)0xD387, 0x8000000000000000UL, (ushort)0x5386, 0x8000000000000000UL, (ushort)0x4000, 0xAB63739CBFAD72CCUL)] //2^4999 / -2^5000
        [InlineData((ushort)0x0000, 0x0000000000000001UL, (ushort)0x0000, 0x0000000000000003UL, (ushort)0x3FFF, 0x9FE0BB5BD42AFFECUL)] //two denormals: atan(3)
        [InlineData((ushort)0x47CF, 0x8000000000000000UL, (ushort)0x3FFF, 0x8000000000000000UL, (ushort)0x382F, 0x8000000000000000UL)] //1 / 2^2000: 2^-2000
        [InlineData((ushort)0x47CF, 0x8000000000000000UL, (ushort)0xBFFF, 0x8000000000000000UL, (ushort)0xB82F, 0x8000000000000000UL)] //-1 / 2^2000: -2^-2000
        [InlineData((ushort)0x47CF, 0xC000000000000000UL, (ushort)0x3FFF, 0x8000000000000000UL, (ushort)0x382E, 0xAAAAAAAAAAAAAAABUL)] //1 / (1.5 * 2^2000)
        [InlineData((ushort)0x47CF, 0x8000000000000000UL, (ushort)0x43E7, 0x8000000000000000UL, (ushort)0x3C17, 0x8000000000000000UL)] //2^1000 / 2^2000: 2^-1000
        [InlineData((ushort)0x7E7F, 0x8000000000000000UL, (ushort)0x3E6F, 0x8000000000000000UL, (ushort)0x0000, 0x0000200000000000UL)] //2^-400 / 2^16000: the denormal 2^-16400
        public void FPATAN_OutsideDoubleRange(ushort ST0SignExponent, ulong ST0Significand, ushort ST1SignExponent,
            ulong ST1Significand, ushort expectedSignExponent, ulong expectedSignificand)
        {
            Reset();

            mbbsEmuCpuRegisters.Fpu.SetStackTop(1);
            mbbsEmuCpuCore.FpuStack[1] = new Float80(ST0SignExponent, ST0Significand); //ST0
            mbbsEmuCpuCore.FpuStack[0] = new Float80(ST1SignExponent, ST1Significand); //ST1

            var instructions = new Assembler(16);
            instructions.fpatan();
            CreateCodeSegment(instructions);

            mbbsEmuCpuCore.Tick();

            var result = mbbsEmuCpuCore.FpuStack[mbbsEmuCpuRegisters.Fpu.GetStackTop()];
            Assert.Equal(expectedSignExponent, result.SignExponent);
            Assert.InRange((long)(result.Significand - expectedSignificand), -4096, 4096);
        }

        /// <summary>
        ///     A NaN operand gives the NaN made quiet (IE only if it was signalling; of two NaNs, the one with the
        ///     larger significand, the positive one when they are equal), ahead of every other invalid case; an
        ///     unsupported encoding gives the real indefinite (IE). C1 is cleared. Expected values: an x87 coprocessor.
        /// </summary>
        [Theory]
        [InlineData((ushort)0x7FFF, 0xC000000000000001UL, (ushort)0x3FFF, 0x8000000000000000UL, (ushort)0x7FFF, 0xC000000000000001UL, (ushort)0x00)] //ST(0) +quiet NaN, ST(1) 1
        [InlineData((ushort)0x3FFF, 0x8000000000000000UL, (ushort)0x7FFF, 0xC000000000000001UL, (ushort)0x7FFF, 0xC000000000000001UL, (ushort)0x00)] //ST(0) 1, ST(1) +quiet NaN
        [InlineData((ushort)0x7FFF, 0xA000000000000000UL, (ushort)0x3FFF, 0x8000000000000000UL, (ushort)0x7FFF, 0xE000000000000000UL, (ushort)0x01)] //ST(0) +signalling NaN, ST(1) 1
        [InlineData((ushort)0x3FFF, 0x8000000000000000UL, (ushort)0x7FFF, 0xA000000000000000UL, (ushort)0x7FFF, 0xE000000000000000UL, (ushort)0x01)] //ST(0) 1, ST(1) +signalling NaN
        [InlineData((ushort)0x7FFF, 0xC000000000000001UL, (ushort)0xFFFF, 0xE000000000000000UL, (ushort)0xFFFF, 0xE000000000000000UL, (ushort)0x00)] //ST(0) +quiet NaN, ST(1) -quiet NaN, larger significand
        [InlineData((ushort)0xFFFF, 0xE000000000000000UL, (ushort)0x7FFF, 0xC000000000000001UL, (ushort)0xFFFF, 0xE000000000000000UL, (ushort)0x00)] //ST(0) -quiet NaN, larger significand, ST(1) +quiet NaN
        [InlineData((ushort)0x7FFF, 0xA000000000000000UL, (ushort)0xFFFF, 0xE000000000000000UL, (ushort)0xFFFF, 0xE000000000000000UL, (ushort)0x01)] //ST(0) +signalling NaN, ST(1) -quiet NaN, larger significand
        [InlineData((ushort)0x7FFF, 0xC000000000000001UL, (ushort)0xFFFF, 0x8000000000000001UL, (ushort)0x7FFF, 0xC000000000000001UL, (ushort)0x01)] //ST(0) +quiet NaN, ST(1) -signalling NaN, smaller significand
        [InlineData((ushort)0x7FFF, 0xA000000000000000UL, (ushort)0xFFFF, 0x8000000000000001UL, (ushort)0x7FFF, 0xE000000000000000UL, (ushort)0x01)] //ST(0) +signalling NaN, ST(1) -signalling NaN, smaller significand
        [InlineData((ushort)0xFFFF, 0x8000000000000001UL, (ushort)0x7FFF, 0xA000000000000000UL, (ushort)0x7FFF, 0xE000000000000000UL, (ushort)0x01)] //ST(0) -signalling NaN, smaller significand, ST(1) +signalling NaN
        [InlineData((ushort)0x7FFF, 0xC000000000000001UL, (ushort)0xFFFF, 0xC000000000000001UL, (ushort)0x7FFF, 0xC000000000000001UL, (ushort)0x00)] //ST(0) +quiet NaN, ST(1) -quiet NaN, same significand
        [InlineData((ushort)0xFFFF, 0xC000000000000001UL, (ushort)0x7FFF, 0xC000000000000001UL, (ushort)0x7FFF, 0xC000000000000001UL, (ushort)0x00)] //ST(0) -quiet NaN, same significand, ST(1) +quiet NaN
        [InlineData((ushort)0x7FFF, 0xC000000000000001UL, (ushort)0x7FFF, 0x8000000000000000UL, (ushort)0x7FFF, 0xC000000000000001UL, (ushort)0x00)] //ST(0) +quiet NaN, ST(1) +infinity
        [InlineData((ushort)0x7FFF, 0xC000000000000001UL, (ushort)0x0000, 0x0000000000000000UL, (ushort)0x7FFF, 0xC000000000000001UL, (ushort)0x00)] //ST(0) +quiet NaN, ST(1) +0
        [InlineData((ushort)0x7FFF, 0xC000000000000001UL, (ushort)0x0000, 0x0000000000000001UL, (ushort)0x7FFF, 0xC000000000000001UL, (ushort)0x00)] //ST(0) +quiet NaN, ST(1) a denormal
        [InlineData((ushort)0x0000, 0x0000000000000001UL, (ushort)0x7FFF, 0xC000000000000001UL, (ushort)0x7FFF, 0xC000000000000001UL, (ushort)0x00)] //ST(0) a denormal, ST(1) +quiet NaN
        [InlineData((ushort)0x4000, 0x4000000000000000UL, (ushort)0x3FFF, 0x8000000000000000UL, (ushort)0xFFFF, 0xC000000000000000UL, (ushort)0x01)] //ST(0) an unnormal, ST(1) 1
        [InlineData((ushort)0x3FFF, 0x8000000000000000UL, (ushort)0x4000, 0x4000000000000000UL, (ushort)0xFFFF, 0xC000000000000000UL, (ushort)0x01)] //ST(0) 1, ST(1) an unnormal
        [InlineData((ushort)0x4000, 0x4000000000000000UL, (ushort)0x7FFF, 0xC000000000000001UL, (ushort)0xFFFF, 0xC000000000000000UL, (ushort)0x01)] //ST(0) an unnormal, ST(1) +quiet NaN
        [InlineData((ushort)0xBFFF, 0x8000000000000000UL, (ushort)0x7FFF, 0xC000000000000001UL, (ushort)0x7FFF, 0xC000000000000001UL, (ushort)0x00)] //ST(0) -1, ST(1) +quiet NaN
        [InlineData((ushort)0x0000, 0x0000000000000000UL, (ushort)0x7FFF, 0xC000000000000001UL, (ushort)0x7FFF, 0xC000000000000001UL, (ushort)0x00)] //ST(0) +0, ST(1) +quiet NaN
        [InlineData((ushort)0xC000, 0x8000000000000000UL, (ushort)0x7FFF, 0xC000000000000001UL, (ushort)0x7FFF, 0xC000000000000001UL, (ushort)0x00)] //ST(0) -2, ST(1) +quiet NaN
        public void FPATAN_NaN(ushort ST0SignExponent, ulong ST0Significand, ushort ST1SignExponent,
            ulong ST1Significand, ushort expectedSignExponent, ulong expectedSignificand, ushort expectedExceptions)
        {
            Reset();

            mbbsEmuCpuRegisters.Fpu.SetStackTop(1);
            mbbsEmuCpuCore.FpuStack[1] = new Float80(ST0SignExponent, ST0Significand); //ST0
            mbbsEmuCpuCore.FpuStack[0] = new Float80(ST1SignExponent, ST1Significand); //ST1
            mbbsEmuCpuRegisters.Fpu.SetFlag(EnumFpuStatusFlags.Code1);

            var instructions = new Assembler(16);
            instructions.fpatan();
            CreateCodeSegment(instructions);

            mbbsEmuCpuCore.Tick();

            Assert.Equal(0, mbbsEmuCpuRegisters.Fpu.GetStackTop());
            Assert.Equal(new Float80(expectedSignExponent, expectedSignificand), mbbsEmuCpuCore.FpuStack[0]);
            Assert.Equal(expectedExceptions, mbbsEmuCpuRegisters.Fpu.StatusWord & 0x3F);
            Assert.False(mbbsEmuCpuRegisters.Fpu.StatusWord.IsFlagSet((ushort)EnumFpuStatusFlags.Code1));
        }

        /// <summary>
        ///     A denormal operand raises DE, whatever the other operand is. Expected values: an x87 coprocessor,
        ///     compared to 52 bits (FPATAN computes through double); the exception flags but PE exactly.
        /// </summary>
        [Theory]
        [InlineData((ushort)0x0000, 0x0000000000000000UL, (ushort)0x0000, 0x0000000000000001UL, (ushort)0x3FFF, 0xC90FDAA22168C235UL, (ushort)0x02)] //ST(0) +0, ST(1) the smallest denormal
        [InlineData((ushort)0x0000, 0x0000000000000001UL, (ushort)0x0000, 0x0000000000000000UL, (ushort)0x0000, 0x0000000000000000UL, (ushort)0x02)] //ST(0) the smallest denormal, ST(1) +0
        [InlineData((ushort)0x0000, 0x0000000000000001UL, (ushort)0x0000, 0x0000000000000001UL, (ushort)0x3FFE, 0xC90FDAA22168C235UL, (ushort)0x02)] //ST(0) the smallest denormal, ST(1) the smallest denormal
        [InlineData((ushort)0x8000, 0x0000000000000001UL, (ushort)0x0000, 0x0000000000000001UL, (ushort)0x4000, 0x96CBE3F9990E91A8UL, (ushort)0x02)] //ST(0) -(the smallest denormal), ST(1) the smallest denormal
        [InlineData((ushort)0x0000, 0x0000000000000001UL, (ushort)0x3FFF, 0x8000000000000000UL, (ushort)0x3FFF, 0xC90FDAA22168C235UL, (ushort)0x02)] //ST(0) the smallest denormal, ST(1) 1
        [InlineData((ushort)0xBFFF, 0x8000000000000000UL, (ushort)0x0000, 0x0000000000000001UL, (ushort)0x4000, 0xC90FDAA22168C235UL, (ushort)0x02)] //ST(0) -1, ST(1) the smallest denormal
        [InlineData((ushort)0x0000, 0x0000000000000001UL, (ushort)0x7FFF, 0x8000000000000000UL, (ushort)0x3FFF, 0xC90FDAA22168C235UL, (ushort)0x02)] //ST(0) the smallest denormal, ST(1) +infinity
        [InlineData((ushort)0x7FFF, 0x8000000000000000UL, (ushort)0x8000, 0x0000000000000001UL, (ushort)0x8000, 0x0000000000000000UL, (ushort)0x02)] //ST(0) +infinity, ST(1) -(the smallest denormal)
        [InlineData((ushort)0x0000, 0x8000000000000000UL, (ushort)0x3FFF, 0x8000000000000000UL, (ushort)0x3FFF, 0xC90FDAA22168C235UL, (ushort)0x02)] //ST(0) a pseudo-denormal, ST(1) 1
        [InlineData((ushort)0x4000, 0xC000000000000000UL, (ushort)0x0000, 0x4000000000000000UL, (ushort)0x0000, 0x1555555555555555UL, (ushort)0x12)] //ST(0) 3, ST(1) a denormal
        [InlineData((ushort)0x0000, 0x7FFFFFFFFFFFFFFFUL, (ushort)0x0000, 0x4000000000000000UL, (ushort)0x3FFD, 0xED63382B0DDA7B47UL, (ushort)0x02)] //ST(0) the largest denormal, ST(1) a denormal
        public void FPATAN_DenormalOperand(ushort ST0SignExponent, ulong ST0Significand, ushort ST1SignExponent,
            ulong ST1Significand, ushort expectedSignExponent, ulong expectedSignificand, ushort expectedExceptions)
        {
            Reset();

            mbbsEmuCpuRegisters.Fpu.SetStackTop(1);
            mbbsEmuCpuCore.FpuStack[1] = new Float80(ST0SignExponent, ST0Significand); //ST0
            mbbsEmuCpuCore.FpuStack[0] = new Float80(ST1SignExponent, ST1Significand); //ST1

            var instructions = new Assembler(16);
            instructions.fpatan();
            CreateCodeSegment(instructions);

            mbbsEmuCpuCore.Tick();

            var result = mbbsEmuCpuCore.FpuStack[mbbsEmuCpuRegisters.Fpu.GetStackTop()];
            Assert.Equal(expectedSignExponent, result.SignExponent);
            Assert.InRange((long)(result.Significand - expectedSignificand), -4096, 4096);
            Assert.Equal(expectedExceptions, mbbsEmuCpuRegisters.Fpu.StatusWord & 0x1F);
        }

        /// <summary>
        ///     The quotient ST(1) / ST(0) (exponents more than 40 apart): PE unless ST(1) is zero, UE when the quotient
        ///     is a denormal, DE for a denormal operand. Expected values: an x87 coprocessor, the result and every
        ///     exception flag exactly.
        /// </summary>
        [Theory]
        [InlineData((ushort)0x47CF, 0x8000000000000000UL, (ushort)0x3FFF, 0x8000000000000000UL, (ushort)0x382F, 0x8000000000000000UL, (ushort)0x20)] //ST(0) 2^2000, ST(1) 1
        [InlineData((ushort)0x3FFF, 0x8000000000000000UL, (ushort)0x0000, 0x0000000000000001UL, (ushort)0x0000, 0x0000000000000001UL, (ushort)0x32)] //ST(0) 1, ST(1) the smallest denormal
        [InlineData((ushort)0x4040, 0x8000000000000000UL, (ushort)0x0000, 0x0000000000000001UL, (ushort)0x0000, 0x0000000000000000UL, (ushort)0x32)] //ST(0) 2^65, ST(1) the smallest denormal
        [InlineData((ushort)0x7E7F, 0x8000000000000000UL, (ushort)0x3E6F, 0x8000000000000000UL, (ushort)0x0000, 0x0000200000000000UL, (ushort)0x30)] //ST(0) 2^16000, ST(1) 2^-400
        [InlineData((ushort)0x47CF, 0x8000000000000000UL, (ushort)0x0000, 0x0000000000000000UL, (ushort)0x0000, 0x0000000000000000UL, (ushort)0x00)] //ST(0) 2^2000, ST(1) +0
        [InlineData((ushort)0x3FFF, 0x8000000000000000UL, (ushort)0x0000, 0x8000000000000000UL, (ushort)0x0001, 0x8000000000000000UL, (ushort)0x22)] //ST(0) 1, ST(1) a pseudo-denormal
        [InlineData((ushort)0x4028, 0x8000000000000000UL, (ushort)0xBFFF, 0x8000000000000000UL, (ushort)0xBFD6, 0x8000000000000000UL, (ushort)0x20)] //ST(0) 2^41, ST(1) -1
        [InlineData((ushort)0x7E7F, 0x8000000000000000UL, (ushort)0x3BE7, 0x8000000000000000UL, (ushort)0x0000, 0x0000000000000000UL, (ushort)0x30)] //ST(0) 2^16000, ST(1) 2^-1000
        public void FPATAN_Quotient_Flags(ushort ST0SignExponent, ulong ST0Significand, ushort ST1SignExponent,
            ulong ST1Significand, ushort expectedSignExponent, ulong expectedSignificand, ushort expectedExceptions)
        {
            Reset();

            mbbsEmuCpuRegisters.Fpu.SetStackTop(1);
            mbbsEmuCpuCore.FpuStack[1] = new Float80(ST0SignExponent, ST0Significand); //ST0
            mbbsEmuCpuCore.FpuStack[0] = new Float80(ST1SignExponent, ST1Significand); //ST1

            var instructions = new Assembler(16);
            instructions.fpatan();
            CreateCodeSegment(instructions);

            mbbsEmuCpuCore.Tick();

            var result = mbbsEmuCpuCore.FpuStack[mbbsEmuCpuRegisters.Fpu.GetStackTop()];
            Assert.Equal(new Float80(expectedSignExponent, expectedSignificand), result);
            Assert.Equal(expectedExceptions, mbbsEmuCpuRegisters.Fpu.StatusWord & 0x3F);
        }
    }
}
