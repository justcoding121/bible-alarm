using System.Xml;

namespace Bible.Alarm.VersionPatcher.Services.Contracts;

public interface IXmlDocument
{
    void LoadXml(string xml);
    XmlNode? SelectSingleNode(string xpath);
    string OuterXml { get; }
}
