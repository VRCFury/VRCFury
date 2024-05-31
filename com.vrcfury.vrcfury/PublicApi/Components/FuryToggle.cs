using com.vrcfury.api.Actions;
using UnityEngine;
using VF.Model;
using VF.Model.Feature;

namespace com.vrcfury.api.Components {
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

        public void SetSlider(bool slider = true) {
            c.slider = slider;
        }

        public FuryActionSet GetActions() {
            return new FuryActionSet(c.state);
        }
    }
}
