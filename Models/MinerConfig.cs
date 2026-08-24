using System;
using System.Collections.Generic;

namespace XMRiGUI.Models
{
    public class XmriguiConfig
    {
        public List<ProfileConfig> profiles { get; set; } = new List<ProfileConfig>
        {
            new ProfileConfig { name = "Profil 1" },
            new ProfileConfig { name = "Profil 2" },
            new ProfileConfig { name = "Profil 3" }
        };
        public bool start_with_windows { get; set; } = false;
        public bool minimize_to_tray { get; set; } = true;
        public bool close_to_tray { get; set; } = true;
    }

    public class ProfileConfig
    {
        public string name { get; set; } = "Profil";
        public bool enabled { get; set; } = false;
        public string pool { get; set; } = "pool.hashvault.pro:443";
        public string wallet { get; set; } = "";
        public string password { get; set; } = "x";
        public int donate { get; set; } = 1;
        public int threads { get; set; } = 0;
        public string algo { get; set; } = "rx/0";
        public bool cpu { get; set; } = true;
        public bool cuda { get; set; } = false;
        public bool opencl { get; set; } = false;
        public bool tls { get; set; } = true;
    }
}
