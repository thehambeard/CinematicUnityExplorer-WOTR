using System;
using System.Collections.Generic;
using System.IO;
using System.Xml.Serialization;

namespace UnityExplorer.Serializers
{
    public class BonesSerializer
    {
        public static string Serialize(Dictionary<string, List<CachedBonesTransform>> dict)
        {
            SerializableDictionary serializableDict = new SerializableDictionary(dict);

            var serializer = new XmlSerializer(typeof(SerializableDictionary));
            using (var writer = new StringWriter())
            {
                serializer.Serialize(writer, serializableDict);
                return writer.ToString();
            }
        }

        public static Dictionary<string, List<CachedBonesTransform>> Deserialize(string xml)
        {
            var serializer = new XmlSerializer(typeof(SerializableDictionary));
            using (var reader = new StringReader(xml))
            {
                return ((SerializableDictionary)serializer.Deserialize(reader)).ToDictionary();
            }
        }

        // Old versions of .net can't natively serialize dictionaries, therefore we make a couple of classes to do it ourselves
        [XmlRoot("Dictionary")]
        public class SerializableDictionary
        {
            [XmlElement("Item")]
            public List<DictionaryItem> Items { get; set; } = new List<DictionaryItem>();

            public SerializableDictionary() { }

            public SerializableDictionary(Dictionary<string, List<CachedBonesTransform>> dictionary)
            {
                foreach (var kvp in dictionary)
                {
                    Items.Add(new DictionaryItem { Key = kvp.Key, Value = kvp.Value });
                }
            }

            public Dictionary<string, List<CachedBonesTransform>> ToDictionary()
            {
                var dictionary = new Dictionary<string, List<CachedBonesTransform>>();
                foreach (var item in Items)
                {
                    dictionary[item.Key] = item.Value;
                }
                return dictionary;
            }
        }

        public class DictionaryItem
        {
            [XmlAttribute("Key")]
            public string Key { get; set; }

            [XmlElement("Value")]
            public List<CachedBonesTransform> Value { get; set; }
        }
    }

    public struct CachedBonesTransform
    {
        public CachedBonesTransform(Vector3 position, Vector3 angles, Vector3 scale)
        {
            this.position = position;
            this.angles = angles;
            this.scale = scale;
        }

        public readonly Vector3 position;
        public readonly Vector3 angles;
        public readonly Vector3 scale;

        public void CopyToTransform(Transform transform){
            transform.localPosition = position;
            transform.localEulerAngles = angles;
            transform.localScale = scale;
        }
    }
}
