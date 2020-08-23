using System;
using System.Text;

namespace MBBSEmu.HostProcess.Structs
{
    /// <summary>
    ///     Struct that holds the multilingual information for a user
    ///
    ///     Because we're only supporting ANSI & English, we set default values
    ///     in the constructor.
    /// </summary>
    public class LingoStruct
    {
        private const int LanguageNameSize = 16;
        private const int LanguageDescriptionSize = 51;
        private const int LanguageFileExtensionSize = 4;
        private const int LanguageEditorSize = 41;
        private const int LanguageYesNoOptionSize = 13;

        public byte[] name
        {
            get => new ReadOnlySpan<byte>(Data).Slice(0, LanguageNameSize).ToArray();
            set => Array.Copy(value, 0, Data, 0, value.Length);
        }

        public const ushort Size = LanguageNameSize + LanguageDescriptionSize + (LanguageFileExtensionSize * 3) +
                                   LanguageEditorSize + (LanguageYesNoOptionSize * 2);

        public readonly byte[] Data = new byte[Size];

        public LingoStruct()
        {
            name = Encoding.ASCII.GetBytes("ansi");
        }
    }
}
