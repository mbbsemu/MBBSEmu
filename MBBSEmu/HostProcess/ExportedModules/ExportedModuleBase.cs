using MBBSEmu.Btrieve;
using MBBSEmu.CPU;
using MBBSEmu.Date;
using MBBSEmu.HostProcess.Structs;
using MBBSEmu.IO;
using MBBSEmu.Memory;
using MBBSEmu.Module;
using MBBSEmu.Session;
using MBBSEmu.TextVariables;
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
    public abstract class ExportedModuleBase : IDisposable
    {
        /// <summary>
        ///     The return value from the GetLeadingNumberFromString methods
        /// </summary>
        protected class LeadingNumberFromStringResult
        {
            /// <summary>Whether the integer parsing succeeded</summary>
            public bool Valid { get; set; }
            /// <summary>The integer valued parsed. Only valid is Valid is true, otherwise 0.</summary>
            public int Value { get; set; }
            /// <summary>The raw string that was parsed.</summary>
            public string StringValue { get; set; }
            /// <summary>True to indicate there is more input following what was parsed</summary>
            public bool MoreInput { get; set; }

            public LeadingNumberFromStringResult()
            {
                Valid = false;
                Value = 0;
                StringValue = "";
                MoreInput = false;
            }
        }

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
        public readonly PointerDictionary<FileStream> FilePointerDictionary;
        public readonly PointerDictionary<McvFile> McvPointerDictionary;

        private protected readonly ILogger _logger;
        private protected readonly IClock _clock;
        private protected readonly AppSettings _configuration;
        private protected readonly IFileUtility _fileFinder;
        private protected readonly IGlobalCache _globalCache;
        private protected readonly ITextVariableService _textVariableService;
        public CpuRegisters Registers;

        public MbbsModule Module;

        /// <summary>
        ///     Specifies Module DLL being invoked from, if multiple are present
        /// </summary>
        public ushort ModuleDll;

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
        private protected const ushort GENBB_BASE_SEGMENT = 0x3000;
        private protected const ushort ACCBB_BASE_SEGMENT = 0x3001;
        private protected const ushort MaxTextVariables = 64;

        public void Dispose()
        {
            foreach (var f in FilePointerDictionary)
            {
                f.Value.Close();
                _logger.Warn($"({Module.ModuleIdentifier}) WARNING -- File: {f.Value.Name} left open by module, closing");
            }
            FilePointerDictionary.Clear();
        }

        private protected ExportedModuleBase(IClock clock, ILogger logger, AppSettings configuration, IFileUtility fileUtility, IGlobalCache globalCache, MbbsModule module, PointerDictionary<SessionBase> channelDictionary, ITextVariableService textVariableService)
        {
            _clock = clock;
            _logger = logger;
            _configuration = configuration;
            _fileFinder = fileUtility;
            _globalCache = globalCache;
            _textVariableService = textVariableService;

            Module = module;
            ChannelDictionary = channelDictionary;

            FilePointerDictionary = new PointerDictionary<FileStream>(1, int.MaxValue);
            McvPointerDictionary = new PointerDictionary<McvFile>();

        }

        /// <summary>
        ///     Sets the parameter by ordinal passed into the routine
        /// </summary>
        /// <param name="parameterOrdinal"></param>
        /// <param name="value"></param>
        /// <returns></returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private protected void SetParameter(int parameterOrdinal, ushort value)
        {
            Module.Memory.SetWord(Registers.SS, GetParameterOffset(parameterOrdinal), value);
        }

        /// <summary>
        ///     Sets the parameter pointer by ordinal passed into the routine
        /// </summary>
        /// <param name="parameterOrdinal"></param>
        /// <param name="value"></param>
        /// <returns></returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private protected void SetParameterPointer(int parameterOrdinal, FarPtr value)
        {
            SetParameter(parameterOrdinal, value.Offset);
            SetParameter(parameterOrdinal + 1, value.Segment);
        }

        /// <summary>
        ///     Gets the parameter by ordinal passed into the routine
        /// </summary>
        /// <param name="parameterOrdinal"></param>
        /// <returns></returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private protected ushort GetParameter(int parameterOrdinal)
        {
            return Module.Memory.GetWord(Registers.SS, GetParameterOffset(parameterOrdinal));
        }

        /// <summary>
        ///     Gets the boolean parameter by ordinal passed into the routine
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private protected bool GetParameterBool(int parameterOrdinal) => GetParameter(parameterOrdinal) != 0;

        /// <summary>
        ///     Gets the parameter pointer by ordinal passed into the routine
        /// </summary>
        /// <param name="parameterOrdinal"></param>
        /// <returns></returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private protected FarPtr GetParameterPointer(int parameterOrdinal)
        {
            return new FarPtr(GetParameter(parameterOrdinal + 1), GetParameter(parameterOrdinal));
        }

        /// <summary>
        ///     Gets a long Parameter
        /// </summary>
        /// <param name="parameterOrdinal"></param>
        /// <returns></returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private protected int GetParameterLong(int parameterOrdinal)
        {
            return GetParameter(parameterOrdinal) | (GetParameter(parameterOrdinal + 1) << 16);
        }

        /// <summary>
        ///     Gets a Unsigned Long Parameter
        /// </summary>
        /// <param name="parameterOrdinal"></param>
        /// <returns></returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private protected uint GetParameterULong(int parameterOrdinal)
        {
            return (uint)(GetParameter(parameterOrdinal) | (GetParameter(parameterOrdinal + 1) << 16));
        }

        /// <summary>
        ///     Gets a Floating Point Double (64bit) Parameter
        /// </summary>
        /// <param name="parameterOrdinal"></param>
        /// <returns></returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private protected double GetParameterDouble(int parameterOrdinal)
        {
            return BitConverter.ToDouble(Module.Memory.GetArray(Registers.SS, GetParameterOffset(parameterOrdinal), 8));
        }

        /// <summary>
        ///     Returns the Offset in the Stack of the Specified Parameter
        /// </summary>
        /// <param name="parameterOrdinal"></param>
        /// <returns></returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private protected ushort GetParameterOffset(int parameterOrdinal) =>
            (ushort)(Registers.BP + 6 + (2 * parameterOrdinal));

        /// <summary>
        ///     Gets a string Parameter
        /// </summary>
        /// <param name="parameterOrdinal"></param>
        /// <returns></returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private protected string GetParameterString(int parameterOrdinal, bool stripNull = false)
        {
            var stringPointer = GetParameterPointer(parameterOrdinal);
            return Encoding.ASCII.GetString(Module.Memory.GetString(stringPointer, stripNull));
        }

        /// <summary>
        ///     Gets a string Parameter as a string span
        /// </summary>
        /// <param name="parameterOrdinal"></param>
        /// <returns></returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private protected ReadOnlySpan<byte> GetParameterStringSpan(int parameterOrdinal, bool stripNull = false)
        {
            var stringPointer = GetParameterPointer(parameterOrdinal);
            return Module.Memory.GetString(stringPointer, stripNull);
        }

        /// <summary>
        ///     Gets a Filename Parameter
        /// </summary>
        /// <param name="parameterOrdinal"></param>
        /// <returns>The filename parameter, upper-cased like DOS expects.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
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
            if (stringToParse.Length == 1 && stringToParse[0] == 0x0)
            {
                _logger.Debug($"({Module.ModuleIdentifier}) Empty Formatter (vsprintf:{isVsPrintf})");
                return new byte[] {0};
            }

            using var msOutput = new MemoryStream(stringToParse.Length);
            var currentParameter = startingParameterOrdinal;

            var vsPrintfBase = new FarPtr();
            if (isVsPrintf)
            {
                vsPrintfBase = GetParameterPointer(currentParameter);
                currentParameter += 2;
            }

            stringToParse = ProcessEscapeCharacters(stringToParse);

            for (var i = 0; i < stringToParse.Length; i++)
            {
                var controlStart = i;

                //Handle escaped %% as a single % -- or if % is the last character in a string
                if (stringToParse[i] == '%')
                {
                    switch ((char)stringToParse[i + 1])
                    {
                        case ' ': //Single % followed by space
                            msOutput.WriteByte(stringToParse[i]);
                            continue;

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
                        if (Module.ProtectedMemory.HasSegment(parameterSegment))
                        {
                            msOutput.Write(Module.Memory.GetString(parameterSegment, parameterOffset));
                        }
                        else
                        {
                            msOutput.Write(Encoding.ASCII.GetBytes("Invalid Pointer"));
                            _logger.Error($"({Module.ModuleIdentifier}) Invalid Pointer: {parameterSegment:X4}:{parameterOffset:X4}");
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

                    if (stringPrecision == -1)
                    {

                        if (!isVsPrintf)
                        {
                            //printf
                            stringPrecision = GetParameter(currentParameter++);
                        }
                        else
                        {
                            //vsprintf
                            stringPrecision = Module.Memory.GetWord(vsPrintfBase.Segment, vsPrintfBase.Offset);
                            vsPrintfBase.Offset += 2;
                        }
                    }

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
                                throw new Exception($"({Module.ModuleIdentifier}) Unsupported printf Length Specified");
                        }

                        i++;
                    }

                    //Finally i should be at the specifier
                    if (!InSpan(PRINTF_SPECIFIERS, stringToParse.Slice(i, 1)))
                    {
                        _logger.Debug($"({Module.ModuleIdentifier}) Invalid printf format: {Encoding.ASCII.GetString(stringToParse)}");
                        continue;
                    }

                    var padCharacter = ' ';

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

                                    if (Module.ProtectedMemory.HasSegment(stringPointer.Segment))
                                    {
                                        parameter = Module.Memory.GetString(stringPointer);
                                        vsPrintfBase.Offset += 4;
                                    }
                                    else
                                    {
                                        parameter = Encoding.ASCII.GetBytes("Invalid Pointer");
                                        _logger.Error($"({Module.ModuleIdentifier}) Invalid Pointer: {stringPointer}");
                                    }
                                }
                                else
                                {
                                    var parameterOffset = GetParameter(currentParameter++);
                                    var parameterSegment = GetParameter(currentParameter++);
                                    if (Module.ProtectedMemory.HasSegment(parameterSegment))
                                    {
                                        parameter = Module.Memory.GetString(parameterSegment, parameterOffset);
                                    }
                                    else
                                    {
                                        parameter = Encoding.ASCII.GetBytes("Invalid Pointer");
                                        _logger.Error($"({Module.ModuleIdentifier}) Invalid Pointer: {parameterSegment:X4}:{parameterOffset:X4}");
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
                        case 'u':
                            {
                                if (stringPrecision > 0)
                                {
                                    padCharacter = '0';
                                    // can't left justify a digit with 0 since it changes the digit
                                    stringFlags &= ~EnumPrintfFlags.LeftJustify;
                                }

                                long value;
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
                                                value = (uint)longHigh << 16 | (uint)longLow;
                                                if (stringToParse[i] != 'u')
                                                    value = (int)value;
                                                break;
                                            }
                                        case 0:
                                        default:
                                            {
                                                value = Module.Memory.GetWord(vsPrintfBase);
                                                if (stringToParse[i] != 'u')
                                                    value = (short)value;
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
                                                value = (uint)longHigh << 16 | (uint)longLow;
                                                if (stringToParse[i] != 'u')
                                                    value = (int)value;
                                                break;
                                            }
                                        case 0:
                                        default:
                                            value = GetParameter(currentParameter++);
                                            if (stringToParse[i] != 'u')
                                                value = (short)value;
                                            break;
                                    }
                                }

                                msFormattedValue.Write(Encoding.ASCII.GetBytes(value.ToString()));
                                break;
                            }
                        case 'f':
                            {
                                var floatValue = new byte[8];
                                if (isVsPrintf)
                                {
                                    floatValue = Module.Memory.GetArray(vsPrintfBase.Segment, vsPrintfBase.Offset, 8)
                                        .ToArray();
                                    vsPrintfBase.Offset += 8;
                                }
                                else
                                {
                                    var parameterValue = GetParameterOffset(currentParameter);
                                    currentParameter += 4;
                                    floatValue = Module.Memory.GetArray(Registers.SS, parameterValue, 8).ToArray();
                                }

                                msFormattedValue.Write(
                                    Encoding.ASCII.GetBytes(((float)BitConverter.ToDouble(floatValue)).ToString()));

                                break;
                            }
                        default:
                        {
                            _logger.Warn($"({Module.ModuleIdentifier}) Unhandled Printf Control Character: {(char) stringToParse[i + 1]}");
                            msOutput.Write(stringToParse.Slice(controlStart, (i - controlStart) + 1));
                            continue;
                        }
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
                                    msFormattedValue.WriteByte((byte)padCharacter);
                            }
                            else
                            {
                                //Pad beginning
                                var valueCache = msFormattedValue.ToArray();
                                msFormattedValue.SetLength(0);
                                while (msFormattedValue.Length < stringWidth - valueCache.Length)
                                    msFormattedValue.WriteByte((byte)padCharacter);

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

            _logger.Warn($"({Module.ModuleIdentifier}) Unable to find String terminator");
            return inputArray;
        }


        /// <summary>
        ///     Handles processing of text variables registered with REGISTER_VARIABLE() within an outprf string if they're present
        /// </summary>
        /// <param name="outputBuffer"></param>
        /// <returns></returns>
        private protected ReadOnlySpan<byte> ProcessTextVariables(ReadOnlySpan<byte> outputBuffer)
        {
            //Bypass if no variable if even found
            if (!_textVariableService.HasVariable(outputBuffer))
                return outputBuffer;

            var txtvarsFound = _textVariableService.ExtractVariableDefinitions(outputBuffer);

            var txtvarDictionary = ChannelDictionary[ChannelNumber].SessionVariables;

            //Get Their Values
            var txtvarMemoryBase = Module.Memory.GetVariablePointer("TXTVARS");
            foreach (var txtvar in txtvarsFound)
            {
                //If we've already loaded it, keep going
                if (txtvarDictionary.ContainsKey(txtvar.Name))
                    continue;

                for (ushort j = 0; j < MaxTextVariables; j++)
                {
                    var currentTextVar =
                        new TextvarStruct(Module.Memory.GetArray(txtvarMemoryBase + (j * TextvarStruct.Size),
                            TextvarStruct.Size));

                    if (currentTextVar.name != txtvar.Name)
                        continue;

                    var resultRegisters = Module.Execute(currentTextVar.varrou, ChannelNumber, true, true, null, 0xF100);
                    var variableData = Module.Memory.GetString(resultRegisters.DX, resultRegisters.AX, true);
#if DEBUG
                    _logger.Debug(($"({Module.ModuleIdentifier}) Processing Text Variable {txtvar} ({currentTextVar.varrou}): {BitConverter.ToString(variableData.ToArray()).Replace("-", " ")}"));
#endif

                    txtvarDictionary[txtvar.Name] = Encoding.ASCII.GetString(variableData).ToString;
                }
            }

            return _textVariableService.Parse(outputBuffer, txtvarDictionary);
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
            using var resultStream = new MemoryStream(inputSpan.Length);
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
            var previousBP = Module.Memory.GetWord(Registers.SS, Registers.BP);
            var previousIP = Module.Memory.GetWord(Registers.SS, (ushort)(Registers.BP + 2));
            var previousCS = Module.Memory.GetWord(Registers.SS, (ushort)(Registers.BP + 4));

            //Set stack back to entry state, minus parameters
            Registers.SP += (ushort)(6 + bytesToRealign); //6 bytes for the BP, IP, SP in addition to variables passed in
            Registers.SP -= 2;
            Module.Memory.SetWord(Registers.SS, Registers.SP, previousCS);
            Registers.SP -= 2;
            Module.Memory.SetWord(Registers.SS, Registers.SP, previousIP);
            Registers.SP -= 2;
            Module.Memory.SetWord(Registers.SS, Registers.SP, previousBP);
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
            using var result = new MemoryStream(stringToFormat.Length + 32);
            for (var i = 0; i < stringToFormat.Length; i++)
            {
                var c = stringToFormat[i];
                switch ((char)c)
                {
                    case '\n':
                    case '\r': //carriage return on the input string is handled as a \r\n
                        {
                            result.Write(NEW_LINE); //new line

                            //Verify we're not at the end of the string
                            if (i + 1 < stringToFormat.Length)
                            {
                                var nextCharacter = stringToFormat[i + 1];

                                //If the character sequence was already \r\n (or reverse), then skip the next character
                                //as to not write double new lines
                                if ((c == '\r' && nextCharacter == '\n') ||
                                    (c == '\n' && nextCharacter == '\r'))
                                    i++;
                            }

                            continue;
                        }
                }

                result.WriteByte(c);
            }

            return result.ToArray();
        }

        protected enum CharacterAccepterResponse
        {
            ABORT,
            SKIP,
            ACCEPT,
        }
        protected delegate CharacterAccepterResponse CharacterAccepter(char c);

        /// <summary>
        ///     Consumes all whitespace from input and moves to the first non-whitespace character.
        /// </summary>
        /// <return>boolean for whether there is more input remaining, and an integer for the count
        ///      of characters consumed/skipped</return>
        protected (bool, int) ConsumeWhitespace(IEnumerator<char> input)
        {
            var count = 0;
            do
            {
                if (!char.IsWhiteSpace(input.Current))
                    return (true, count);

                ++count;
            } while (input.MoveNext());

            return (false, count);
        }

        /// <summary>
        ///     Reads a string from input, validating against accepter. Skips beginning whitespace.
        /// </summary>
        /// <return>The string, and a boolean indicating whether there is more input to be read.</return>
        protected (string, bool) ReadString(IEnumerator<char> input, CharacterAccepter accepter)
        {
            var builder = new StringBuilder();

            var (moreInput, _) = ConsumeWhitespace(input);
            if (!moreInput)
                return ("", false);

            do
            {
                if (char.IsWhiteSpace(input.Current))
                    return (builder.ToString(), true);

                var response = accepter(input.Current);
                if (response == CharacterAccepterResponse.ABORT)
                    return (builder.ToString(), true);

                if (response == CharacterAccepterResponse.ACCEPT)
                    builder.Append(input.Current);
            } while (input.MoveNext());

            // end of input
            return (builder.ToString(), false);
        }

        /// <summary>
        ///     Many C++ methods such as ATOL(), SSCANF(), etc. are real forgiving in their parsing of strings to numbers,
        ///     where a string "123test" should be converted to 123.
        ///
        ///     This method extracts the valid number (if any) from the given string
        /// </summary>
        /// <param name="input">Input IEnumerator, assumes MoveNext has already been called</param>
        private protected LeadingNumberFromStringResult GetLeadingNumberFromString(IEnumerator<char> input)
        {
            var result = new LeadingNumberFromStringResult();
            var count = 0;

            (result.StringValue, result.MoreInput) = ReadString(input, c =>
            {
                if (count >= 11)
                    return CharacterAccepterResponse.ABORT;

                var first = (count++ == 0);
                if (first && c == '+')
                    return CharacterAccepterResponse.SKIP;
                if ((first && c == '-') || char.IsDigit(c))
                    return CharacterAccepterResponse.ACCEPT;

                return CharacterAccepterResponse.ABORT;
            });

            result.Valid = int.TryParse(result.StringValue, out var value);
            if (result.Valid)
                result.Value = value;

            return result;
        }

        /// <summary>
        ///     Many C++ methods such as ATOL(), SSCANF(), etc. are real forgiving in their parsing of strings to numbers,
        ///     where a string "123test" should be converted to 123.
        ///
        ///     This method extracts the valid number (if any) from the given string
        /// </summary>
        /// <param name="inputString">Input string containers integer values</param>
        /// <returns></returns>
        private protected LeadingNumberFromStringResult GetLeadingNumberFromString(string inputString)
        {
            var enumerator = inputString.GetEnumerator();
            return enumerator.MoveNext() ? GetLeadingNumberFromString(enumerator) : new LeadingNumberFromStringResult();
        }

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
        private protected void BtrieveSaveProcessor(FarPtr btrievePointer, BtrieveFileProcessor btrieveFileProcessor) =>
            _globalCache.Set(BtrieveCacheKey(btrievePointer),
                btrieveFileProcessor);

        /// <summary>
        ///     Gets a BtrieveFileProcessor for the given pointer to the Global Cache
        /// </summary>
        /// <param name="btrievePointer"></param>
        /// <returns></returns>
        private protected BtrieveFileProcessor BtrieveGetProcessor(FarPtr btrievePointer) =>
            _globalCache.Get<BtrieveFileProcessor>(BtrieveCacheKey(btrievePointer));

        private protected bool BtrieveDeleteProcessor(FarPtr btrievePointer)
        {
            var key = BtrieveCacheKey(btrievePointer);

            if (_globalCache.TryGet<BtrieveFileProcessor>(key, out var processor))
            {
                processor.Dispose();
                return _globalCache.Remove(key);
            }

            return false;
        }

        /// <summary>
        ///     Generates a Unique Key to be used for saving a BtrieveFileProcessor mapped to its pointer
        ///     Unique to the module.
        /// </summary>
        /// <param name="btrievePointer"></param>
        /// <returns></returns>
        private string BtrieveCacheKey(FarPtr btrievePointer) =>
            $"{Module.ModuleIdentifier}-Btrieve-{btrievePointer}";

        /// <summary>
        ///     Sets up a Global Btrieve Pointer for Btrieve files that need to share context between multiple modules
        ///     This is mainly used for system Btrieve files, such as GENBB, ACCBB, etc.
        /// </summary>
        /// <param name="variableName">Variable Name to use for Key (Example: "GENBB")</param>
        /// <param name="fileName">Btrieve Filename to be opened (Example: "BBSGEN.DAT")</param>
        /// <param name="baseSegment">Dedicated Memory Segment in the local Module for this Global Btrieve Struct</param>
        private protected void BtrieveSetupGlobalPointer(string variableName, string fileName, ushort baseSegment)
        {
            //Construct Pointer for Btrieve Struct and Name/Data pointers
            var btrievePointer = new FarPtr(baseSegment, 0x0); //Btrieve Struct
            var btrieveNamePointer = new FarPtr(baseSegment, 0x100); //File Name Pointer
            var btrieveDataPointer = new FarPtr(baseSegment, 0x200); //Record Data Pointer

            //Some Btrieve Processors can be declared elsewhere in the system, so verify the processor doesn't already exist before creating
            if (!_globalCache.ContainsKey($"{variableName}-PROCESSOR"))
                _globalCache.Set($"{variableName}-PROCESSOR", new BtrieveFileProcessor(_fileFinder, Directory.GetCurrentDirectory(), fileName, _configuration.BtrieveCacheSize));

            //Setup the Pointer to the Global Address -- ensuring each module is referencing the same Pointer & Processor
            if (!_globalCache.ContainsKey($"{variableName}-POINTER"))
                _globalCache.Set($"{variableName}-POINTER", btrievePointer);

            //If the Module doesn't already have this Global Btrieve Pointer setup in memory, set it up
            if (!Module.ProtectedMemory.HasSegment(baseSegment))
            {
                //Declare Pointers and Locations for Struct Data
                Module.ProtectedMemory.AddSegment(baseSegment);

                //Set Struct Value
                var newBtvStruct = new BtvFileStruct { filenam = btrieveNamePointer, reclen = 8192, data = btrieveDataPointer };
                Module.Memory.SetArray(btrievePointer, newBtvStruct.Data);

                //Set Filename Value
                Module.Memory.SetArray(btrieveNamePointer, Encoding.ASCII.GetBytes($"{fileName}\0"));
            }

            //If we've already setup the local reference, bail
            if (Module.Memory.TryGetVariablePointer(variableName, out _)) return;

            //Save a local reference to the shared Processor
            BtrieveSaveProcessor(btrievePointer, _globalCache.Get<BtrieveFileProcessor>($"{variableName}-PROCESSOR"));

            //Local Variable that will hold the pointer to the GENBB-POINTER
            var localPointer = Module.Memory.GetOrAllocateVariablePointer(variableName, FarPtr.Size);
            Module.Memory.SetPointer(localPointer, btrievePointer);
        }

    }
}
