using System;
using System.Collections.Generic;
using System.Text;
using MBBSEmu.Memory;

namespace MBBSEmu.Module
{
    public class TextVariable
    {
        /// <summary>
        ///     Pointer to the char* routine that sets the value of the Text Variable
        /// </summary>
        public IntPtr16 Pointer { get; set; }

        /// <summary>
        ///     Variable Name
        /// </summary>
        public byte[] Name { get; set; }

        /// <summary>
        ///     Variable Value
        ///
        ///     Value determined by result of function located at Pointer
        /// </summary>
        public byte[] Value { get; set; }
    }
}
