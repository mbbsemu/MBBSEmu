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
        ///     Currently Selected Field Number
        /// </summary>
        public int SelectedField { get; set; }

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
