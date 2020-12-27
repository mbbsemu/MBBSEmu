using MBBSEmu.Memory;

namespace MBBSEmu.Module
{
    /// <summary>
    ///     Class used to define a Text Variable that's created using the REGISTER_VARIABLE method
    ///     in MAJORBBS.H
    /// </summary>
    public class TextVariable
    {
        /// <summary>
        ///     Pointer to the char* routine that sets the value of the Text Variable
        /// </summary>
        public FarPtr Pointer { get; set; }

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
