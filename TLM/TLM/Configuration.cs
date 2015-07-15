using System.Collections.Generic;
using System.IO;
using System.Xml.Serialization;

namespace TrafficManager
{
    public class Configuration
    {
        public string NodeTrafficLights;
        public string NodeCrosswalk;
        public string LaneFlags;

        public List<int[]> PrioritySegments = new List<int[]>();
        public List<int[]> NodeDictionary = new List<int[]>(); 
        public List<int[]> ManualSegments = new List<int[]>(); 

        public List<int[]> TimedNodes = new List<int[]>();
        public List<ushort[]> TimedNodeGroups = new List<ushort[]>();
        public List<int[]> TimedNodeSteps = new List<int[]>();
        public List<int[]> TimedNodeStepSegments = new List<int[]>(); 
        
        public static void SaveConfigurationToFile(string filename, Configuration config)
        {
            var serializer = new XmlSerializer(typeof(Configuration));

            using (var writer = new StreamWriter(filename))
            {
                serializer.Serialize(writer, config);
            }
        }

        public static Configuration LoadConfigurationFromFile(string filename)
        {
            var serializer = new XmlSerializer(typeof(Configuration));

            try
            {
                using (var reader = new StreamReader(filename))
                {
                    var config = (Configuration)serializer.Deserialize(reader);
                    return config;
                }
            }
            catch
            {
                // ignored
            }

            return null;
        }
    }
}