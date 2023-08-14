using System;
using System.Configuration;
using System.IO;
using System.Linq;
using System.Xml.Linq;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
// ReSharper disable MemberCanBePrivate.Global

// ReSharper disable CheckNamespace
namespace Tcqz.ConfigruationManager
    // ReSharper restore CheckNamespace
{
    /// <summary>
    /// Provides portable, persistent application settings in JSON format.
    /// </summary>
    public class PortableJsonSettingsProvider : PortableSettingsProviderBase, IApplicationSettingsProvider
    {
        /// <summary>
        /// Specifies the name of the settings file to be used.
        /// </summary>
        public static string SettingsFileName { get; set; } = "settings.json";
        /// <summary>
        /// Name
        /// </summary>
        public override string Name => "PortableJsonSettingsProvider";

        /// <summary>
        /// Applies this settings provider to each property of the given settings.
        /// </summary>
        /// <param name="settingsList">An array of settings.</param>
        public static void ApplyProvider(params ApplicationSettingsBase[] settingsList)
            => ApplyProvider(new PortableJsonSettingsProvider(), settingsList);

        private string ApplicationSettingsFile => Path.Combine(SettingsDirectory, SettingsFileName);
        /// <summary>
        /// Reset Config
        /// </summary>
        /// <param name="context"></param>
        public override void Reset(SettingsContext context)
        {
            if (File.Exists(ApplicationSettingsFile))
                File.Delete(ApplicationSettingsFile);
        }

        private JObject GetJObject()
        {
            // to deal with multiple settings providers accessing the same file, reload on every set or get request.
            JObject jObject = null;
            bool initnew = false;
            if (File.Exists(this.ApplicationSettingsFile))
            {
                try
                {
                    jObject = JObject.Parse(File.ReadAllText(ApplicationSettingsFile));
                }
                catch
                {
                    initnew = true;
                }
            }
            else
                initnew = true;

            if (initnew)
            {
                jObject = new JObject(
                    new JProperty("userSettings",
                        new JObject(
                            new JProperty("roaming",
                                new JObject()))));
            }

            return jObject;
        }

        /// <summary>
        /// Get property values
        /// </summary>
        /// <param name="context"></param>
        /// <param name="collection"></param>
        /// <returns></returns>
        public override SettingsPropertyValueCollection GetPropertyValues(SettingsContext context,
            SettingsPropertyCollection collection)
        {
            JObject jObject = GetJObject();
            SettingsPropertyValueCollection values = new SettingsPropertyValueCollection();
            // iterate through settings to be retrieved
            foreach (SettingsProperty setting in collection)
            {
                SettingsPropertyValue value = new SettingsPropertyValue(setting)
                {
                    IsDirty = false,
                    //Set serialized value to element from file. This will be deserialized by SettingsPropertyValue when needed.
                    SerializedValue = GetSettingsValue(jObject, (string) context["GroupName"], setting)
                };
                values.Add(value);
            }

            return values;
        }
        /// <summary>
        /// Set Property Values
        /// </summary>
        /// <param name="context"><see cref="SettingsContext">SettingsContext</see></param>
        /// <param name="collection"></param>
        public override void SetPropertyValues(SettingsContext context, SettingsPropertyValueCollection collection)
        {
            JObject jObject = GetJObject();
            foreach (SettingsPropertyValue value in collection)
            {
                SetSettingsValue(jObject, (string) context["GroupName"], value);
            }

            try
            {
                File.WriteAllText(ApplicationSettingsFile,
                    JsonConvert.SerializeObject(JsonUtility.SortPropertiesAlphabetically(jObject),
                        Formatting.Indented));
            }
            catch
            {
                /* We don't want the app to crash if the settings file is not available */
            }
        }

        private object GetSettingsValue(JObject jObject, string scope, SettingsProperty prop)
        {
            object result;
            if (!IsUserScoped(prop))
                return null;
            //determine the location of the settings property
            JObject settings = (JObject) jObject.SelectToken("userSettings");
            if (IsRoaming(prop))
                settings = (JObject) settings?["roaming"];
            else settings = (JObject) settings?["PC_" + Environment.MachineName];
            // retrieve the value or set to default if available
            if (settings != null && settings[scope] != null)
            {
                JToken propVal = settings[scope][prop.Name];
                if (propVal != null)
                {
                    switch (prop.SerializeAs)
                    {
                        case SettingsSerializeAs.Xml:
                            // Convert json back to xml as this is expected for an xml-serialized element.
                            result = JsonConvert.DeserializeXNode(propVal.ToString())?.ToString();
                            break;
                        case SettingsSerializeAs.Binary:
                            result = Convert.FromBase64String(propVal.ToString());
                            break;
                        default:
                            result = propVal.ToString();
                            break;
                    }
                }
                else result = prop.DefaultValue;
            }
            else
                result = prop.DefaultValue;

            return result;
        }

        private void SetSettingsValue(JObject jObject, string scope, SettingsPropertyValue value)
        {
            if (!IsUserScoped(value.Property)) return;
            //determine the location of the settings property
            JObject settings = (JObject) jObject.SelectToken("userSettings");
            JObject settingsLoc;
            if (IsRoaming(value.Property))
                settingsLoc = (JObject) settings?["roaming"];
            else settingsLoc = (JObject) settings?["PC_" + Environment.MachineName];
            // the serialized value to be saved
            JToken serialized;
            if (value.SerializedValue == null) serialized = new JValue("");
            else if (value.Property.SerializeAs == SettingsSerializeAs.Xml)
            {
                // Convert serialized XML to JSON
                serialized =
                    JObject.Parse(JsonConvert.SerializeXNode(XElement.Parse(value.SerializedValue.ToString())));
            }
            else if (value.Property.SerializeAs == SettingsSerializeAs.Binary)
                serialized = new JValue(Convert.ToBase64String((byte[]) value.SerializedValue));
            else serialized = new JValue((string) value.SerializedValue);

            // check if setting already exists, otherwise create new
            if (settingsLoc == null)
            {
                string settingsSection;
                if (IsRoaming(value.Property)) settingsSection = "roaming";
                else settingsSection = "PC_" + Environment.MachineName;
                settingsLoc = new JObject(new JProperty(scope,
                    new JObject(new JProperty(value.Name, serialized))));
                settings?.Add(settingsSection, settingsLoc);
            }
            else
            {
                JObject scopeProp = (JObject) settingsLoc[scope];
                if (scopeProp != null)
                {
                    scopeProp[value.Name] = serialized;
                }
                else
                {
                    settingsLoc.Add(scope, new JObject(new JProperty(value.Name, serialized)));
                }
            }
        }
    }
    /// <summary>
    /// json
    /// </summary>
    // ReSharper disable once ClassNeverInstantiated.Global
    public class JsonUtility
    {
        /// <summary>
        /// 
        /// </summary>
        /// <param name="original"></param>
        /// <returns></returns>
        public static JObject SortPropertiesAlphabetically(JObject original)
        {
            var result = new JObject();

            foreach (var property in original.Properties().ToList().OrderBy(p => p.Name))
            {
                if (property.Value is JObject value)
                {
                    value = SortPropertiesAlphabetically(value);
                    result.Add(property.Name, value);
                }
                else
                {
                    result.Add(property.Name, property.Value);
                }
            }

            return result;
        }
    }
}