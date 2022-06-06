using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Xml;
using System.Xml.Linq;

namespace TrafficManager.Persistence {
    internal static class XmlLinqExtensions {

        public static XElement NewElement(this XElement element, XName name) {
            var result = new XElement(name);
            element.Add(result);
            return result;
        }

        public static void AddAttribute<T>(this XElement element, XName name, T value) {
            if (!(typeof(T).IsClass && ReferenceEquals(value, default(T)))) {
                element.Add(new XAttribute(name, ConvertToXml(value)));
            }
        }

        public static void AddAttribute(this XElement element, XName name, object value) {
            if (value != null) {
                element.Add(new XAttribute(name, ConvertToXml(value)));
            }
        }

        public static void AddElement(this XElement element, XName name, object value) {
            if (value != null) {
                element.Add(new XElement(name, ConvertToXml(value)));
            }
        }

        public static void AddElement<T>(this XElement element, XName name, T value) {
            if (!(typeof(T).IsClass && ReferenceEquals(value, default(T)))) {
                element.Add(new XElement(name, ConvertToXml(value)));
            }
        }

        public static void AddElements<T>(this XElement element, XName name, IEnumerable<T> values) {
            foreach (var value in values) {
                element.AddElement(name, value);
            }
        }

        public static void AddElements(this XElement element, XName name, params object[] values) {
            foreach (var value in values) {
                element.AddElement(name, value);
            }
        }

        public static void AddElements<T>(this XElement element, XName name, params T[] values)
                where T : struct
                {

            foreach (var value in values) {
                element.AddElement(name, value);
            }
        }

        private static string ConvertToXml(object value) {
            switch (value) {
                case DateTime dateTime:
                    return dateTime.ToString("O", CultureInfo.InvariantCulture);

                default:
                    if (value.GetType().IsEnum) {
                        return ConvertToXml(Convert.ChangeType(value, Enum.GetUnderlyingType(value.GetType())));
                    } else {
                        return (string)Convert.ChangeType(value, typeof(string), CultureInfo.InvariantCulture);
                    }
            }
        }

        public static void AddAttribute<T>(this XElement element, XName name, T? value)
                where T : struct
                {

            if (value.HasValue) {
                element.AddAttribute(name, value.Value);
            }
        }

        public static T Attribute<T>(this XElement element, XName name) {

            var value = element.Attribute(name)?.Value;
            return value == null ? default : ConvertFromXml<T>(value);
        }

        public static object Attribute(this XElement element, Type type, XName name) {
            var value = element.Attribute(name)?.Value;
            return value == null
                    ? type.IsValueType
                        ? Activator.CreateInstance(type)
                        : null
                    : ConvertFromXml(value, type);
        }

        public static T Element<T>(this XElement element, XName name) {

            var value = element.Element(name)?.Value;
            return value == null ? default : ConvertFromXml<T>(value);
        }

        public static object Element(this XElement element, Type type, XName name) {

            var value = element.Element(name)?.Value;
            return value == null
                    ? type.IsValueType
                        ? Activator.CreateInstance(type)
                        : null
                    : ConvertFromXml(value, type);
        }

        public static IEnumerable<T> Elements<T>(this XElement element, XName name)
            => element.Elements(name).Select(e => ConvertFromXml<T>(e.Value));

        public static T Value<T>(this XElement element) => ConvertFromXml<T>(element.Value);

        public static object Value(this XElement element, Type type) => ConvertFromXml(element.Value, type);

        private static T ConvertFromXml<T>(string value) => (T)ConvertFromXml(value, typeof(T));

        private static object ConvertFromXml(string value, Type type) {

            if (type.IsValueType) {
                type = Nullable.GetUnderlyingType(type) ?? type;
            }

            switch (Type.GetTypeCode(type)) {

                case TypeCode.DateTime:
                    return DateTime.ParseExact(value, "O", CultureInfo.InvariantCulture);

                default:
                    if (type.IsEnum) {
                        return Enum.Parse(type, value);
                    } else {
                        return Convert.ChangeType(value, type, CultureInfo.InvariantCulture);
                    }
            }
        }
    }
}
