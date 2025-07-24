using com.vrcfury.api.Actions;
using JetBrains.Annotations;
using UnityEngine;
using VF.Model;
using VF.Model.Feature;

namespace com.vrcfury.api.Components {
    /** Create an instance using <see cref="FuryComponents"/> */
    [PublicAPI]
    public class FuryToggle {
        private readonly Toggle c;

        internal FuryToggle(GameObject obj) {
            var vf = obj.AddComponent<VRCFury>();
            c = new Toggle();
            vf.content = c;
        }

        public void SetMenuPath(string path) {
            c.name = path;
        }

        public void SetMenuIcon(Texture2D icon) {
            c.enableIcon = true;
            c.icon = icon;
        }

        public void SetSlider(bool slider = true) {
            c.slider = slider;
        }

        public void SetDefaultOn() {
            c.defaultOn = true;
        }

        public void SetSaved() {
            c.saved = true;
        }
        
        public void SetExclusiveOffState() {
            c.exclusiveOffState = true;
        }

        public void AddExclusiveTag(string tag) {
            c.enableExclusiveTag = true;
            if (!string.IsNullOrEmpty(c.exclusiveTag)) c.exclusiveTag += ",";
            c.exclusiveTag += tag;
        }

        public void SetGlobalParameter(string globalParameter) {
            c.useGlobalParam = true;
            c.globalParam = globalParameter;
        }

        public FuryActionSet GetActions() {
            return new FuryActionSet(c.state);
        }
    }
}
