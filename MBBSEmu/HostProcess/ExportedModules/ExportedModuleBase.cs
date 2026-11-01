using MBBSEmu.Btrieve;
using MBBSEmu.CPU;
using MBBSEmu.Date;
using MBBSEmu.HostProcess.Structs;
using MBBSEmu.IO;
using MBBSEmu.Logging;
using MBBSEmu.Memory;
using MBBSEmu.Module;
using MBBSEmu.Session;
using MBBSEmu.TextVariables;
using System;
using System.Collections.Generic;
using System.Diagnostics.Contracts;
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

        private protected readonly IMessageLogger _logger;
        private protected readonly IClock _clock;
        private protected readonly AppSettingsManager _configuration;
        private protected readonly IFileUtility _fileFinder;
        private protected readonly IGlobalCache _globalCache;
        private protected readonly ITextVariableService _textVariableService;
        public ICpuRegisters Registers;

        public MbbsModule Module;

        /// <summary>
        ///     Specifies Module DLL being invoked from, if multiple are present
        /// </summary>
        public ushort ModuleDll;

        /// <summary>
        ///     Current Channel Number being serviced
        /// </summary>
        private protected ushort ChannelNumber;

        /// <summary>
        ///     Dictionary that will hold the ordinals used for local variables. This allows methods that
        ///     return pointers to result values in memory (Strings, etc.) to have unique addresses so that
        ///     results aren't overwritten in the same execution cycle.
        /// </summary>
        private protected readonly Dictionary<string, int> LocalVariableOrdinalDictionary = new();

        //Constants
        private protected static readonly char[] SSCANF_SEPARATORS = { ' ', ',', '\r', '\n', '\0', ':' };
        private protected static readonly byte[] NEW_LINE = { (byte)'\r', (byte)'\n' }; //Just easier to read
        private protected const ushort GENBB_BASE_SEGMENT = 0x3000;
        private protected const ushort ACCBB_BASE_SEGMENT = 0x3001;
        private protected const ushort MaxTextVariables = 64;

        public virtual void Dispose()
        {
            foreach (var f in FilePointerDictionary)
            {
                f.Value.Dispose();
                _logger.Warn($"({Module.ModuleIdentifier}) WARNING -- File: {f.Value.Name} left open by module, closing");
            }
            FilePointerDictionary.Clear();
        }

        private protected ExportedModuleBase(IClock clock, IMessageLogger logger, AppSettingsManager configuration, IFileUtility fileUtility, IGlobalCache globalCache, MbbsModule module, PointerDictionary<SessionBase> channelDictionary, ITextVariableService textVariableService)
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
        ///     Resets Local Variable Ordinals all back to Zero
        /// </summary>
        private protected void ResetLocalVariableOrdinals()
        {
            foreach (var k in LocalVariableOrdinalDictionary.Keys)
            {
                LocalVariableOrdinalDictionary[k] = 0;
            }
        }

        /// <summary>
        ///     Retrieves a Local Variable Ordinal based on the ordinal name
        ///
        ///     This is tracked in a dictionary with each value incremented on each get
        /// </summary>
        /// <param name="variableName"></param>
        /// <returns></returns>
        private protected int GetLocalVariableOrdinal(string variableName)
        {
            if (!LocalVariableOrdinalDictionary.ContainsKey(variableName))
                LocalVariableOrdinalDictionary[variableName] = 0;

            return LocalVariableOrdinalDictionary[variableName]++;
        }

        /// <summary>
        ///     Creates a distinct variable name string to use internally when allocating variables
        ///     to store return results for methods. This gives each method call a distinct memory
        ///     pointer to be used to store the results based on the ordinal.
        /// </summary>
        /// <param name="variableName"></param>
        /// <returns>String containing the variable name and ordinal (ie: "MyVar1")</returns>
        private protected string GetLocalVariableName(string variableName) =>
            $"{variableName}{GetLocalVariableOrdinal(variableName)}";

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

        /// <summary>
        ///     Formats a printf() style string using the calling module's variable arguments
        ///     (see <see cref="PrintfFormatter"/> for the supported Borland C++ format specification)
        /// </summary>
        /// <param name="stringToParse">Format string. If NUL terminated, the result is NUL terminated as well</param>
        /// <param name="startingParameterOrdinal">Parameter ordinal of the first variable argument, or of the va_list pointer when isVsPrintf is set</param>
        /// <param name="isVsPrintf">The arguments are in the va_list pointed to by the parameter rather than on the stack</param>
        /// <returns></returns>
        private protected ReadOnlySpan<byte> FormatPrintf(ReadOnlySpan<byte> stringToParse, ushort startingParameterOrdinal, bool isVsPrintf = false)
        {
            var firstArgument = isVsPrintf
                ? GetParameterPointer(startingParameterOrdinal)
                : new FarPtr(Registers.SS, GetParameterOffset(startingParameterOrdinal));

            return PrintfFormatter.Format(stringToParse,
                new PrintfArgumentReader(Module.Memory, firstArgument, Registers.DS),
                Module.Memory,
                segment => Module.ProtectedMemory?.HasSegment(segment) ?? true,
                pointer => _logger.Error($"({Module.ModuleIdentifier}) Invalid Pointer: {pointer}"));
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

            //This can happen if a channel disconnects during the processing of the main loop
            if (!ChannelDictionary.TryGetValue(ChannelNumber, out var channelSession))
                return outputBuffer;

            var txtvarDictionary = channelSession.SessionVariables;

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
        ///     The GSBL BTUXMT supports custom ANSI escape sequences where strings are sent to the client
        ///     depending on if they have ANSI enabled or not. This routine parses those characters based on
        ///     the specified ANSI Support Level.
        ///
        ///     Because MBBSEmu **ONLY** supports ANSI, we ignore the non-ANSI component if an IF-ANSI sequence
        /// </summary>
        /// <param name="isAnsi"></param>
        /// <returns></returns>
        internal ReadOnlySpan<byte> ProcessIfANSI(ReadOnlySpan<byte> inputSpan, bool isAnsi = false)
        {
            using var resultStream = new MemoryStream(inputSpan.Length);
            for (var i = 0; i < inputSpan.Length; i++)
            {
                if (inputSpan[i] != 0x1B)
                {
                    resultStream.WriteByte(inputSpan[i]);

                    //Process ~~ escape
                    if (inputSpan[i] == '~' && i + 1 < inputSpan.Length && inputSpan[i + 1] == '~')
                        i++;

                    continue;
                }

                //Standalone 0x1B (CP437 left arrow glyph, not followed by '[')
                if (inputSpan[i] == 0x1B && (i + 1 >= inputSpan.Length || inputSpan[i + 1] != '['))
                {
                    resultStream.WriteByte(inputSpan[i]);
                    continue;
                }

                //Normal ANSI (ESC + '[' + not '[', or a truncated ESC[ at end of buffer)
                if (inputSpan[i] == 0x1B && inputSpan[i + 1] == '[' && (i + 2 >= inputSpan.Length || inputSpan[i + 2] != '['))
                {
                    resultStream.WriteByte(inputSpan[i]);
                    continue;
                }

                //Found IF-ANSI (ESC + '[[')
                if (inputSpan[i] == 0x1B && inputSpan[i + 1] == '[' && i + 2 < inputSpan.Length && inputSpan[i + 2] == '[')
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
                                    // Only a ~-escaped delimiter is literal output; an
                                    // unescaped one (including at j == 0, where there is no
                                    // preceding byte) is a segment delimiter and is dropped.
                                    if (j > 0 && substringSpan[j - 1] == '~')
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
        ///     Handles calling functions to format bytes to be sent to the client.
        ///x
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
