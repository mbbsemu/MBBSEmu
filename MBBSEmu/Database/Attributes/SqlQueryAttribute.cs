using System;

namespace MBBSEmu.Database.Attributes
{
    /// <summary>
    ///     Defines the custom attribute "SqlQuery" to be added
    ///     to enums which allows us to define the embedded .sql
    ///     file the enumerator is referencing
    /// </summary>
    [AttributeUsage(AttributeTargets.Field)]
    internal class SqlQueryAttribute : Attribute
    {
        private readonly string _sqlQuery;

        public SqlQueryAttribute(string name)
        {
            _sqlQuery = name;
        }

        /// <summary>
        ///     Returns the value defined for "SqlQuery" of the given enumerator name
        /// </summary>
        /// <param name="enumType"></param>
        /// <param name="name"></param>
        /// <returns></returns>
        public static string Get(Type enumType, string name)
        {
            var memberInstance = enumType.GetMember(name);
            if (memberInstance.Length <= 0) return null;


            if (GetCustomAttribute(memberInstance[0],
                typeof(SqlQueryAttribute)) is SqlQueryAttribute attr)
            {
                return attr._sqlQuery;
            }
            return null;
        }

        /// <summary>
        ///     Returns the value defined for "SqlQuery" of the given Enumerator
        /// </summary>
        /// <param name="enumToGet"></param>
        /// <returns></returns>
        public static string Get(object enumToGet)
        {
            var memberInstance = enumToGet?.GetType().GetMember(enumToGet.ToString());
            if (memberInstance == null || memberInstance.Length <= 0) return null;

            if (GetCustomAttribute(memberInstance[0],
                typeof(SqlQueryAttribute)) is SqlQueryAttribute attr)
            {
                return $"{enumToGet.GetType().Namespace}.{attr._sqlQuery}";
            }

            return null;
        }
    }
}
