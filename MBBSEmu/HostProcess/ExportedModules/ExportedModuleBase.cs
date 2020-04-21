using MBBSEmu.Btrieve;
using MBBSEmu.CPU;
using MBBSEmu.DependencyInjection;
using MBBSEmu.Memory;
using MBBSEmu.Module;
using MBBSEmu.Session;
using Microsoft.Extensions.Configuration;
using NLog;
using System;
using System.Collections.Generic;
using System.IO;
using System.Runtime.CompilerServices;
using System.Text;
using MBBSEmu.IO;

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
        private protected PointerDictionary<UserSession> ChannelDictionary;


        /// <summary>
        ///     Pointers to files opened using FOPEN
        /// </summary>
        private protected readonly PointerDictionary<FileStream> FilePointerDictionary;
        private protected readonly Dictionary<IntPtr16, BtrieveFileProcessor> BtrievePointerDictionaryNew;
        private protected readonly PointerDictionary<McvFile> McvPointerDictionary;

        private protected readonly ILogger _logger;
        private protected readonly IConfiguration _configuration;
        private protected readonly IFileUtility _fileFinder;

        public CpuRegisters Registers;
        public MbbsModule Module;

        private protected ushort ChannelNumber;

        private protected ExportedModuleBase(MbbsModule module, PointerDictionary<UserSession> channelDictionary)
        {
            _logger = ServiceResolver.GetService<ILogger>();
            _configuration = ServiceResolver.GetService<IConfiguration>();
            _fileFinder = ServiceResolver.GetService<IFileUtility>();

            Module = module;
            ChannelDictionary = channelDictionary;

            FilePointerDictionary = new PointerDictionary<FileStream>();
            McvPointerDictionary = new PointerDictionary<McvFile>();
            BtrievePointerDictionaryNew = new Dictionary<IntPtr16, BtrieveFileProcessor>();
        }

        /// <summary>
        ///     Gets the parameter by ordinal passed into the routine
        /// </summary>
        /// <param name="parameterOrdinal"></param>
        /// <returns></returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private protected ushort GetParameter(int parameterOrdinal)
        {
            var parameterOffset = (ushort)(Registers.BP + 7 + (2 * parameterOrdinal));
            return Module.Memory.GetWord(Registers.SS, parameterOffset);
        }

        /// <summary>
        ///     Gets the parameter pointer by ordinal passed into the routine
        /// </summary>
        /// <param name="parameterOrdinal"></param>
        /// <returns></returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private protected IntPtr16 GetParameterPointer(int parameterOrdinal)
        {
            return new IntPtr16(GetParameter(parameterOrdinal + 1), GetParameter(parameterOrdinal));
        }

        /// <summary>
        ///     Gets a long Parameter
        /// </summary>
        /// <param name="parameterOrdinal"></param>
        /// <returns></returns>
        private protected int GetParameterLong(int parameterOrdinal)
        {
            return GetParameter(parameterOrdinal) | (GetParameter(parameterOrdinal + 1) << 16);
        }

        /// <summary>
        ///     Gets a Unsigned Long Parameter
        /// </summary>
        /// <param name="parameterOrdinal"></param>
        /// <returns></returns>
        private protected uint GetParameterULong(int parameterOrdinal)
        {
            return (uint)(GetParameter(parameterOrdinal) | (GetParameter(parameterOrdinal + 1) << 16));
        }

        private static readonly char[] PrintfSpecifiers = { 'c', 'd', 's', 'e', 'E', 'f', 'g', 'G', 'o', 'x', 'X', 'u', 'i', 'P', 'N', '%' };
        private static readonly char[] PrintfFlags = { '-', '+', ' ', '#', '0' };
        private static readonly char[] PrintfWidth = { '1', '2', '3', '4', '5', '6', '7', '8', '9', '0', '*' };
        private static readonly char[] PrintfPrecision = { '.', '1', '2', '3', '4', '5', '6', '7', '8', '9', '0', '*' };
        private static readonly char[] PrintfLength = { 'h', 'l', 'j', 'z', 't', 'L' };

        private static bool InSpan(ReadOnlySpan<char> spanToSearch, ReadOnlySpan<byte> character)
        {
            foreach (var c in spanToSearch)
            {
                if (c == character[0])
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

                    //Found a Lone % as the last character in a string, consider it just outputting the string submitted
                    if (stringToParse[i] == 0x0)
                    {
                        var parameterOffset = GetParameter(currentParameter++);
                        var parameterSegment = GetParameter(currentParameter++);
                        if (Module.Memory.HasSegment(parameterSegment))
                        {
                            msOutput.Write(Module.Memory.GetString(parameterSegment, parameterOffset));
                        }
                        else
                        {
                            msOutput.Write(Encoding.ASCII.GetBytes("Invalid Pointer"));
                            _logger.Error($"Invalid Pointer: {parameterSegment:X4}:{parameterOffset:X4}");
                        }

                        continue;
                    }

                    //Process Flags
                    var stringFlags = EnumPrintfFlags.None;
                    while (InSpan(PrintfFlags, stringToParse.Slice(i, 1)))
                    {
                        switch ((char)stringToParse[i])
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
                        switch ((char)stringToParse[i])
                        {
                            case '*':
                                stringWidth = -1;
                                break;
                            default:
                                stringWidthValue += (char)stringToParse[i];
                                break;

                        }
                        i++;
                    }
                    if (!string.IsNullOrEmpty(stringWidthValue))
                        stringWidth = int.Parse(stringWidthValue);

                    if (stringWidth == -1)
                    {

                        if (!isVsPrintf)
                        {
                            //printf
                            stringWidth = GetParameter(currentParameter++);
                        }
                        else
                        {
                            //vsprintf
                            stringWidth = Module.Memory.GetWord(vsPrintfBase.Segment, vsPrintfBase.Offset);
                            vsPrintfBase.Offset += 2;
                        }
                    }

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
                    {
                        _logger.Warn($"Invalid printf format: {Encoding.ASCII.GetString(stringToParse)}");
                        continue;
                    }

                    switch ((char)stringToParse[i])
                    {
                        //Character
                        case 'c':
                            {
                                byte charParameter;
                                if (!isVsPrintf)
                                {
                                    charParameter = (byte)GetParameter(currentParameter++);

                                }
                                else
                                {
                                    charParameter = Module.Memory.GetByte(vsPrintfBase.Segment, vsPrintfBase.Offset);
                                    vsPrintfBase.Offset += 2;
                                }
                                msFormattedValue.WriteByte(charParameter);
                                break;

                            }
                        //String of characters
                        case 's':
                            {
                                ReadOnlySpan<byte> parameter;
                                if (!isVsPrintf)
                                {
                                    var parameterOffset = GetParameter(currentParameter++);
                                    var parameterSegment = GetParameter(currentParameter++);
                                    if (Module.Memory.HasSegment(parameterSegment))
                                    {
                                        parameter = Module.Memory.GetString(parameterSegment, parameterOffset);
                                    }
                                    else
                                    {
                                        parameter = Encoding.ASCII.GetBytes("Invalid Pointer");
                                        _logger.Error($"Invalid Pointer: {parameterSegment:X4}:{parameterOffset:X4}");
                                    }
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
                        //Signed decimal integer
                        case 'i':
                        case 'd':
                            {
                                if (!isVsPrintf)
                                {
                                    var parameter = (short)GetParameter(currentParameter++);
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
                        //Unsigned decimal integer
                        case 'u':
                            {
                                if (!isVsPrintf)
                                {
                                    var parameter = GetParameter(currentParameter++);
                                    msFormattedValue.Write(Encoding.ASCII.GetBytes(parameter.ToString()));
                                }
                                else
                                {
                                    var parameterString = Module.Memory.GetWord(vsPrintfBase.Segment, vsPrintfBase.Offset).ToString();
                                    msFormattedValue.Write(Encoding.ASCII.GetBytes(parameterString));
                                    vsPrintfBase.Offset += 2;
                                }

                                break;
                            }
                        case 'f':
                            {
                                var floatValue = new byte[8];
                                if (!isVsPrintf)
                                {
                                    var parameterHigh = GetParameterULong(currentParameter++);
                                    var parameterLow = GetParameterULong(currentParameter++);


                                    Array.Copy(BitConverter.GetBytes(parameterHigh), 0, floatValue, 0, 4);
                                    Array.Copy(BitConverter.GetBytes(parameterLow), 0, floatValue, 4, 4);


                                }
                                else
                                {
                                    floatValue = Module.Memory.GetArray(vsPrintfBase.Segment, vsPrintfBase.Offset, 8).ToArray();
                                    vsPrintfBase.Offset += 8;
                                }

                                msFormattedValue.Write(
                                    Encoding.ASCII.GetBytes(BitConverter.ToSingle(floatValue).ToString()));

                                break;
                            }
                        default:
                            throw new InvalidDataException(
                                $"Unhandled Printf Control Character: {(char)stringToParse[i + 1]}");
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
                                while (msFormattedValue.Length < stringWidth)
                                    msFormattedValue.WriteByte((byte)' ');
                            }
                            else
                            {
                                //Pad beginning
                                var valueCache = msFormattedValue.ToArray();
                                msFormattedValue.SetLength(0);
                                while (msFormattedValue.Length < stringWidth - valueCache.Length)
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

        /// <summary>
        ///     Implementation of sscanf C++ routine
        /// </summary>
        /// <param name="inputString"></param>
        /// <param name="formatString"></param>
        /// <param name="startingParameterOrdinal"></param>
        private protected void sscanf(ReadOnlySpan<byte> inputString, ReadOnlySpan<byte> formatString, ushort startingParameterOrdinal)
        {
            //Take input value, spit into array
            var stringValues = Encoding.ASCII.GetString(inputString).Split(' ');
            var valueOrdinal = 0;
            for (var i = 0; i < formatString.Length; i++)
            {
                if (formatString[i] == '%' && formatString[i + 1] != '*')
                {
                    i++;
                    switch ((char)formatString[i])
                    {
                        case 'd':
                            var numberValueDestinationPointer = GetParameterPointer(startingParameterOrdinal);
                            startingParameterOrdinal += 2;
                            var numberValue = short.Parse(stringValues[valueOrdinal++]);
                            Module.Memory.SetWord(numberValueDestinationPointer, (ushort)numberValue);
#if DEBUG
                            // _logger.Info($"Saved {numberValue} to {numberValueDestinationPointer}");
#endif
                            continue;
                        case 's':
                            var stringValueDestinationPointer = GetParameterPointer(startingParameterOrdinal);
                            startingParameterOrdinal += 2;
                            var stringValue = stringValues[valueOrdinal++] + "\0";
                            Module.Memory.SetArray(stringValueDestinationPointer, Encoding.ASCII.GetBytes(stringValue));
#if DEBUG
                            //_logger.Info($"Saved {Encoding.ASCII.GetBytes(stringValue)} to {stringValueDestinationPointer}");
#endif
                            continue;
                    }
                }
            }
        }

        private protected ReadOnlySpan<byte> StringFromArray(ReadOnlySpan<byte> inputArray, bool stripNull = false)
        {
            for (var i = 0; i < inputArray.Length; i++)
            {
                if (inputArray[i] == 0x0)
                    return inputArray.Slice(0, i + (stripNull ? 0 : 1));
            }

            _logger.Warn("Unable to find String terminator");
            return inputArray;
        }

        /// <summary>
        ///     Routine handles processing of text variables within an outprf string if they're present and registered
        /// </summary>
        /// <param name="outputBuffer"></param>
        /// <returns></returns>
        private protected ReadOnlySpan<byte> ProcessTextVariables(ReadOnlySpan<byte> outputBuffer)
        {
            using var newOutputBuffer = new MemoryStream(outputBuffer.Length * 2);
            for (var i = 0; i < outputBuffer.Length; i++)
            {
                //Look for initial signature byte -- faster
                if (outputBuffer[i] != 0x1)
                {
                    newOutputBuffer.WriteByte(outputBuffer[i]);
                    continue;
                }

                //If we found a 0x1 -- but it'd take us past the end of the buffer, we're done
                if (i + 3 >= outputBuffer.Length)
                    break;

                //Look for full signature of 0x1,0x4E,0x26
                if (outputBuffer[i + 1] != 0x4E && outputBuffer[i + 2] != 0x26)
                    continue;

                //Increment 3 Bytes
                i += 3;

                using var variableName = new MemoryStream();
                //Get variable name
                while (outputBuffer[i] != 0x1)
                {
                    variableName.WriteByte(outputBuffer[i]);
                    i++;
                }

                //Get Variable Entry Point
                var variableEntryPoint = Module.TextVariables[Encoding.ASCII.GetString(variableName.ToArray())];
                var resultRegisters = Module.Execute(variableEntryPoint, ChannelNumber, true, true);
                var variableData = Module.Memory.GetString(resultRegisters.DX, resultRegisters.AX, true);

#if DEBUG
                _logger.Info($"Processing Text Variable {Encoding.ASCII.GetString(variableName.ToArray())} ({variableEntryPoint}): {BitConverter.ToString(variableData.ToArray()).Replace("-", " ")}");
#endif
                newOutputBuffer.Write(variableData);
            }

            return newOutputBuffer.ToArray();
        }
    }
}