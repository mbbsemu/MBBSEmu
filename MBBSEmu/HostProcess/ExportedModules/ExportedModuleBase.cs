using MBBSEmu.CPU;
using MBBSEmu.DependencyInjection;
using MBBSEmu.Logging;
using MBBSEmu.Memory;
using MBBSEmu.Module;
using MBBSEmu.Session;
using NLog;
using System;
using System.Collections.Generic;
using System.IO;
using System.Runtime.CompilerServices;
using System.Text;

namespace MBBSEmu.HostProcess.ExportedModules
{
    /// <summary>
    ///     Base Class for Exported MajorBBS Routines
    /// </summary>
    public abstract class ExportedModuleBase
    {
        /// <summary>
        ///     Internal Variables are stored inside the system (module name, etc.)
        ///
        ///     We want to write these only once to the HostMemorySegment, so we use this
        ///     dictionary to track the variable by a given name (key) and the pointer to the segment
        ///     it lives in.
        /// </summary>
        private protected Dictionary<string, IntPtr16> HostMemoryVariables;

        private protected PointerDictionary<UserSession> ChannelDictionary;


        /// <summary>
        ///     Pointers to files opened using FOPEN
        /// </summary>
        private protected readonly PointerDictionary<MemoryStream> FilePointerDictionary;

        private protected readonly ILogger _logger;

        public CpuRegisters Registers;
        public MbbsModule Module;

        private readonly IntPtr16 HostMemoryPointer = new IntPtr16((ushort)EnumHostSegments.HostMemorySegmentBase, 0);
        private readonly IntPtr16 VariablePointer = new IntPtr16((ushort)EnumHostSegments.VariablePointerSegmentBase, 0);

        private protected ExportedModuleBase(MbbsModule module, PointerDictionary<UserSession> channelDictionary)
        {
            _logger = ServiceResolver.GetService<ILogger>();
            Module = module;
            ChannelDictionary = channelDictionary;
            HostMemoryVariables = new Dictionary<string, IntPtr16>();
            FilePointerDictionary = new PointerDictionary<MemoryStream>();
        }

        /// <summary>
        ///     Gets the parameter by ordinal passed into the routine
        /// </summary>
        /// <param name="parameterOrdinal"></param>
        /// <returns></returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private protected ushort GetParameter(ushort parameterOrdinal)
        {
            var parameterOffset = (ushort) (Registers.BP + 7 + (2 * parameterOrdinal));
            return Module.Memory.GetWord(Registers.SS, parameterOffset);
        }

        /// <summary>
        ///     Allocates and Handles Host Memory
        /// </summary>
        /// <param name="size"></param>
        /// <returns></returns>
        private protected IntPtr16 AllocateHostMemory(ushort size)
        {
            //Do we have enough room in the current segment?
            //If not, declare a new segment and start there
            if (size + HostMemoryPointer.Offset >= ushort.MaxValue)
            {
                HostMemoryPointer.Segment++;
                HostMemoryPointer.Offset = 0;

                if (HostMemoryPointer.Segment > 0x2FF)
                    throw new OutOfMemoryException("Exhausted Host Memory Segment Space 0x200->0x2FF");

                Module.Memory.AddSegment(HostMemoryPointer.Segment);
            }

            if(!Module.Memory.HasSegment(HostMemoryPointer.Segment))
                Module.Memory.AddSegment(HostMemoryPointer.Segment);

#if DEBUG
            _logger.Debug($"Allocated {size} bytes of memory in Host Memory Segment {HostMemoryPointer.Segment:X4}:{HostMemoryPointer.Offset:X4}");
#endif
            var currentOffset = HostMemoryPointer.Offset;
            HostMemoryPointer.Offset += size;
            return new IntPtr16(HostMemoryPointer.Segment, currentOffset);
        }

        /// <summary>
        ///     Returns a 4 byte Segment used to hold a IntPtr16 value
        ///
        ///     This is basically a ring buffer of a maximum of 64 segments,
        ///     which SHOULD be enough.... 
        /// </summary>
        /// <returns></returns>
        private protected ushort GetPointerSegment()
        {
            if (VariablePointer.Segment > 0x440)
                VariablePointer.Segment = 0x400;

            if(!Module.Memory.HasSegment(VariablePointer.Segment))
                Module.Memory.AddSegment(VariablePointer.Segment, 4);

            return VariablePointer.Segment++;
        }

