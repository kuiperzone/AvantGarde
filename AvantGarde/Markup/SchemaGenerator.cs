// -----------------------------------------------------------------------------
// PROJECT   : Avant Garde
// COPYRIGHT : Andy Thomas
// LICENSE   : GPLv3
// HOMEPAGE  : https://kuiper.zone/avantgarde-avalonia/
// -----------------------------------------------------------------------------

using System;
using System.Collections.Generic;
using System.Xml.Linq;

namespace AvantGarde.Markup
{
    /// <summary>
    /// Generates schema for <see cref="MarkupDictionary"/>. The schema is compatible with the "XAMl Complete"
    /// extension for Visual Code. See https://github.com/rogalmic/vscode-xml-complete
    /// </summary>
    public static class SchemaGenerator
    {
        private static readonly XNamespace _xs = "http://www.w3.org/2001/XMLSchema";

        /// <summary>
        /// Get schema text. The call is relatively expensive, so cache if needed often.
        /// </summary>
        public static string GetSchema(bool formatted = false, bool annotations = true)
        {
            return CreateDocument(annotations).ToString(formatted ? SaveOptions.None : SaveOptions.DisableFormatting);
        }

        /// <summary>
        /// Creates a new instance of <see cref="XDocument"/>.
        /// </summary>
        public static XDocument CreateDocument(bool annotations = true)
        {
            var root = CreateRootElement();
            root.Add(CreateSimpleElements());

            foreach (var item in MarkupDictionary.Types.Keys)
            {
                var info = MarkupDictionary.GetMarkupInfo(item) ??
                    throw new InvalidOperationException("Expected key not found");

                root.Add(CreateComplexElement(info, annotations));
            }

            var document = new XDocument();
            document.Add(root);

            return document;
        }

        /// <summary>
        /// Saves the schema to file.
        /// </summary>
        public static void SaveDocument(string filename, bool formatted = true, bool annotations = true)
        {
            CreateDocument(annotations).Save(filename, formatted ? SaveOptions.None : SaveOptions.DisableFormatting);
        }

        private static XElement[] CreateSimpleElements()
        {
            var restriction = new XElement(_xs + "restriction", new XAttribute("base", "xs:string"));

            var simple = new XElement(_xs + "simpleType", new XAttribute("name", "text"));
            simple.Add(restriction);

            return new[] { simple };
        }

        private static XElement[] CreateAttributes(params string[] attributeNames)
        {
            var list = new List<XElement>();

            foreach (var name in attributeNames)
            {
                list.Add(new XElement(_xs + "attribute", new XAttribute("name", name), new XAttribute("type", "text")));
            }

            return list.ToArray();
        }

        private static XElement? CreateAnnotationElement(string? text)
        {
            if (!string.IsNullOrWhiteSpace(text))
            {
                return new XElement(_xs + "annotation", new XElement(_xs + "documentation", text));
            }

            return null;
        }

        private static XElement CreateRootElement()
        {
            var attribs = new List<XAttribute>();

            attribs.Add(new XAttribute("id", "Schema." + MarkupDictionary.Name.Replace(' ', '-')));
            attribs.Add(new XAttribute("frameworkVersion", MarkupDictionary.Version));
            attribs.Add(new XAttribute("generator", "AvantGarde"));
            attribs.Add(new XAttribute("targetNamespace", MarkupDictionary.XmlNamespace));

            attribs.Add(new XAttribute(XNamespace.Xmlns + "xs", _xs));
            attribs.Add(new XAttribute(XNamespace.Xmlns + "noNamespaceSchemaLocation", "https://www.w3.org/2001/XMLSchema.xsd"));
            attribs.Add(new XAttribute("elementFormDefault", "qualified"));

            return new XElement(_xs + "schema", attribs.ToArray());
        }

        private static XElement[] CreateComplexElement(MarkupInfo info, bool annotations)
        {
            // Get attributes not in
            var attribs = new List<AttributeInfo>();
            var baseInfo = FilterComplexAttributes(info, attribs);

            var extension = new XElement(_xs + "extension");

            if (baseInfo != null)
            {
                extension.SetAttributeValue("base", baseInfo.Name + "Type");
            }

            var choice = new XElement(_xs + "choice", new XAttribute("minOccurs", "0"), new XAttribute("maxOccurs", "unbounded"));
            choice.Add(CreateAttributeElements(attribs, annotations));

            extension.Add(choice);

            var content = new XElement(_xs + "complexContent");
            content.Add(extension);

            var complexType = new XElement(_xs + "complexType", new XAttribute("name", info.Name + "Type"), new XAttribute("mixed", "true"));
            complexType.Add(content);


            var element = new XElement(_xs + "element", new XAttribute("name", info.Name),
                new XAttribute("mixed", "true"), new XAttribute("type", info.Name + "Type"));

            if (annotations)
            {
                element.Add(CreateAnnotationElement(info.GetHelpDocument()));
            }

            return new XElement[] { complexType, element };
        }

        private static MarkupInfo? FilterComplexAttributes(MarkupInfo info, List<AttributeInfo> attribs)
        {
            var baseName = info.ClassType.BaseType?.Name;
            MarkupInfo? baseInfo = MarkupDictionary.GetMarkupInfo(baseName);

            foreach (var item in info.Attributes.Values)
            {
                if (baseInfo == null || !baseInfo.Attributes.ContainsKey(item.Name))
                {
                    attribs.Add(item);
                }
            }

            return baseInfo;
        }

        private static XElement[] CreateAttributeElements(IReadOnlyCollection<AttributeInfo> attribs, bool annotations)
        {
            var list = new List<XElement>(attribs.Count);

            foreach (var item in attribs)
            {
                var e = new XElement(_xs + "attribute", new XAttribute("name", item.Name), new XAttribute("type", "text"));

                if (annotations)
                {
                    e.Add(CreateAnnotationElement(item.GetHelpDocument()));
                }

                list.Add(e);
            }

            return list.ToArray();
        }

        /*
        TBC - do we need this?
        private XElement CreateControlGroup()
        {
            var fcd = Framework.ElementDictionary;
            var elems = new List<XElement>(fcd.Count);

            foreach (var name in fcd.Keys)
            {
                elems.Add(new XElement(_xs + "element", new XAttribute("ref", name)));
            }

            var choice = new XElement(_xs + "choice");
            choice.Add(elems.ToArray());

            var group = new XElement(_xs + "group", new XAttribute("name", "controls"));
            group.Add(choice);
            return group;
        }
        */
    }
}