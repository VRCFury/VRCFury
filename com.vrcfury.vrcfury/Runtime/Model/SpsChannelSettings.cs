using System;
using System.Collections.Generic;

namespace VF.Model {
    [Serializable]
    internal class SpsChannelSettings {
        public bool useSharedTag = true;
        public bool useHipAvoidance = true;
        public List<Channel> channels = new();

        [Serializable]
        public class Channel {
            public string id;
            public bool allowSelf = true;
            public bool allowOthers = true;
        }
    }
}
