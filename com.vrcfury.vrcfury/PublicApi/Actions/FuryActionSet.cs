using UnityEngine;
using VF.Model;
using VF.Model.StateAction;

namespace com.vrcfury.api.Actions {
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
    }
}
