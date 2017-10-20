﻿using System.Fabric;

namespace BackEnd.Core.Stateless.Helpers
{
    public static class ServiceFabricConfiguration
    {
        public static string GetConfigurationSettingValue(string sectionName, string settingName, string defaultValue, string package = "Config")
        {
            // Defaulting to Config package though in theory different folders may be created for different settings collection
            var configurationPackage = FabricRuntime.GetActivationContext().GetConfigurationPackageObject(package);
            var settingValue = (configurationPackage.Settings.Sections[sectionName].Parameters[settingName]?.Value) ?? defaultValue;
            return settingValue;
        }
    }
}
