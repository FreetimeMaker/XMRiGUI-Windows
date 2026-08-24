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

            try
            {
                string json = File.ReadAllText(filePath);
                return JsonConvert.DeserializeObject<XmriguiConfig>(json);
            }
            catch
            {
                return new XmriguiConfig();
            }
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
