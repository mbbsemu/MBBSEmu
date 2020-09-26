using MBBSEmu.Btrieve;
using MBBSEmu.CPU;
using MBBSEmu.Extensions;
using MBBSEmu.IO;
using MBBSEmu.Memory;
using MBBSEmu.Module;
using MBBSEmu.Session;
using Microsoft.Extensions.Configuration;
using NLog;
using System;
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
        public const ushort OUTBUF_SIZE = 8192;

        /// <summary>
        ///     Internal Variables are stored inside the system (module name, etc.)
        ///
        ///     We want to write these only once to the HostMemorySegment, so we use this
        ///     dictionary to track the variable by a given name (key) and the pointer to the segment
        ///     it lives in.
        /// </summary>
        private protected PointerDictionary<SessionBase> ChannelDictionary;


        /// <summary>
        ///     Pointers to files opened using FOPEN
        /// </summary>
        private protected readonly PointerDictionary<FileStream> FilePointerDictionary;
        public readonly PointerDictionary<McvFile> McvPointerDictionary;

        private protected readonly ILogger _logger;
        private protected readonly IConfiguration _configuration;
        private protected readonly IFileUtility _fileFinder;
        private protected readonly IGlobalCache _globalCache;

        public CpuRegisters Registers;

        public MbbsModule Module;

        /// <summary>
        ///     Current Channel Number being serviced
        /// </summary>
        private protected ushort ChannelNumber;

        //Constants
        private protected static readonly char[] SSCANF_SEPARATORS = { ' ', ',', '\r', '\n', '\0', ':' };
        private protected static readonly char[] PRINTF_SPECIFIERS = { 'c', 'd', 's', 'e', 'E', 'f', 'g', 'G', 'o', 'x', 'X', 'u', 'i', 'P', 'N', '%' };
        private protected static readonly char[] PRINTF_FLAGS = { '-', '+', ' ', '#', '0' };
        private protected static readonly char[] PRINTF_WIDTH = { '1', '2', '3', '4', '5', '6', '7', '8', '9', '0', '*' };
        private protected static readonly char[] PRINTF_PRECISION = { '.', '1', '2', '3', '4', '5', '6', '7', '8', '9', '0', '*' };
        private protected static readonly char[] PRINTF_LENGTH = { 'h', 'l', 'j', 'z', 't', 'L' };
        private protected static readonly byte[] NEW_LINE = { (byte)'\r', (byte)'\n' }; //Just easier to read

        private protected ExportedModuleBase(ILogger logger, IConfiguration configuration, IFileUtility fileUtility, IGlobalCache globalCache, MbbsModule module, PointerDictionary<SessionBase> channelDictionary)
        {
            _logger = logger;
            _configuration = configuration;
            _fileFinder = fileUtility;
            _globalCache = globalCache;

            Module = module;
            ChannelDictionary = channelDictionary;

            FilePointerDictionary = new PointerDictionary<FileStream>(1, int.MaxValue);
            McvPointerDictionary = new PointerDictionary<McvFile>();

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

        /// <summary>
        ///     Gets a string Parameter
        /// </summary>
        /// <param name="parameterOrdinal"></param>
        /// <returns></returns>
        private protected string GetParameterString(int parameterOrdinal, bool stripNull = false)
        {
            var filenamePointer = GetParameterPointer(parameterOrdinal);
            return Encoding.ASCII.GetString(Module.Memory.GetString(filenamePointer, stripNull));
        }

        /// <summary>
        ///     Gets a Filename Parameter
        /// </summary>
        /// <param name="parameterOrdinal"></param>
        /// <returns>The filename parameter, uppercased like DOS expects.</returns>
        private protected string GetParameterFilename(int parameterOrdinal)
        {
            return GetParameterString(parameterOrdinal, true).ToUpper();
        }

        private static bool InSpan(ReadOnlySpan<char> spanToSearch, ReadOnlySpan<byte> character)
        {
            foreach (var c in spanToSearch)
            {
                if (c == character[0])
                    return true;
            }

            return false;
        }

        private static bool IsPrintfPrecision(ReadOnlySpan<byte> c) => c[0] == PRINTF_PRECISION[0];

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
                vsPrintfBase = GetParameterPointer(currentParameter);
                currentParameter += 2;
            }

            stringToParse = ProcessEscapeCharacters(stringToParse);

            for (var i = 0; i < stringToParse.Length; i++)
            {
                //Handle escaped %% as a single % -- or if % is the last character in a string
                if (stringToParse[i] == '%')
                {
                    switch ((char)stringToParse[i + 1])
                    {
                        case '%': //escaped %
                            msOutput.WriteByte((byte)'%');
                            i++;
                            continue;
                        case '\0': //last character is an invalid single %, just print it and move on
                            msOutput.WriteByte((byte)'%');
                            msOutput.WriteByte(0);
                            i++;
                            continue;
                    }

                }

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
                    while (InSpan(PRINTF_FLAGS, stringToParse.Slice(i, 1)))
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
                    while (InSpan(PRINTF_WIDTH, stringToParse.Slice(i, 1)))
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
                    while (InSpan(PRINTF_PRECISION, stringToParse.Slice(i, 1)))
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
                    var variableLength = 0;
                    while (InSpan(PRINTF_LENGTH, stringToParse.Slice(i, 1)))
                    {
                        switch (stringToParse[i])
                        {
                            case (byte)'l':
                                variableLength = 4;
                                break;
                            default:
                                throw new Exception("Unsupported printf Length Specified");
                        }

                        i++;
                    }

                    //Finally i should be at the specifier
                    if (!InSpan(PRINTF_SPECIFIERS, stringToParse.Slice(i, 1)))
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
                                if (isVsPrintf)
                                {
                                    charParameter = Module.Memory.GetByte(vsPrintfBase.Segment, vsPrintfBase.Offset);
                                    vsPrintfBase.Offset += 2;
                                }
                                else
                                {
                                    charParameter = (byte)GetParameter(currentParameter++);
                                }

                                msFormattedValue.WriteByte(charParameter);
                                break;

                            }
                        //String of characters
                        case 's':
                            {
                                ReadOnlySpan<byte> parameter;
                                if (isVsPrintf)
                                {
                                    var stringPointer = Module.Memory.GetPointer(vsPrintfBase);
                                    parameter = Module.Memory.GetString(stringPointer);
                                    vsPrintfBase.Offset += 4;
                                }
                                else
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

                                if (parameter[^1] == 0x0)
                                    parameter = parameter.Slice(0, parameter.Length - 1);

                                msFormattedValue.Write(parameter);
                                break;
                            }
                        //Signed decimal integer
                        case 'i':
                        case 'd':
                            {
                                if (isVsPrintf)
                                {
                                    switch (variableLength)
                                    {
                                        case 4:
                                            {
                                                var longLow = Module.Memory.GetWord(vsPrintfBase);
                                                vsPrintfBase.Offset += 2;
                                                var longHigh = Module.Memory.GetWord(vsPrintfBase);
                                                vsPrintfBase.Offset += 2;
                                                var longIntParameter = longHigh << 16 | longLow;
                                                msFormattedValue.Write(
                                                    Encoding.ASCII.GetBytes(longIntParameter.ToString()));
                                                break;
                                            }
                                        case 0:
                                        default:
                                            {
                                                var parameterString =
                                                    ((short)Module.Memory.GetWord(vsPrintfBase)).ToString();
                                                msFormattedValue.Write(Encoding.ASCII.GetBytes(parameterString));
                                                vsPrintfBase.Offset += 2;
                                                break;
                                            }
                                    }

                                }
                                else
                                {
                                    switch (variableLength)
                                    {
                                        //ld or li (long int)
                                        case 4:
                                            {
                                                var longLow = GetParameter(currentParameter++);
                                                var longHigh = GetParameter(currentParameter++);
                                                var longIntParameter = longHigh << 16 | longLow;
                                                msFormattedValue.Write(
                                                    Encoding.ASCII.GetBytes(longIntParameter.ToString()));
                                                break;
                                            }
                                        case 0:
                                        default:
                                            var parameter = (short)GetParameter(currentParameter++);
                                            msFormattedValue.Write(Encoding.ASCII.GetBytes(parameter.ToString()));
                                            break;
                                    }


                                }

                                break;
                            }
                        //Unsigned decimal integer
                        case 'u':
                            {
                                if (isVsPrintf)
                                {
                                    var parameterString = Module.Memory.GetWord(vsPrintfBase)
                                        .ToString();
                                    msFormattedValue.Write(Encoding.ASCII.GetBytes(parameterString));
                                    vsPrintfBase.Offset += 2;
                                }
                                else
                                {
                                    var parameter = GetParameter(currentParameter++);
                                    msFormattedValue.Write(Encoding.ASCII.GetBytes(parameter.ToString()));
                                }

                                break;
                            }
                        case 'f':
                            {
                                var floatValue = new byte[4];
                                if (isVsPrintf)
                                {
                                    floatValue = Module.Memory.GetArray(vsPrintfBase.Segment, vsPrintfBase.Offset, 4)
                                        .ToArray();
                                    vsPrintfBase.Offset += 4;
                                }
                                else
                                {
                                    var parameterValue = GetParameterULong(currentParameter++);
                                    currentParameter++;
                                    Array.Copy(BitConverter.GetBytes(parameterValue), 0, floatValue, 0, 4);
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
        ///     Handles processing of text variables registered with REGISTER_VARIABLE() within an outprf string if they're present
        /// </summary>
        /// <param name="outputBuffer"></param>
        /// <returns></returns>
        private protected ReadOnlySpan<byte> ProcessTextVariables(ReadOnlySpan<byte> outputBuffer)
        {
            using var newOutputBuffer = new MemoryStream(outputBuffer.Length);
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

                var variableNameStart = i;
                var variableNameLength = 0;
                //Get variable name
                while (outputBuffer[i] != 0x1)
                {
                    i++;
                    variableNameLength++;
                }

                switch (Encoding.ASCII.GetString(outputBuffer.Slice(variableNameStart, variableNameLength)))
                {
                    //Built in internal Text Variables
                    case "USERID":
                        newOutputBuffer.Write(Encoding.ASCII.GetBytes(ChannelDictionary[ChannelNumber].Username));
                        break;

                    //Registered Variables
                    case var textVariableName when Module.TextVariables.ContainsKey(textVariableName):
                        //Get Variable Entry Point
                        var variableEntryPoint = Module.TextVariables[textVariableName];
                        var resultRegisters = Module.Execute(variableEntryPoint, ChannelNumber, true, true, null, 0xF100);
                        var variableData = Module.Memory.GetString(resultRegisters.DX, resultRegisters.AX, true);
#if DEBUG
                        _logger.Info($"Processing Text Variable {textVariableName} ({variableEntryPoint}): {BitConverter.ToString(variableData.ToArray()).Replace("-", " ")}");
#endif
                        newOutputBuffer.Write(variableData);
                        break;

                    default:
                        _logger.Error($"Unknown Text Variable: {Encoding.ASCII.GetString(outputBuffer.Slice(variableNameStart, variableNameLength))}");
                        break;
                }
            }

            return newOutputBuffer.ToArray();
        }

        /// <summary>
        ///     Handles replacing C++ escape characters within the specified string
        /// </summary>
        /// <param name="inputSpan"></param>
        /// <returns></returns>
        public ReadOnlySpan<byte> ProcessEscapeCharacters(ReadOnlySpan<byte> inputSpan)
        {
            using var resultStream = new MemoryStream(inputSpan.Length);
            for (var i = 0; i < inputSpan.Length; i++)
            {
                if (inputSpan[i] != '\\')
                {
                    resultStream.WriteByte(inputSpan[i]);
                    continue;
                }

                i++;
                switch ((char)inputSpan[i])
                {
                    case 'a': //alert (bell)
                        resultStream.WriteByte(0x7);
                        continue;
                    case 'b': //backspace
                        resultStream.WriteByte(0x8);
                        continue;
                    case 't': //tab
                        resultStream.WriteByte(0x9);
                        continue;
                    case 'n': //newline
                        resultStream.WriteByte(0xA);
                        continue;
                    case 'v': //vertical tab
                        resultStream.WriteByte(0xB);
                        continue;
                    case 'f': //form feed
                        resultStream.WriteByte(0xC);
                        continue;
                    case 'r': //carriage return
                        resultStream.WriteByte(0xD);
                        continue;
                    case '\\':
                        resultStream.WriteByte((byte)'\\');
                        continue;
                    case '"':
                        resultStream.WriteByte((byte)'"');
                        continue;
                    case '?':
                        resultStream.WriteByte((byte)'?');
                        continue;
                    case 'x': //hex character
                        {
                            resultStream.WriteByte(Convert.ToByte($"{inputSpan[i + 1]}{inputSpan[i + 2]}"));
                            i += 2;
                            continue;
                        }
                    case var n when (n >= '0' && n <= '9'):
                        {
                            var stringValue = inputSpan[i].ToString();

                            if (inputSpan[i + 1] >= '0' && inputSpan[i + 1] <= '9')
                            {
                                stringValue += inputSpan[i + 1].ToString();
                                i++;

                                if (inputSpan[i + 2] >= '0' && inputSpan[i + 2] <= '9')
                                {
                                    stringValue += inputSpan[i + 2].ToString();
                                    i++;
                                }
                            }
                            resultStream.WriteByte(byte.Parse(stringValue));

                            break;
                        }
                    default:
                        resultStream.WriteByte((byte)'\\');
                        resultStream.WriteByte(inputSpan[i]);
                        continue;
                }
            }

            return resultStream.ToArray();
        }

        /// <summary>
        ///     The GSBL BTUXMT supports custom ANSI escape sequences where strings are sent to the client
        ///     depending on if they have ANSI enabled or not. This routine parses those characters based on
        ///     the specified ANSI Support Level.
        ///
        ///     Because MBBSEmu **ONLY** supports ANSI, we ignore the non-ANSI component if an IF-ANSI sequence
        /// </summary>
        /// <param name="isAnsi"></param>
        /// <returns></returns>
        private protected ReadOnlySpan<byte> ProcessIfANSI(ReadOnlySpan<byte> inputSpan, bool isAnsi = false)
        {
            using var resultStream = new MemoryStream();
            for (var i = 0; i < inputSpan.Length; i++)
            {
                if (inputSpan[i] != 0x1B)
                {
                    resultStream.WriteByte(inputSpan[i]);

                    //Process ~~ escape
                    if (inputSpan[i] == '~' && inputSpan[i + 1] == '~')
                        i++;

                    continue;
                }

                //Normal ANSI
                if (inputSpan[i] == 0x1B && inputSpan[i + 1] == '[' && inputSpan[i + 2] != '[')
                {
                    resultStream.WriteByte(inputSpan[i]);
                    continue;
                }

                //Found IF-ANSI
                if (inputSpan[i] == 0x1B && inputSpan[i + 1] == '[' && inputSpan[i + 2] == '[')
                {
                    i += 3;
                    var substringStart = i;
                    var substringEnd = i; //We will increment this

                    //Find the end of the first segment
                    while (substringEnd < inputSpan.Length)
                    {
                        //Break if we've found the unescaped end
                        if (inputSpan[substringEnd] == '|' && inputSpan[substringEnd - 1] != '~')
                            break;

                        substringEnd++;
                    }

                    var substringSpan = inputSpan.Slice(substringStart, (substringEnd - substringStart));

                    //Process Escape Characters for IF-ANSI and write to output buffer
                    for (var j = 0; j < substringSpan.Length; j++)
                    {
                        switch (substringSpan[j])
                        {
                            case (byte)'|':
                            case (byte)']':
                            case (byte)'~':
                                {
                                    if (substringSpan[j - 1] == '~')
                                        resultStream.WriteByte(substringSpan[j]);

                                    break;
                                }
                            default:
                                resultStream.WriteByte(substringSpan[j]);
                                break;
                        }
                    }

                    //Set Cursor to where we're at now
                    i = substringEnd;

                    //Skip past 'else'
                    //Find the end of the first segment
                    while (i < inputSpan.Length)
                    {
                        //Break if we've found the unescaped end
                        if (inputSpan[i] == ']' && inputSpan[i - 1] != '~')
                            break;

                        i++;
                    }
                }

            }

            return resultStream.ToArray();
        }

        /// <summary>
        ///     Some external code has stack realignment internally handled, meaning after a CALL, it does the
        ///     leave & retf within it, setting the stack back to a pre-call state (no need to ADD SP, x post call)
        ///
        ///     This routine realigns the stack in a similar fashion
        /// </summary>
        /// <param name="bytesToRealign">Number of bytes pushed to stack prior to CALL</param>
        private protected void RealignStack(ushort bytesToRealign)
        {
            //Get Previous State Values off the Stack
            var previousBP = Module.Memory.GetWord(Registers.SS, (ushort)(Registers.BP + 1));
            var previousIP = Module.Memory.GetWord(Registers.SS, (ushort)(Registers.BP + 3));
            var previousCS = Module.Memory.GetWord(Registers.SS, (ushort)(Registers.BP + 5));

            //Set stack back to entry state, minus parameters
            Registers.SP += (ushort)(bytesToRealign + 6); //6 bytes for the BP, IP, SP in addition to variables passed in
            Module.Memory.SetWord(Registers.SS, (ushort)(Registers.SP - 1), previousCS);
            Registers.SP -= 2;
            Module.Memory.SetWord(Registers.SS, (ushort)(Registers.SP - 1), previousIP);
            Registers.SP -= 2;
            Module.Memory.SetWord(Registers.SS, (ushort)(Registers.SP - 1), previousBP);
            Registers.SP -= 2;
            Registers.BP = Registers.SP;
        }

        /// <summary>
        ///     Telnet needs to handle \r as if it were \r\n, and any \n must be accompanied by a \r as well
        ///
        ///     This method will scan the specified array and generate a properly formatted output
        /// </summary>
        /// <param name="stringToFormat"></param>
        /// <returns></returns>
        private protected ReadOnlySpan<byte> FormatNewLineCarriageReturn(ReadOnlySpan<byte> stringToFormat)
        {
            using var result = new MemoryStream();
            foreach (var c in stringToFormat)
            {
                switch ((char)c)
                {
                    case '\n':
                    case '\r': //carriage return on the input string is handled as a \r\n
                        result.Write(NEW_LINE); //new line
                        continue;
                }
                result.WriteByte(c);
            }

            return result.ToArray();
        }

        /// <summary>
        ///     Many C++ methods such as ATOL(), SSCANF(), etc. are real forgiving in their parsing of strings to numbers,
        ///     where a string "123test" should be converted to 123.
        ///
        ///     This method extracts the valid number (if any) from the given string
        /// </summary>
        /// <param name="inputString"></param>
        /// <param name="success"></param>
        private protected int GetLeadingNumberFromString(ReadOnlySpan<byte> inputString, out bool success)
        {
            success = false;
            var result = 0;

            if (inputString.Length == 0 || inputString[0] == '\0')
                return result;

            var characterStart = 0;
            //Trim Leading Spaces/Tabs
            for (var i = 0; i < inputString.Length; i++)
            {
                if (inputString[i] == ' ' || inputString[i] == '\t')
                    continue;

                characterStart = i;
                break;
            }

            //Find the first string representing a numeric value in the provided input string
            for (var i = characterStart; i < inputString.Length; i++)
            {
                if (char.IsNumber((char)inputString[i]) || inputString[i] == '-' || inputString[i] == '+')
                    continue;

                if (i == 0)
                {
                    _logger.Warn($"Unable to find leading number: {Encoding.ASCII.GetString(inputString)}");
                    return 0;
                }

                success = int.TryParse(inputString.ToCharSpan().Slice(0, i), out result);

                if (!success)
                    _logger.Warn($"Unable to cast to long: {Encoding.ASCII.GetString(inputString.Slice(0, i).ToArray())}");

                return result;
            }

            //At this point, the entire string is assumed a numeric
            success = int.TryParse(inputString.ToCharSpan(), out result);

            if (!success)
                _logger.Warn($"Unable to cast to long: {Encoding.ASCII.GetString(inputString.ToArray())}");

            return result;
        }

        private protected int GetLeadingNumberFromString(string inputString, out bool success) =>
            GetLeadingNumberFromString(Encoding.ASCII.GetBytes(inputString), out success);

        /// <summary>
        ///     Handles calling functions to format bytes to be sent to the client.
        ///
        ///     They are (in order):
        ///     ProcessIfANSI() - Handles processing of IF-ANSI Sequences
        ///     FormatNewLineCarriageReturn() - Ensures any \r or \n are converted to \r\n
        ///     ProcessTextVariables() - Handles processing any Text Variables in the given string
        /// </summary>
        /// <param name="outputBytes"></param>
        /// <returns></returns>
        private protected ReadOnlySpan<byte> FormatOutput(ReadOnlySpan<byte> outputBytes)
        {
            using var output = new MemoryStream(outputBytes.Length * 2);
            output.Write(ProcessTextVariables(FormatNewLineCarriageReturn(ProcessIfANSI(outputBytes))));
            return output.ToArray();
        }

        /// <summary>
        ///     Saves a BtrieveFileProcessor for the given pointer to the Global Cache
        /// </summary>
        /// <param name="btrievePointer"></param>
        /// <param name="btrieveFileProcessor"></param>
        private protected void BtrieveSaveProcessor(IntPtr16 btrievePointer, BtrieveFileProcessor btrieveFileProcessor) =>
            _globalCache.Set(BtrieveCacheKey(btrievePointer),
                btrieveFileProcessor);

        /// <summary>
        ///     Gets a BtrieveFileProcessor for the given pointer to the Global Cache
        /// </summary>
        /// <param name="btrievePointer"></param>
        /// <returns></returns>
        private protected BtrieveFileProcessor BtrieveGetProcessor(IntPtr16 btrievePointer) =>
            _globalCache.Get<BtrieveFileProcessor>(BtrieveCacheKey(btrievePointer));

        private protected bool BtrieveDeleteProcessor(IntPtr16 btrievePointer) =>
            _globalCache.Remove(BtrieveCacheKey(btrievePointer));

        /// <summary>
        ///     Generates a Unique Key to be used for saving a BtrieveFileProcessor mapped to its pointer
        ///     Unique to the module.
        /// </summary>
        /// <param name="btrievePointer"></param>
        /// <returns></returns>
        private string BtrieveCacheKey(IntPtr16 btrievePointer) =>
            $"{Module.ModuleIdentifier}-Btrieve-{btrievePointer}";



    }
}
