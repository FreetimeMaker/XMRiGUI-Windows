using System;
using System.Collections.Generic;

namespace XMRiGUI.Models
{
    public class XmriguiConfig
    {
        public List<ProfileConfig> profiles { get; set; } = new List<ProfileConfig>
        {
            new ProfileConfig { name = "Profile 1" },
            new ProfileConfig { name = "Profile 2" },
            new ProfileConfig { name = "Profile 3" }
        };
        public bool start_with_windows { get; set; } = false;
        public bool minimize_to_tray { get; set; } = true;
        public bool close_to_tray { get; set; } = true;
    }

    public class ProfileConfig
    {
        public string name { get; set; } = "Profile";
        public bool enabled { get; set; } = false;
        public string pool { get; set; } = "pool.supportxmr.com:3333";
        public string wallet { get; set; } = "49szz88CqMWGgyDxp7VqvBS62pGLQcV4YPSBHcLwtxAXLz1Wngf8vW6is4w13Au7C2RovrTiJQaGDV5VBhFnyMBsM44Pn2P";
        public string password { get; set; } = "Donate";
        public int donate { get; set; } = 1;
        public int threads { get; set; } = 0;
        public string algo { get; set; } = "rx/0";
        public bool cpu { get; set; } = true;
        public bool cuda { get; set; } = false;
        public bool opencl { get; set; } = false;
        public bool tls { get; set; } = true;
        public bool auto_restart { get; set; }
        public bool default_args { get; set; }
        public string args { get; set; } = "";

        public ProfileConfig Copy() => (ProfileConfig)MemberwiseClone();
    }
}
