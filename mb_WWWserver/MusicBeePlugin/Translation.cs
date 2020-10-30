using System;
using System.Xml.Serialization;

namespace MusicBeePlugin
{
    public class Translation
    {
        [XmlAttribute]
        public string Name { get; set; }

        [XmlText]
        public string Value { get; set; }
    }
}
