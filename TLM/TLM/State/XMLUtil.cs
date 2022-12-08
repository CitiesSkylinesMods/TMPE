namespace TrafficManager.State {
    using System.IO;
    using System.Text;
    using System.Xml.Serialization;
    using System.Xml;

    internal static class XMLUtil {
        internal static XmlSerializerNamespaces NoNamespaces =>
            new XmlSerializerNamespaces(new[] { XmlQualifiedName.Empty });

        internal static string Serialize<T>(T obj) {
            var serializer = new XmlSerializer(typeof(T));
            StringBuilder sb = new();
            using (TextWriter writer = new StringWriter(sb)) {
                using (var xmlWriter = new XmlTextWriter(writer)) {
                    serializer.Serialize(xmlWriter, obj, NoNamespaces);
                }
            }
            return sb.ToString();
        }

        internal static T Deserialize<T>(string data) {
            if (string.IsNullOrEmpty(data)) {
                return default;
            }
            XmlSerializer ser = new XmlSerializer(typeof(T));
            using (var reader = new StringReader(data)) {
                return (T)ser.Deserialize(reader);
            }
        }
    }
}
