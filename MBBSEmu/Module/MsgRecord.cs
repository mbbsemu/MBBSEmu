using System.Collections.Generic;

namespace MBBSEmu.Module
{
    /// <summary>
    ///     Represents a single record in the MSG file for the specified module
    /// </summary>
    public class MsgRecord
    {
        /// <summary>
        ///     Name of the MSG Value
        /// </summary>
        public string Name { get; set; }
        /// <summary>
        ///     Description of the Field
        /// </summary>
        public string Description { get; set; }
        /// <summary>
        ///     Ordinal Defined in the Module .H
        ///     Number referenced in code when reading MSG file values
        /// </summary>
        public int Ordinal { get; set; }
        /// <summary>
        ///     Prompt user would be displayed when setting this value
        /// </summary>
        public string Prompt { get; set; }
        /// <summary>
        ///     Default Value defined in the MSG File
        /// </summary>
        public string DefaultValue { get; set; }
        public List<string> Options { get; set; }
        /// <summary>
        ///     Specified Value
        /// </summary>
        public string Value { get; set; }
        /// <summary>
        ///     Data Type of MSG Value
        ///     (B == Boolean, S == String, N == Numeric, T == Text Block or ANSI)
        /// </summary>
        public string DataType { get; set; }
        /// <summary>
        ///     Maximum Allowed Value Length (if String)
        /// </summary>
        public int MaxLength { get; set; }
        /// <summary>
        ///     Minimum Value (if Numeric)
        /// </summary>
        public int MinValue { get; set; }
        /// <summary>
        ///     Maximum Value (if Numeric)
        /// </summary>
        public int MaxValue { get; set; }
        /// <summary>
        ///     Previous MSG Value required for this option to be used
        /// </summary>
        public string Predicate { get; set; }
    }
}
