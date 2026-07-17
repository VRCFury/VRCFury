using System;
using System.Collections.Generic;
using System.Text.RegularExpressions;
using Microsoft.Win32;

namespace VF.Inspector {
    internal class VrcRegistryConfig {
        private readonly Dictionary<string, int> data = new Dictionary<string, int>();
        public VrcRegistryConfig() {
            try {
                RegistryKey root = Registry.CurrentUser.OpenSubKey("Software\\VRChat\\VRChat");
                if (root == null) return;
                foreach (var subkey in root.GetValueNames()) {
                    var clean = Regex.Replace(subkey, "_h[0-9]+", "").ToLower();
                    if (root.GetValue(subkey) is int num) data[clean] = num;
                }
            } catch (Exception) {
            }
        }

        public bool TryGet(string key, out int value) {
            return data.TryGetValue(key.ToLower(), out value);
        }
    }
}
