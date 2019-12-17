using MBBSEmu.CPU;
using MBBSEmu.Extensions;
using MBBSEmu.HostProcess.Attributes;
using MBBSEmu.Logging;
using MBBSEmu.Memory;
using MBBSEmu.Module;
using NLog;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using MBBSEmu.Session;

namespace MBBSEmu.HostProcess.ExportedModules
{
    /// <summary>
    ///     Base Class for Exported MajorBBS Routines
    /// </summary>
    public abstract class ExportedModuleBase
    {
        public delegate ushort ExportedFunctionDelegate();

        /// <summary>
        ///     Dictionary of Exported Functions
        ///
        ///     Key Represents the Function Ordinal in the associated .H file
        /// </summary>
        public Dictionary<ushort, ExportedFunctionDelegate> ExportedFunctions;

        /// <summary>
        ///     Internal Variables are stored inside the system (module name, etc.)
        ///
        ///     We want to write these only once to the HostMemorySegment, so we use this
        ///     dictionary to track the variable by a given name (key) and the pointer to the segment
        ///     it lives in.
        /// </summary>
        private protected Dictionary<string, IntPtr16> HostMemoryVariables;

        private protected PointerDictionary<UserSession> ChannelDictionary;

        private protected static readonly Logger _logger = LogManager.GetCurrentClassLogger(typeof(CustomLogger));

        public IMemoryCore Memory;
        public CpuRegisters Registers;
        public MbbsModule Module;

        /// <summary>
        ///     Convenience Variable, prevents having to repeatedly cast the Enum to ushort
        /// </summary>
        private protected readonly ushort HostMemorySegment = (ushort) EnumHostSegments.HostMemorySegment;

        private protected ExportedModuleBase(MbbsModule module, PointerDictionary<UserSession> channelDictionary)
        {
            Module = module;
            Memory = module.Memory;
            ChannelDictionary = channelDictionary;

            //Setup Exported Functions
            ExportedFunctions = new Dictionary<ushort, ExportedFunctionDelegate>();
            SetupExportedFunctionDelegates();

            HostMemoryVariables = new Dictionary<string, IntPtr16>();
        }

        /// <summary>
        ///     Sets the value in UserNum segment to the desired channelNumber
        ///
        ///     Used by the USERNUM() method to get the channel of the current user
        /// </summary>
        /// <param name="channelNumber"></param>
        public void SetCurrentChannel(ushort channelNumber)
        {
            Memory.SetWord((ushort)EnumHostSegments.UserNum, 0, channelNumber);
        }

        private void SetupExportedFunctionDelegates()
        {
            _logger.Info($"Setting up {this.GetType().Name.ToUpper()} exported functions...");
            var functionBindings = GetType()
                .GetMethods(BindingFlags.Public | BindingFlags.Instance)
                .Where(m => m.GetCustomAttributes(typeof(ExportedFunctionAttribute), false).Length > 0).Select(y => new
                {
                    binding = (ExportedFunctionDelegate) Delegate.CreateDelegate(typeof(ExportedFunctionDelegate),
                        this,
                        y.Name),
                    definitions = y.GetCustomAttributes(typeof(ExportedFunctionAttribute))
                });

            foreach (var f in functionBindings)
            {
                var ordinal = ((ExportedFunctionAttribute) f.definitions.First()).Ordinal;
                ExportedFunctions[ordinal] = f.binding;
            }

            _logger.Info($"Setup {ExportedFunctions.Count} functions from {GetType().Name.ToUpper()}");
        }


        /// <summary>
        ///     Gets the parameter by ordinal passed into the routine
        /// </summary>
        /// <param name="parameterOrdinal"></param>
        /// <returns></returns>
        private protected ushort GetParameter(ushort parameterOrdinal)
        {
            var parameterOffset = (ushort) (Registers.BP + 5 + (2 * parameterOrdinal));
            return Memory.GetWord(Registers.SS, parameterOffset);
        }

        private static readonly char[] _printfSpecifiers = {'c', 'd', 's', 'e', 'E', 'f', 'g', 'G', 'o', 'x', 'X', 'u', 'i', 'P', 'N', '%'};
        private static readonly char[] _printfFlags = {'-', '+', ' ', '#', '0'};
        private static readonly char[] _printfWidth = {'1', '2', '3', '4', '5', '6', '7', '8', '9', '0'};
        private static readonly char[] _printfPrecision = {'.', '1', '2', '3', '4', '5', '6', '7', '8', '9', '0', '*' };
        private static readonly char[] _printfLength = {'h', 'l', 'j', 'z', 't', 'L'};

        private static bool InSpan(ReadOnlySpan<char> spanToSearch, ReadOnlySpan<byte> character)
        {
            for (var i = 0; i < spanToSearch.Length; i++)
            {
                if (spanToSearch[i] == character[0])
                    return true;
            }
            return false;
        }

        private static bool IsPrintfPrecision(ReadOnlySpan<byte> c) => c[0] == _printfPrecision[0];

        /// <summary>
        ///     Printf Parsing and Encoding
        /// </summary>
        /// <param name="s"></param>
        /// <param name="stringToParse"></param>
        /// <param name="startingParameterOrdinal"></param>
        /// <returns></returns>
        private protected ReadOnlySpan<byte> FormatPrintf(ReadOnlySpan<byte> stringToParse, ushort startingParameterOrdinal)
        {
            using var msOutput = new MemoryStream(stringToParse.Length);
            var currentParameter = startingParameterOrdinal;
            for (var i = 0; i < stringToParse.Length; i++)
            {
                //Found a Control Character
                if (stringToParse[i] == '%' && stringToParse[i + 1] != '%')
                {
                    using var msFormattedValue = new MemoryStream();
                    i++;

                    //Process Flags
                    var stringFlags = EnumPrintfFlags.None;
                    while (InSpan(_printfFlags, stringToParse.Slice(i, 1)))
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
                    while (InSpan(_printfWidth, stringToParse.Slice(i, 1)))
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
                    while (InSpan(_printfPrecision, stringToParse.Slice(i, 1)))
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
                    while (InSpan(_printfLength, stringToParse.Slice(i, 1)))
                    {
                        i++;
                    }

                    //Finally i should be at the specifier 
                    if (!InSpan(_printfSpecifiers, stringToParse.Slice(i, 1)))
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

                            var parameterOffset = GetParameter(currentParameter++);
                            var parameterSegment = GetParameter(currentParameter++);
                            var parameter = Memory.GetString(parameterSegment, parameterOffset);
                            msFormattedValue.Write(parameter);
                            break;
                        }
                        case 'd':
                        {
                            var parameter = GetParameter(currentParameter++);
                            msFormattedValue.Write(Encoding.ASCII.GetBytes(parameter.ToString()));
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
                                msFormattedValue.Position = 0;
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