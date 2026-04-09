using System;
using System.Collections.Generic;
using UnityEngine;

namespace VF.Model.Feature {
    [Serializable]
    internal class BlendShapeLink : NewFeatureModel {
        [Obsolete] public List<GameObject> objs = new List<GameObject>();
        public List<LinkSkin> linkSkins = new List<LinkSkin>();
        public string baseObj;
        public bool includeAll = true;
        public bool exactMatch = false;
        public List<Exclude> excludes = new List<Exclude>();
        public List<Include> includes = new List<Include>();

        [Serializable]
        public class LinkSkin {
            public SkinnedMeshRenderer renderer;
        }
        [Serializable]
        public class Exclude {
            public string name;
        }
        [Serializable]
        public class Include {
            public string nameOnBase;
            public string nameOnLinked;
        }
        
        public override bool Upgrade(int fromVersion) {
#pragma warning disable 0612
            if (fromVersion < 1) {
                linkSkins.Clear();
                foreach (var obj in objs) {
                    if (obj != null) {
                        linkSkins.Add(new LinkSkin { renderer = obj.GetComponent<SkinnedMeshRenderer>() });
                    }
                }
            }
            return false;
#pragma warning restore 0612
        }

        public override int GetLatestVersion() {
            return 1;
        }
    }
}