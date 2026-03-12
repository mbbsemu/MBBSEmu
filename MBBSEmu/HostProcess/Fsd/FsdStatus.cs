using System.Collections.Generic;

namespace MBBSEmu.HostProcess.Fsd
{
    public class FsdStatus
    {
        /// <summary>
        ///     Channel Number for this Status
        /// </summary>
        public int Channel { get; set; }

        /// <summary>
        ///     Currently Selected Field (based on Ordinal)
        /// </summary>
        public FsdFieldSpec SelectedField
        {
            get => SelectedOrdinal >= 0 && SelectedOrdinal < Fields.Count
                ? Fields[SelectedOrdinal]
                : null;
            set
            {
                if (SelectedOrdinal >= 0 && SelectedOrdinal < Fields.Count)
                    Fields[SelectedOrdinal] = value;
            }
        }

        /// <summary>
        ///     Currently Selected Field Ordinal
        /// </summary>
        public int SelectedOrdinal { get; set; }

        /// <summary>
        ///     Error field for error messages, if defined in the template
        /// </summary>
        public FsdFieldSpec ErrorField { get; set; }

        /// <summary>
        ///     Field Specifications
        /// </summary>
        public List<FsdFieldSpec> Fields { get; set; }

        public FsdStatus()
        {
            Fields = new List<FsdFieldSpec>();
        }
    }
}
