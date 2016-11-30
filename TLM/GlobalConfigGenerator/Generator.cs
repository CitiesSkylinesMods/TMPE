using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Xml;
using System.Xml.Serialization;
using TrafficManager.State;

namespace GlobalConfigGenerator {
	class Generator {
		public const string FILENAME = "TMPE_GlobalConfig.xml";
		public static int? RushHourParkingSearchRadius { get; private set; } = null;
		private static DateTime? rushHourConfigModifiedTime = null;
		private const string RUSHHOUR_CONFIG_FILENAME = "RushHourOptions.xml";
		private static uint lastRushHourConfigCheck = 0;

		public static void Main(string[] args) {
			/*WriteDefaultConfig();
			GlobalConfig conf = LoadConfig();*/
			TestRushHourConf();
			Console.ReadLine();
		}

		/*public static void WriteDefaultConfig() {
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
		}*/

		private static void TestRushHourConf() { // TODO refactor
			XmlDocument doc = new XmlDocument();
			doc.Load(RUSHHOUR_CONFIG_FILENAME);
			XmlNode root = doc.DocumentElement;

			XmlNode betterParkingNode = root.SelectSingleNode("OptionPanel/data/BetterParking");
			XmlNode parkingSpaceRadiusNode = root.SelectSingleNode("OptionPanel/data/ParkingSearchRadius");

			string  s = betterParkingNode.InnerText;

			if ("True".Equals(s)) {
				RushHourParkingSearchRadius = int.Parse(parkingSpaceRadiusNode.InnerText);
			}
		}
	}
}
