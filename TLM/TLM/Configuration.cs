using System.Collections.Generic;
using System.IO;
using System.Xml.Serialization;

namespace TrafficManager
{
    public class Configuration
    {
        public string nodeTrafficLights;
        public string nodeCrosswalk;
        public string laneFlags;

        public List<int[]> prioritySegments = new List<int[]>();
        public List<int[]> nodeDictionary = new List<int[]>(); 
        public List<int[]> manualSegments = new List<int[]>(); 

        public List<int[]> timedNodes = new List<int[]>();
        public List<ushort[]> timedNodeGroups = new List<ushort[]>();
        public List<int[]> timedNodeSteps = new List<int[]>();
        public List<int[]> timedNodeStepSegments = new List<int[]>(); 

        public void OnPreSerialize()
        {
        }

        public void OnPostDeserialize()
        {
        }

        public static void Serialize(string filename, Configuration config)
        {
            var serializer = new XmlSerializer(typeof(Configuration));

            using (var writer = new StreamWriter(filename))
            {
                config.OnPreSerialize();
                serializer.Serialize(writer, config);
            }
        }

        public static Configuration Deserialize(string filename)
        {
            var serializer = new XmlSerializer(typeof(Configuration));

            try
            {
                using (var reader = new StreamReader(filename))
                {
                    var config = (Configuration)serializer.Deserialize(reader);
                    config.OnPostDeserialize();
                    return config;
                }
            }
            catch { }

            return null;
        }
    }
}