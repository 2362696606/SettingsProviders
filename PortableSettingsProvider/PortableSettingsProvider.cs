﻿using System;
using System.Configuration;
using System.IO;
using System.Xml;
using System.Xml.Linq;

// ReSharper disable CheckNamespace
namespace Tcqz.ConfigruationManager
// ReSharper restore CheckNamespace
{
    /// <summary>
    /// Provides portable, persistent application settings.
    /// </summary>
    public class PortableSettingsProvider : PortableSettingsProviderBase
    {
        /// <summary>
        /// Specifies the name of the settings file to be used.
        /// </summary>
        // ReSharper disable once MemberCanBePrivate.Global
        public static string SettingsFileName { get; set; } = "portable.config";

        /// <summary>
        /// Name
        /// </summary>
        public override string Name => "PortableSettingsProvider";

        /// <summary>
        /// Applies this settings provider to each property of the given settings.
        /// </summary>
        /// <param name="settingsList">An array of settings.</param>
        public static void ApplyProvider(params ApplicationSettingsBase[] settingsList)
            => ApplyProvider(new PortableSettingsProvider(), settingsList);

        private string ApplicationSettingsFile => Path.Combine(SettingsDirectory, SettingsFileName);

        /// <summary>
        /// 重置
        /// </summary>
        /// <param name="context"></param>
        public override void Reset(SettingsContext context)
        {
            if (File.Exists(ApplicationSettingsFile))
                File.Delete(ApplicationSettingsFile);
        }

        private XDocument GetXmlDoc()
        {
            // to deal with multiple settings providers accessing the same file, reload on every set or get request.
            XDocument xmlDoc = null;
            bool initnew = false;
            if (File.Exists(this.ApplicationSettingsFile))
            {
                try
                {
                    xmlDoc = XDocument.Load(ApplicationSettingsFile);
                }
                catch { initnew = true; }
            }
            else
                initnew = true;
            if (initnew)
            {
                xmlDoc = new XDocument(new XElement("configuration",
                    new XElement("userSettings", new XElement("Roaming"))));
            }
            return xmlDoc;
        }

        /// <summary>
        /// 获取上一版本
        /// </summary>
        /// <param name="context"></param>
        /// <param name="collection"></param>
        /// <returns></returns>
        public override SettingsPropertyValueCollection GetPropertyValues(SettingsContext context, SettingsPropertyCollection collection)
        {
            XDocument xmlDoc = GetXmlDoc();
            SettingsPropertyValueCollection values = new SettingsPropertyValueCollection();
            // iterate through settings to be retrieved
            foreach (SettingsProperty setting in collection)
            {
                SettingsPropertyValue value = new SettingsPropertyValue(setting);
                value.IsDirty = false;
                //Set serialized value to xml element from file. This will be deserialized by SettingsPropertyValue when needed.
                value.SerializedValue = GetXmlValue(xmlDoc, XmlConvert.EncodeLocalName((string)context["GroupName"]), setting);
                values.Add(value);
            }
            return values;
        }
        /// <summary>
        /// 写值
        /// </summary>
        /// <param name="context"></param>
        /// <param name="collection"></param>
        public override void SetPropertyValues(SettingsContext context, SettingsPropertyValueCollection collection)
        {
            XDocument xmlDoc = GetXmlDoc();
            foreach (SettingsPropertyValue value in collection)
            {
                SetXmlValue(xmlDoc, XmlConvert.EncodeLocalName((string)context["GroupName"]), value);
            }
            try
            {
                // Make sure that special chars such as '\r\n' are preserved by replacing them with char entities.
                using (var writer = XmlWriter.Create(ApplicationSettingsFile,
                    new XmlWriterSettings() { NewLineHandling = NewLineHandling.Entitize, Indent = true }))
                {
                    xmlDoc.Save(writer);
                }
            }
            catch { /* We don't want the app to crash if the settings file is not available */ }
        }
        /// <summary>
        /// 获取xml值
        /// </summary>
        /// <param name="xmlDoc"></param>
        /// <param name="scope"></param>
        /// <param name="prop"></param>
        /// <returns></returns>
        private object GetXmlValue(XDocument xmlDoc, string scope, SettingsProperty prop)
        {
            object result;
            if (!IsUserScoped(prop))
                return null;
            //determine the location of the settings property
            XElement xmlSettings = xmlDoc.Element("configuration")?.Element("userSettings");
            xmlSettings = IsRoaming(prop) ? xmlSettings?.Element("Roaming") : xmlSettings?.Element("PC_" + Environment.MachineName);
            // retrieve the value or set to default if available
            if (xmlSettings != null && xmlSettings.Element(scope) != null && xmlSettings.Element(scope)?.Element(prop.Name) != null)
            {
                using (var reader = xmlSettings.Element(scope)?.Element(prop.Name)?.CreateReader())
                {
                    reader?.MoveToContent();
                    switch (prop.SerializeAs)
                    {
                        case SettingsSerializeAs.Xml:
                            result = reader?.ReadInnerXml();
                            break;
                        case SettingsSerializeAs.Binary:
                            result = reader?.ReadInnerXml();
                            result = Convert.FromBase64String((string)result ?? string.Empty);
                            break;
                        default:
                            result = reader?.ReadElementContentAsString();
                            break;
                    }
                }
            }
            else
                result = prop.DefaultValue;
            return result;
        }

        private void SetXmlValue(XDocument xmlDoc, string scope, SettingsPropertyValue value)
        {
            if (!IsUserScoped(value.Property)) return;
            //determine the location of the settings property
            XElement xmlSettings = xmlDoc.Element("configuration")?.Element("userSettings");
            XElement xmlSettingsLoc;
            if (IsRoaming(value.Property))
                xmlSettingsLoc = xmlSettings?.Element("Roaming");
            else xmlSettingsLoc = xmlSettings?.Element("PC_" + Environment.MachineName);
            // the serialized value to be saved
            XNode serialized;
            if (value.SerializedValue == null || value.SerializedValue is string s && String.IsNullOrWhiteSpace(s))
                serialized = new XText("");
            else if (value.Property.SerializeAs == SettingsSerializeAs.Xml)
                serialized = XElement.Parse((string)value.SerializedValue);
            else if (value.Property.SerializeAs == SettingsSerializeAs.Binary)
                serialized = new XText(Convert.ToBase64String((byte[])value.SerializedValue));
            else serialized = new XText((string)value.SerializedValue);
            // check if setting already exists, otherwise create new
            if (xmlSettingsLoc == null)
            {
                if (IsRoaming(value.Property)) xmlSettingsLoc = new XElement("Roaming");
                else xmlSettingsLoc = new XElement("PC_" + Environment.MachineName);
                xmlSettingsLoc.Add(new XElement(scope,
                    new XElement(value.Name, serialized)));
                xmlSettings?.Add(xmlSettingsLoc);
            }
            else
            {
                XElement xmlScope = xmlSettingsLoc.Element(scope);
                if (xmlScope != null)
                {
                    XElement xmlElem = xmlScope.Element(value.Name);
                    if (xmlElem == null) xmlScope.Add(new XElement(value.Name, serialized));
                    else xmlElem.ReplaceAll(serialized);
                }
                else
                {
                    xmlSettingsLoc.Add(new XElement(scope, new XElement(value.Name, serialized)));
                }
            }
        }
    }
}
