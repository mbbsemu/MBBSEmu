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
using System.Linq;
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
        private protected PointerDictionary<UserSession> ChannelDictionary;


        /// <summary>
        ///     Pointers to files opened using FOPEN
        /// </summary>
        private protected readonly PointerDictionary<FileStream> FilePointerDictionary;
        private protected readonly Dictionary<IntPtr16, BtrieveFile> BtrievePointerDictionaryNew;
        private protected readonly PointerDictionary<McvFile> McvPointerDictionary;

        private protected readonly ILogger _logger;
        private protected readonly IConfigurationRoot _configuration;

        public CpuRegisters Registers;
        public MbbsModule Module;

        private protected ushort ChannelNumber;

        private protected ExportedModuleBase(MbbsModule module, PointerDictionary<UserSession> channelDictionary)
        {
            _logger = ServiceResolver.GetService<ILogger>();
            _configuration = ServiceResolver.GetService<IConfigurationRoot>();

            Module = module;
            ChannelDictionary = channelDictionary;

            FilePointerDictionary = new PointerDictionary<FileStream>();
            McvPointerDictionary = new PointerDictionary<McvFile>();
            BtrievePointerDictionaryNew = new Dictionary<IntPtr16, BtrieveFile>();
        }

        /// <summary>
        ///     Gets the parameter by ordinal passed into the routine
        /// </summary>
        /// <param name="parameterOrdinal"></param>
        /// <returns></returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private protected ushort GetParameter(int parameterOrdinal)
        {
            var parameterOffset = (ushort) (Registers.BP + 7 + (2 * parameterOrdinal));
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

        private static readonly char[] PrintfSpecifiers = {'c', 'd', 's', 'e', 'E', 'f', 'g', 'G', 'o', 'x', 'X', 'u', 'i', 'P', 'N', '%'};
        private static readonly char[] PrintfFlags = {'-', '+', ' ', '#', '0'};
        private static readonly char[] PrintfWidth = {'1', '2', '3', '4', '5', '6', '7', '8', '9', '0'};
        private static readonly char[] PrintfPrecision = {'.', '1', '2', '3', '4', '5', '6', '7', '8', '9', '0', '*' };
        private static readonly char[] PrintfLength = {'h', 'l', 'j', 'z', 't', 'L'};

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
                    {
                        _logger.Warn($"Invalid printf format: {Encoding.ASCII.GetString(stringToParse)}");
                        continue;
                    }

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
                    switch ((char) formatString[i])
                    {
                        case 'd':
                            var numberValueDestinationPointer = GetParameterPointer(startingParameterOrdinal);
                            startingParameterOrdinal += 2;
                            var numberValue = short.Parse(stringValues[valueOrdinal++]);
                            Module.Memory.SetWord(numberValueDestinationPointer, (ushort)numberValue);
                            continue;
                        case 's':
                            var stringValueDestinationPointer = GetParameterPointer(startingParameterOrdinal);
                            startingParameterOrdinal += 2;
                            var stringValue = stringValues[valueOrdinal++] + "\0";
                            Module.Memory.SetArray(stringValueDestinationPointer, Encoding.ASCII.GetBytes(stringValue));
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

        private protected ReadOnlySpan<byte> ProcessTextVariables(ReadOnlySpan<byte> outputBuffer)
        {
            using var newOutputBuffer = new MemoryStream(outputBuffer.Length * 2);
            for (var i = 0; i < outputBuffer.Length; i++)
            {
                if (outputBuffer[i] != 0x1)
                {
                    newOutputBuffer.WriteByte(outputBuffer[i]);
                    continue;
                }

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

        /// <summary>
        ///     This routine tried multiple cases for the path and filename in an
        ///     attempt to locate the file if the file system is case sensitive.
        /// </summary>
        /// <param name="path"></param>
        /// <param name="fileName"></param>
        /// <returns></returns>
        private protected string CheckFileCasing(string path, string fileName)
        {
            //Duh
            if (File.Exists($"{path}{fileName}"))
                return fileName;

            if (File.Exists($"{path}{fileName.ToUpper()}"))
                return fileName.ToUpper();

            if (File.Exists($"{path}{fileName.ToLower()}"))
                return fileName.ToLower();

            if (fileName.Contains(@"/") || fileName.Contains(@"\"))
            {
                string[] fileNameElements = new []{ string.Empty};
                var directorySpecifier = string.Empty;
                if (fileName.Contains('/'))
                {
                    fileNameElements = fileName.Split('/');
                    directorySpecifier = "/";
                }

                if (fileName.Contains(@"\"))
                {
                    fileNameElements = fileName.Split(@"\");
                    directorySpecifier = @"\";
                }

                //We only support 1 directory deep.. for now
                if (fileNameElements.Length > 2 || fileNameElements.Length == 0)
                    return fileName;

                fileNameElements[0] = fileNameElements[0].ToUpper();
                fileNameElements[1] = fileNameElements[1].ToUpper();
                if (File.Exists($"{path}{fileNameElements[0]}{directorySpecifier}{fileNameElements[1]}"))
                    return string.Join(directorySpecifier, fileNameElements);

                fileNameElements[0] = fileNameElements[0].ToLower();
                fileNameElements[1] = fileNameElements[1].ToUpper();
                if (File.Exists($"{path}{fileNameElements[0]}{directorySpecifier}{fileNameElements[1]}"))
                    return string.Join(directorySpecifier, fileNameElements);

                fileNameElements[0] = fileNameElements[0].ToUpper();
                fileNameElements[1] = fileNameElements[1].ToLower();
                if (File.Exists($"{path}{fileNameElements[0]}{directorySpecifier}{fileNameElements[1]}"))
                    return string.Join(directorySpecifier, fileNameElements);

                fileNameElements[0] = fileNameElements[0].ToLower();
                fileNameElements[1] = fileNameElements[1].ToLower();
                if (File.Exists($"{path}{fileNameElements[0]}{directorySpecifier}{fileNameElements[1]}"))
                    return string.Join(directorySpecifier, fileNameElements);
            }

            _logger.Warn("Unable to locate file attempting multiple cases");

            return fileName;
        }
    }
}