        /// <summary>
        ///     Calculates which Segment & Offset the specified channel's memory is in the
        ///     Volatile Memory segments
        /// </summary>
        /// <param name="channel"></param>
        /// <returns></returns>
        private protected IntPtr16 GetVolatileMemoryPointer(byte channel)
        {
            var segment = (ushort)((ushort)EnumHostSegments.VolatileDataSegmentBase + (channel % 8));
            var offset = (ushort)((channel / 8) * 0x800);

            if(!Module.Memory.HasSegment(segment))
                Module.Memory.AddSegment(segment);

            return new IntPtr16(segment, offset);
        }

        private protected IntPtr16 GetHostMemoryVariablePointer(string variableName, ushort size = 0x400)
        {
            if (!HostMemoryVariables.TryGetValue(variableName, out var variablePointer))
            {
                //allocate 1k for the SPR buffer
                HostMemoryVariables[variableName] = AllocateHostMemory(size);

            }
            return variablePointer;
        }

        /// <summary>
        ///     Parses File Access characters passed into FOPEN
        /// </summary>
        /// <param name="flags"></param>
        /// <returns></returns>
        private protected EnumFileAccessFlags ParseFileAccessFlags(ReadOnlySpan<byte> flags)
        {
            var result = EnumFileAccessFlags.Text;

            foreach (var f in flags)
            {
                switch ((char)f)
                {
                    case 'r':
                        result |= EnumFileAccessFlags.Read;
                        break;
                    case 'w':
                        result |= EnumFileAccessFlags.Write;
                        break;
                    case 'a':
                        result |= EnumFileAccessFlags.Append;
                        break;
                    case '+':
                        result |= EnumFileAccessFlags.Update;
                        break;
                    case 'b':
                    {
                        result &= ~EnumFileAccessFlags.Text;
                        result |= EnumFileAccessFlags.Binary;
                        break;
                    }
                    default:
                        throw new ArgumentOutOfRangeException($"Unknown File Access Flag: {(char) f}");
                }
            }
            return result;
        }

        private static readonly char[] PrintfSpecifiers = {'c', 'd', 's', 'e', 'E', 'f', 'g', 'G', 'o', 'x', 'X', 'u', 'i', 'P', 'N', '%'};
        private static readonly char[] PrintfFlags = {'-', '+', ' ', '#', '0'};
        private static readonly char[] PrintfWidth = {'1', '2', '3', '4', '5', '6', '7', '8', '9', '0'};
        private static readonly char[] PrintfPrecision = {'.', '1', '2', '3', '4', '5', '6', '7', '8', '9', '0', '*' };
        private static readonly char[] PrintfLength = {'h', 'l', 'j', 'z', 't', 'L'};

        private static bool InSpan(ReadOnlySpan<char> spanToSearch, ReadOnlySpan<byte> character)
        {
            for (var i = 0; i < spanToSearch.Length; i++)
            {
                if (spanToSearch[i] == character[0])
                    return true;
            }
            return false;
        }

        private static bool IsPrintfPrecision(ReadOnlySpan<byte> c) => c[0] == PrintfPrecision[0];

