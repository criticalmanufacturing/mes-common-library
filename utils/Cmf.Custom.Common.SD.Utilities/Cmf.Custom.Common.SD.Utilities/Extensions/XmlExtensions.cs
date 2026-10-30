using Cmf.Custom.Common.SD.Utilities.Exceptions;
using Cmf.Foundation.Common;
using System.Text;
using System.Xml;
using System.Xml.Serialization;

namespace Cmf.Custom.Common.SD.Utilities.Extensions;

public static class XmlExtensions
{
    /// <summary>
    /// Retrieves the value of a tag on xml document by xpath
    /// </summary>
    /// <param name="doc">Xml Document</param>
    /// <param name="xPath">Path to the value</param>
    /// <param name="attribute">Get attribute tag</param>
    /// <returns>Value</returns>
    public static string? GetValueByXPath(this XmlDocument doc, string xPath, string? attribute = null)
    {
        string? value;

        XmlNode? node = doc.SelectSingleNode(xPath);
        if (node is not null)
        {
            if (attribute == null)
            {
                value = node.InnerText;
            }
            else
            {
                value = node.Attributes?[attribute]?.Value;
            }
        }
        else
        {
            throw new SDUtilitiesException("Error parsing XML");
        }

        return value;
    }

    /// <summary>
    /// To xml.
    /// </summary>
    /// <typeparam name="T">Class.</typeparam>
    /// <param name="instance">Instance.</param>
    /// <returns>Xml.</returns>
    public static string ToXml<T>(this T instance, bool ignoreNamespaces = false) where T : class
    {
        string? xml;

        XmlSerializer serializer = new(typeof(T));
        using (StringWriter writer = new Utf8StringWriter())
        {
            if (ignoreNamespaces)
            {
                XmlSerializerNamespaces ns = new();
                ns.Add(string.Empty, string.Empty);
                serializer.Serialize(writer, instance, ns);
            }
            else
            {
                serializer.Serialize(writer, instance);
            }
            xml = writer.ToString();
        }

        return xml;
    }
}

public class Utf8StringWriter : StringWriter
{
    private static readonly Encoding UTF8NoBom = new UTF8Encoding(false);

    public override Encoding Encoding => UTF8NoBom;
}