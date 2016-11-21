using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Xml.Serialization;
using TrafficManager.State;

namespace GlobalConfigGenerator {
	class Generator {
		public const string FILENAME = "TMPE_GlobalConfig.xml";

		public static void Main(string[] args) {
			WriteDefaultConfig();
			GlobalConfig conf = LoadConfig();
			Console.ReadLine();
		}

		public static void WriteDefaultConfig() {
			GlobalConfig conf = new GlobalConfig();
			XmlSerializer serializer = new XmlSerializer(typeof(GlobalConfig));
			TextWriter writer = new StreamWriter(FILENAME);
			serializer.Serialize(writer, conf);
			writer.Close();
		}

		public static GlobalConfig LoadConfig() {
			FileStream fs = new FileStream(FILENAME, FileMode.Open);
			XmlSerializer serializer = new XmlSerializer(typeof(GlobalConfig));
			GlobalConfig conf = (GlobalConfig)serializer.Deserialize(fs);
			Console.WriteLine("OK");
			return conf;
		}
	}
}