        /// <summary>
        ///     Printf Parsing and Encoding
        /// </summary>
        /// <param name="s"></param>
        /// <param name="stringToParse"></param>
        /// <param name="startingParameterOrdinal"></param>
        /// <returns></returns>
        private protected ReadOnlySpan<byte> FormatPrintf(ReadOnlySpan<byte> stringToParse, ushort startingParameterOrdinal, bool isVsPrintf = false)
        {
            using var msOutput = new MemoryStream(stringToParse.Length);
            var currentParameter = startingParameterOrdinal;

            var vsPrintfBase = new IntPtr16();
            if (isVsPrintf)
            {
                vsPrintfBase.Offset = GetParameter(currentParameter++);
                vsPrintfBase.Segment = GetParameter(currentParameter++);
            }

            for (var i = 0; i < stringToParse.Length; i++)
            {
                //Found a Control Character
                if (stringToParse[i] == '%' && stringToParse[i + 1] != '%')
                {
                    using var msFormattedValue = new MemoryStream();
                    i++;

                    //Process Flags
                    var stringFlags = EnumPrintfFlags.None;
                    while (InSpan(PrintfFlags, stringToParse.Slice(i, 1)))
                    {
                        switch ((char) stringToParse[i])
                        {
                            case '-':
                                stringFlags |= EnumPrintfFlags.LeftJustify;
                                break;
                            case '+':
                                stringFlags |= EnumPrintfFlags.Signed;
                                break;
                            case ' ':
                                stringFlags |= EnumPrintfFlags.Space;
                                break;
                            case '#':
                                stringFlags |= EnumPrintfFlags.DecimalOrHex;
                                break;
                            case '0':
                                stringFlags |= EnumPrintfFlags.LeftPadZero;
                                break;
                        }
                        i++;
                    }

                    //Process Width
                    var stringWidth = 0;
                    var stringWidthValue = string.Empty;
                    while (InSpan(PrintfWidth, stringToParse.Slice(i, 1)))
                    {
                        switch ((char) stringToParse[i])
                        {
                            case '*':
                                stringWidth = -1;
                                break;
                            default:
                                stringWidthValue += (char) stringToParse[i];
                                break;

                        }
                        i++;
                    }
                    if (!string.IsNullOrEmpty(stringWidthValue))
                        stringWidth = int.Parse(stringWidthValue);

                    //Process Precision
                    var stringPrecision = 0;
                    var stringPrecisionValue = string.Empty;
                    while (InSpan(PrintfPrecision, stringToParse.Slice(i, 1)))
                    {
                        switch ((char)stringToParse[i])
                        {
                            case '.':
                                break;
                            case '*':
                                stringPrecision = -1;
                                break;
                            default:
                                stringPrecisionValue += (char)stringToParse[i];
                                break;

                        }
                        i++;
                    }
                    if (!string.IsNullOrEmpty(stringPrecisionValue))
                        stringPrecision = int.Parse(stringPrecisionValue);

                    //Process Length
                    //TODO -- We'll process it but ignore it for now
                    while (InSpan(PrintfLength, stringToParse.Slice(i, 1)))
                    {
                        i++;
                    }

                    //Finally i should be at the specifier 
                    if (!InSpan(PrintfSpecifiers, stringToParse.Slice(i, 1)))
                        throw new Exception("Invalid printf format");

                    switch ((char) stringToParse[i])
                    {
                        case 'c':
                        {
                            var charParameter = GetParameter(currentParameter++);
                            msFormattedValue.WriteByte((byte) charParameter);
                            break;
                        }
                        case 's':
                        {
                            ReadOnlySpan<byte> parameter;
                            if (!isVsPrintf)
                            {
                                var parameterOffset = GetParameter(currentParameter++);
                                var parameterSegment = GetParameter(currentParameter++);
                                parameter = Module.Memory.GetString(parameterSegment, parameterOffset);
                            }
                            else
                            {

                                var address = Module.Memory.GetArray(vsPrintfBase.Segment, vsPrintfBase.Offset, 4);
                                var stringPointer = new IntPtr16(address);
                                parameter = Module.Memory.GetString(stringPointer.Segment, stringPointer.Offset);
                                vsPrintfBase.Offset += 4;
                            }

                            if (parameter[^1] == 0x0)
                                parameter = parameter.Slice(0, parameter.Length - 1);

                            msFormattedValue.Write(parameter);
                            break;
                        }
                        case 'd':
                        {
                            if (!isVsPrintf)
                            {
                                var parameter = GetParameter(currentParameter++);
                                msFormattedValue.Write(Encoding.ASCII.GetBytes(parameter.ToString()));
                            }
                            else
                            {
                                var parameterString = ((short)Module.Memory.GetWord(vsPrintfBase.Segment, vsPrintfBase.Offset)).ToString();
                                msFormattedValue.Write(Encoding.ASCII.GetBytes(parameterString));
                                vsPrintfBase.Offset += 2;
                            }

                            break;
                        }
                        default:
                            throw new InvalidDataException(
                                $"Unhandled Printf Control Character: {(char) stringToParse[i + 1]}");
                    }

                    //Process Padding
                    if (stringWidth > 0 && stringWidth != msFormattedValue.Length)
                    {
                        //Need to pad
                        if (msFormattedValue.Length < stringWidth)
                        {
                            if (stringFlags.HasFlag(EnumPrintfFlags.LeftJustify))
                            {
                                //Pad at the end
                                while(msFormattedValue.Length < stringWidth)
                                    msFormattedValue.WriteByte((byte)' ');
                            }
                            else
                            {
                                //Pad beginning
                                var valueCache = msFormattedValue.ToArray();
                                msFormattedValue.SetLength(0);
                                while(msFormattedValue.Length < stringWidth - valueCache.Length)
                                    msFormattedValue.WriteByte((byte)' ');

                                msFormattedValue.Write(valueCache);
                            }
                        }

                        //Need to truncate -- EZPZ
                        if (msFormattedValue.Length > stringWidth)
                            msFormattedValue.SetLength(stringWidth);
                    }
                    msOutput.Write(msFormattedValue.ToArray());
                    continue;
                }

                msOutput.WriteByte(stringToParse[i]);
            }
            return msOutput.ToArray();
        }
    }
}