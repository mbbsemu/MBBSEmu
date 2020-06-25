using System.Collections.Generic;

namespace MBBSEmu.HostProcess.Fsd
{
    /// <summary>
    ///     FSD Field Specification Class
    /// </summary>
    public class FsdFieldSpec
    {
        /// <summary>
        ///     Field Name
        /// </summary>
        public string Name { get; set; }
        /// <summary>
        ///     Minimum Length for the Input Field
        /// </summary>
        public int LengthMin { get; set; }

        /// <summary>
        ///     Maximum Length for the Input Field
        /// </summary>
        public int LengthMax { get; set; }

        /// <summary>
        ///     Field Type for this Field
        /// </summary>
        public EnumFsdFieldType FsdFieldType { get; set; }

        /// <summary>
        ///     Specifies if spaces are allowed in this field
        /// </summary>
        public bool NoSpaces { get; set; }

        /// <summary>
        ///     List of Values for Multiple Choide/Drop Down Fields
        /// </summary>
        public List<string> Values { get; set; }

        /// <summary>
        ///     Specified Field Value
        /// </summary>
        public string Value { get; set; }

        public FsdFieldSpec()
        {
            Values = new List<string>();
            FsdFieldType = EnumFsdFieldType.Text;
        }
    }
}
