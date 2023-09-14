using System;

namespace MBBSEmu.UI
{
    /// <summary>
    ///     Custom Attribute to define the Name and Description of a UI View
    /// </summary>
    [AttributeUsage(AttributeTargets.Class)]
    public class UIMetadata : Attribute
    {

        /// <summary>
        ///     Name of the UI View
        /// </summary>
        public string Name { get; set; }

        /// <summary>
        ///    Description of the UI View
        /// </summary>
        public string Description { get; set; }

        /// <summary>
        ///     Default Constructor
        /// </summary>
        /// <param name="name"></param>
        /// <param name="description"></param>
        public UIMetadata(string name, string description)
        {
            this.Name = name;
            this.Description = description;
        }

        public static string GetName(Type t) => ((UIMetadata)GetCustomAttributes(t)[0]).Name;

        public static string GetDescription(Type t) => ((UIMetadata)GetCustomAttributes(t)[0]).Description;
    }
}
