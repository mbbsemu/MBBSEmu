using System;

namespace MBBSEmu.Host
{
    [AttributeUsage(AttributeTargets.Method)]
    internal class MBBSHostFunction : Attribute
    {
        private readonly string _importedName;
        private readonly int _ordinal;

        public MBBSHostFunction(string importedName, int ordinal)
        {
            _importedName = importedName;
            _ordinal = ordinal;
        }

        /// <summary>
        ///     Returns the value defined for "Ordinal" of the given enumerator name
        /// </summary>
        /// <param name="enumType"></param>
        /// <param name="name"></param>
        /// <returns></returns>
        public static int GetOrdinal(Type enumType, string name)
        {
            var memberInstance = enumType.GetMember(name);
            if (memberInstance.Length <= 0) return -1;


            if (GetCustomAttribute(memberInstance[0],
                typeof(MBBSHostFunction)) is MBBSHostFunction attr)
            {
                return attr._ordinal;
            }
            return -1;
        }

        /// <summary>
        ///     Returns the value defined for "ImportedName" of the given enumerator name
        /// </summary>
        /// <param name="enumType"></param>
        /// <param name="name"></param>
        /// <returns></returns>
        public static string GetImportedName(Type enumType, string name)
        {
            var memberInstance = enumType.GetMember(name);
            if (memberInstance.Length <= 0) return null;


            if (GetCustomAttribute(memberInstance[0],
                typeof(MBBSHostFunction)) is MBBSHostFunction attr)
            {
                return attr._importedName;
            }
            return null;
        }

        
    }
}
