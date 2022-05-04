using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using System.Xml;
using System.Xml.Linq;

namespace TrafficManager.Persistence {
    internal static class XmlLinqExtensions {

        public static void AddAttribute<T>(this XElement element, XName name, T value) {
            if (!(typeof(T).IsClass && ReferenceEquals(value, default(T)))) {
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
                element.Add(name, value.Value);
            }
        }

        public static T Attribute<T>(this XElement element, XName name) {

            var value = element.Attribute(name)?.Value;
            return value == null ? (T)(object)value : ConvertFromXml<T>(value);
        }

        public static T? NullableAttribute<T>(this XElement element, XName name)
                where T : struct
                {

            var value = element.Attribute(name)?.Value;
            return value == null ? default : ConvertFromXml<T>(value);
        }

        public static T Element<T>(this XElement element, XName name) {

            var value = element.Element(name)?.Value;
            return value == null ? (T)(object)value : ConvertFromXml<T>(value);
        }

        public static IEnumerable<T> Elements<T>(this XElement element, XName name)
            => element.Elements(name).Select(e => ConvertFromXml<T>(e.Value));

        public static T? NullableElement<T>(this XElement element, XName name)
                where T : struct {

            var value = element.Element(name)?.Value;
            return value == null ? default : ConvertFromXml<T>(value);
        }

        public static T Value<T>(this XElement element) => ConvertFromXml<T>(element.Value);

        private static T ConvertFromXml<T>(string value) {
            switch (Type.GetTypeCode(typeof(T))) {

                case TypeCode.DateTime:
                    return (T)(object)DateTime.ParseExact(value, "O", CultureInfo.InvariantCulture);

                default:
                    if (typeof(T).IsEnum) {
                        return (T)Enum.Parse(typeof(T), value);
                    } else {
                        return (T)Convert.ChangeType(value, typeof(T), CultureInfo.InvariantCulture);
                    }
            }
        }
    }
}
