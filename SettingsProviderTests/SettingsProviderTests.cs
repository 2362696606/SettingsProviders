using System;
using Tcqz.ConfigruationManager;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace SettingsProviderTests
{
    [TestClass]
    public class PortableSettingsProviderTests : SettingsProviderTestsBase
    {
        [ClassInitialize]
        public static void Initialize(TestContext context)
        {
            settingsFile = "portable.config";
            PortableSettingsProvider.ApplyProvider(Properties.Settings.Default);
            Properties.Settings.Default.Reset();
        }
    }

    [TestClass]
    public class PortableJsonSettingsProviderTests : SettingsProviderTestsBase
    {
        /// <summary>
        /// test
        /// </summary>
        /// <param name="context">aaa</param>
        [ClassInitialize]
        public static void Initialize(TestContext context)
        {
            settingsFile = "settings.json";
            PortableJsonSettingsProvider.ApplyProvider(Properties.Settings.Default);
            Properties.Settings.Default.Reset();
        }
    }

}
