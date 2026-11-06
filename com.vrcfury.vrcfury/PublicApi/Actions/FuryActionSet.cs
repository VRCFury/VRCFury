using JetBrains.Annotations;
using UnityEngine;
using VF.Model;
using VF.Model.StateAction;

namespace com.vrcfury.api.Actions {
    [PublicAPI]
    public class FuryActionSet {
        private readonly State s;

        internal FuryActionSet(State s) {
            this.s = s;
        }
        
        public void AddTurnOn(GameObject obj) {
            s.actions.Add(new ObjectToggleAction() {
                obj = obj
            });
        }
        
        public void AddAnimationClip(AnimationClip clip) {
            s.actions.Add(new AnimationClipAction() {
                clip = clip
            });
        }
        
        public FuryFlipbookBuilder AddFlipbookBuilder() {
            var a = new FlipBookBuilderAction();
            s.actions.Add(a);
            return new FuryFlipbookBuilder(a);
        }

        public void AddBlendshape(string name, float value, Renderer renderer = null) {
            var a = new BlendShapeAction();
            if (renderer != null) {
                a.allRenderers = false;
                a.renderer = renderer;
            }
            a.blendShape = name;
            a.blendShapeValue = value;
            s.actions.Add(a);
        }

        /**
         * Sets an Animator Animated Parameter to the specified value when the toggle is on
         * Note: When an AAP is controlled from a controller like this, it can no longer be
         * controlled by VRChat or seen in VRChat (menu or OSC). It essentially becomes
         * detached, accessible only within the controller.
         */
        public void AddAap(string name, float value) {
            var a = new FxFloatAction();
            a.name = name;
            a.value = value;
            s.actions.Add(a);
        }

        private MaterialPropertyAction CreateMaterialPropertyAction(string propertyName, GameObject renderer) {
            var m = new MaterialPropertyAction();
            if (renderer != null) {
                m.affectAllMeshes = false;
                m.renderer2 = renderer;
            } else {
                m.affectAllMeshes = true;
            }
            m.propertyName = propertyName;
            s.actions.Add(m);
            return m;
        }

        public void AddMaterialProperty(string propertyName, float value, GameObject renderer = null) {
            var m = CreateMaterialPropertyAction(propertyName, renderer);
            m.value = value;
        }

        public void AddMaterialProperty(string propertyName, Vector4 value, GameObject renderer = null) {
            var m = CreateMaterialPropertyAction(propertyName, renderer);
            m.valueVector = value;
        }

        public void AddMaterialProperty(string propertyName, Color value, GameObject renderer = null) {
            var m = CreateMaterialPropertyAction(propertyName, renderer);
            m.valueColor = value;
        }
    }
}
