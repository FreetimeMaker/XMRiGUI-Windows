using System;
using System.IO;
using Newtonsoft.Json;
using XMRiGUI.Models;

namespace XMRiGUI.Services
{
    public class ConfigService
    {
        private string ConfigFilePath => Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
            "XMRiGUI",
            "xmrigui.json");

        public XmriguiConfig LoadConfig()
        {
            string filePath = ConfigFilePath;
            if (!File.Exists(filePath))
            {
                return new XmriguiConfig();
            }

            string json = File.ReadAllText(filePath);
            var config = JsonConvert.DeserializeObject<XmriguiConfig>(json, new JsonSerializerSettings
            {
                ObjectCreationHandling = ObjectCreationHandling.Replace
            }) ?? throw new InvalidDataException("The settings file is empty.");
            config.profiles ??= new System.Collections.Generic.List<ProfileConfig>();
            // Older versions appended deserialized entries to the defaults. Keep any extra entries on save.
            for (int i = 0; i < 3; i++)
            {
                var fallback = new ProfileConfig { name = $"Profile {i + 1}" };
                if (i == config.profiles.Count) config.profiles.Add(fallback);
                else config.profiles[i] ??= fallback;
            }
            return config;
        }

        public void SaveConfig(XmriguiConfig config)
        {
            string filePath = ConfigFilePath;
            string directory = Path.GetDirectoryName(filePath);
            if (!Directory.Exists(directory)) Directory.CreateDirectory(directory);

            string json = JsonConvert.SerializeObject(config, Formatting.Indented);
            File.WriteAllText(filePath, json);
        }
    }
}
