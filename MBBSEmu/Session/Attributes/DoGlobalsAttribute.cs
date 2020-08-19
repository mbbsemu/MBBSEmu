using System;
using System.Collections.Generic;
using System.Text;

namespace MBBSEmu.Session.Attributes
{
    /// <summary>
    ///     Defines the custom attribute "DoGlobals" which is
    ///     added to the enumSessionState fields to denote if
    ///     globals are to be processed while in the specific
    ///     state
    /// </summary>
    [AttributeUsage(AttributeTargets.Field)]
    internal class DoGlobalsAttribute : Attribute
    {
        private readonly bool _doGlobals;

        public DoGlobalsAttribute(bool doGlobals)
        {
            _doGlobals = doGlobals;
        }

        public DoGlobalsAttribute()
        {
            _doGlobals = true;
        }

        /// <summary>
        ///     Returns the value defined for "DoGlobals" of the given enumerator name
        /// </summary>
        /// <param name="enumType"></param>
        /// <param name="name"></param>
        /// <returns></returns>
        public static bool Get(Type enumType, string name)
        {
            var memberInstance = enumType.GetMember(name);
            if (memberInstance.Length <= 0) return false;


            if (GetCustomAttribute(memberInstance[0],
                typeof(DoGlobalsAttribute)) is DoGlobalsAttribute attr)
            {
                return attr._doGlobals;
            }
            return false;
        }

        /// <summary>
        ///     Returns the value defined for "DoGlobals" of the given Enumerator
        /// </summary>
        /// <param name="enumToGet"></param>
        /// <returns></returns>
        public static bool Get(object enumToGet)
        {
            var memberInstance = enumToGet?.GetType().GetMember(enumToGet.ToString());
            if (memberInstance == null || memberInstance.Length <= 0) return false;

            if (GetCustomAttribute(memberInstance[0],
                typeof(DoGlobalsAttribute)) is DoGlobalsAttribute attr)
            {
                return attr._doGlobals;
            }

            return false;
        }
    }
}
