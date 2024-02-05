using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using VF.Builder;
using VF.Feature.Base;
using VF.Injector;
using VF.Utils.Controller;

namespace VF.Feature {
    /**
     * When you first load into a world, contact receivers already touching a sender register as 0 proximity
     * until they are removed and then reintroduced to each other.
     */
    [VFService]
    public class ForceStateInAnimatorService : FeatureBuilder {
        [VFAutowired] private readonly AvatarManager avatarManager;
        private readonly List<VFGameObject> _disableDuringLoad = new List<VFGameObject>();
        private readonly List<VFGameObject> _forceEnable = new List<VFGameObject>();
        private readonly List<VFGameObject> _forceEnableLocal = new List<VFGameObject>();

        public void DisableDuringLoad(VFGameObject receiver) {
            _disableDuringLoad.Add(receiver);
        }
        
        public void ForceEnable(VFGameObject t) {
            _forceEnable.Add(t);
        }
        
        public void ForceEnableLocal(VFGameObject t) {
            _forceEnableLocal.Add(t);
        }

        [FeatureBuilderAction(FeatureOrder.FixTouchingContacts)]
        public void Apply() {
            var disableDuringLoad = _disableDuringLoad
                .Where(r => r != null)
                .ToImmutableHashSet();
            var forceEnable = this._forceEnable
                .Where(o => o != null)
                .ToImmutableHashSet();
            var forceEnableLocal = this._forceEnableLocal
                .Where(o => o != null)
                .ToImmutableHashSet();
            if (disableDuringLoad.Count == 0 && forceEnable.Count == 0 && forceEnableLocal.Count == 0) return;

            var fx = avatarManager.GetFx();
            var layer = fx.NewLayer("Force State");
            var load = layer.NewState("Load");
            var on = layer.NewState("On");
            var onLocal = layer.NewState("On (Local)");
            var onRemote = layer.NewState("On (Remote)");
            
            load.TransitionsTo(on).When().WithTransitionExitTime(1);
            VFState.FakeAnyState(
                (onLocal, fx.IsLocal().IsTrue()),
                (on, VFCondition.Never()),
                (onRemote, null)
            );

            var firstFrameClip = fx.NewClip("Load (First Frame)");
            foreach (var obj in disableDuringLoad) {
                clipBuilder.Enable(firstFrameClip, obj, false);
            }
            load.WithAnimation(firstFrameClip);
            
            var onLocalClip = fx.NewClip("Idle (Local)");
            var onRemoteClip = fx.NewClip("Idle (Remote)");
            foreach (var obj in forceEnable) {
                clipBuilder.Enable(onLocalClip, obj);
                clipBuilder.Enable(onRemoteClip, obj);
            }
            foreach (var obj in forceEnableLocal) {
                clipBuilder.Enable(onLocalClip, obj);
            }
            onLocal.WithAnimation(onLocalClip);
            onRemote.WithAnimation(onRemoteClip);
        }
    }
}
