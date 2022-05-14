using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Xml.Linq;

namespace TrafficManager.Persistence {
    public static class XSerializer {

        public static XElement XSerialize<T>(this T value, XName name)
                where T : new()
            => value.XSerialize(typeof(T), name);

        public static XElement XSerialize(this object value, Type type, XName name) {

            XElement element = null;

            if (value != null) {

                if (Type.GetTypeCode(type) != TypeCode.Object) {
                    throw new ArgumentException("Non-primitive type required");
                }

                element = new XElement(name);

                foreach (var field in type.GetFields(BindingFlags.Instance | BindingFlags.FlattenHierarchy | BindingFlags.Public | BindingFlags.NonPublic)) {

                    var fieldValue = field.GetValue(value);

                    if (fieldValue != null) {

                        if (Type.GetTypeCode(field.FieldType) == TypeCode.Object) {
                            element.Add(fieldValue.XSerialize(field.FieldType, field.Name));
                        } else {
                            element.AddAttribute(field.Name, fieldValue);
                        }
                    }
                }
            }
            return element;
        }

        public static T XDeserialize<T>(this XElement element)
                where T : new()
            => (T)element.XDeserialize(typeof(T));

        public static object XDeserialize(this XElement element, Type type) {

            object result = null;

            if (element != null) {

                if (Type.GetTypeCode(type) != TypeCode.Object) {
                    throw new ArgumentException("Non-primitive type required");
                }

                result = Activator.CreateInstance(type);

                foreach (var field in type.GetFields(BindingFlags.Instance | BindingFlags.FlattenHierarchy | BindingFlags.Public | BindingFlags.NonPublic)) {

                    if (Type.GetTypeCode(field.FieldType) == TypeCode.Object) {
                        var memberElement = element.Element(field.Name);
                        if (memberElement != null) {
                            field.SetValue(result, memberElement.XDeserialize(field.FieldType));
                        }
                    } else {
                        field.SetValue(result, element.Attribute(field.FieldType, field.Name));
                    }
                }
            }

            return result;
        }
    }
}
