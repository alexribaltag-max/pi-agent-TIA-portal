using System;
using System.IO;
using System.Runtime.Serialization.Json;
using System.Text;
using System.Xml;

namespace TiaLocalBridge.Services
{
    internal static class InventoryJson
    {
        public static string Serialize<T>(T value)
        {
            using (var stream = new MemoryStream())
            {
                new DataContractJsonSerializer(typeof(T)).WriteObject(stream, value);
                return Encoding.UTF8.GetString(stream.ToArray());
            }
        }

        public static T Deserialize<T>(string json)
        {
            var bytes = new UTF8Encoding(false, true).GetBytes(json);
            using (var reader = JsonReaderWriterFactory.CreateJsonReader(bytes, new XmlDictionaryReaderQuotas
            {
                MaxDepth = 12, MaxStringContentLength = 16384, MaxArrayLength = 1024,
                MaxBytesPerRead = 16384, MaxNameTableCharCount = 16384
            }))
            {
                return (T)new DataContractJsonSerializer(typeof(T)).ReadObject(reader);
            }
        }
    }
}
