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
    }
}
