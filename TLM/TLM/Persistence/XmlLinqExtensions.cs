using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Xml.Linq;

namespace TrafficManager.Persistence {
    internal static class XmlLinqExtensions {

        public static ushort AsUInt16(this XElement element) => (ushort)(uint)element;

        public static ushort AsUInt16(this XAttribute attribute) => (ushort)(uint)attribute;

        public static T AsEnum<T>(this XElement element)
                where T : struct
            => (T)Convert.ChangeType((long)element, typeof(T));


        public static T AsEnum<T>(this XAttribute attribute)
                where T : struct
            => (T)Convert.ChangeType((long)attribute, typeof(T));
    }
}
