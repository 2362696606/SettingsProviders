using System;
using System.Collections;
using System.Collections.Specialized;
using System.Configuration;
using System.Reflection;

namespace Tcqz.ConfigruationManager
{
    /// <summary>
    /// A shared base class for portable settings providers.
    /// </summary>
    public abstract class PortableSettingsProviderBase : SettingsProvider, IApplicationSettingsProvider
    {
        /// <summary>
        /// Specifies if all settings should be roaming.
        /// </summary>
        // ReSharper disable once MemberCanBePrivate.Global
        public static bool AllRoaming { get; set; } = false;

        /// <summary>
        /// Specifies the directory of the settings file.
        /// </summary>
        // ReSharper disable once MemberCanBeProtected.Global
        public static string SettingsDirectory { get; set; } = AppDomain.CurrentDomain.BaseDirectory;
        /// <summary>
        /// 
        /// </summary>
        public override string ApplicationName { get => Assembly.GetExecutingAssembly().GetName().Name;
            set { } }
        /// <summary>
        /// 应用提供者
        /// </summary>
        /// <param name="provider"></param>
        /// <param name="settingsList"></param>
        protected static void ApplyProvider(PortableSettingsProviderBase provider, params ApplicationSettingsBase[] settingsList)
        {
            foreach (var settings in settingsList)
            {
                settings.Providers.Clear();
                settings.Providers.Add(provider);
                foreach (SettingsProperty prop in settings.Properties)
                    prop.Provider = provider;
                settings.Reload();
            }
        }

        /// <summary>
        /// 初始化
        /// </summary>
        /// <param name="name"></param>
        /// <param name="config"></param>
        public override void Initialize(string name, NameValueCollection config)
        {
            if (String.IsNullOrEmpty(name)) name = Name;
            base.Initialize(name, config);
        }
        /// <summary>
        /// 获取前一版本
        /// </summary>
        /// <param name="context"></param>
        /// <param name="property"></param>
        /// <returns></returns>
        /// <exception cref="NotImplementedException"></exception>
        public virtual SettingsPropertyValue GetPreviousVersion(SettingsContext context, SettingsProperty property)
        {
            throw new NotImplementedException();
        }
        /// <summary>
        /// 重置
        /// </summary>
        /// <param name="context"></param>
        public abstract void Reset(SettingsContext context);
        /// <summary>
        /// 更新
        /// </summary>
        /// <param name="context"></param>
        /// <param name="properties"></param>
        public virtual void Upgrade(SettingsContext context, SettingsPropertyCollection properties)
        { /* don't do anything here*/ }

        /// <summary>
        /// Iterates through a property's attributes to determine whether it is user-scoped or application-scoped.
        /// </summary>
        protected bool IsUserScoped(SettingsProperty prop)
        {
            foreach (DictionaryEntry d in prop.Attributes)
            {
                Attribute a = (Attribute)d.Value;
                if (a is UserScopedSettingAttribute)
                    return true;
            }
            return false;
        }

        /// <summary>
        /// Iterates through a property's attributes to determine whether it is set to roam.
        /// </summary>
        protected bool IsRoaming(SettingsProperty prop)
        {
            if (AllRoaming)
                return true;
            foreach (DictionaryEntry d in prop.Attributes)
            {
                Attribute a = (Attribute)d.Value;
                if (a is SettingsManageabilityAttribute)
                    return true;
            }
            return false;
        }
    }
}
