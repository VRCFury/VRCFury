using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using UnityEngine;
using VF.Builder;
using VF.Feature.Base;
using VF.Injector;
using VRC.SDK3.Avatars.Components;
using VRC.SDK3.Dynamics.Contact.Components;
using VRC.SDKBase;

namespace VF.Feature {
    /**
     * When you first load into a world, contact receivers already touching a sender register as 0 proximity
     * until they are removed and then reintroduced to each other.
     */
    [VFService]
    public class ForceStateInAnimatorService : FeatureBuilder {
        [VFAutowired] private readonly AvatarManager avatarManager;
        private readonly List<Transform> _disableDuringLoad = new List<Transform>();
        private readonly List<Transform> _forceEnable = new List<Transform>();
        private readonly List<Transform> _forceEnableLocal = new List<Transform>();

        public void DisableDuringLoad(Transform receiver) {
            _disableDuringLoad.Add(receiver);
        }
        
        public void ForceEnable(Transform t) {
            _forceEnable.Add(t);
        }
        
        public void ForceEnableLocal(Transform t) {
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
            VFAState.FakeAnyState(
                (onLocal, fx.IsLocal().IsTrue()),
                (on, VFACondition.Never()),
                (onRemote, null)
            );

            var firstFrameClip = fx.NewClip("Load (First Frame)");
            foreach (var obj in disableDuringLoad) {
                clipBuilder.Enable(firstFrameClip, obj.gameObject, false);
            }
            load.WithAnimation(firstFrameClip);
            
            var onLocalClip = fx.NewClip("Idle (Local)");
            var onRemoteClip = fx.NewClip("Idle (Remote)");
            foreach (var obj in forceEnable) {
                clipBuilder.Enable(onLocalClip, obj.gameObject);
                clipBuilder.Enable(onRemoteClip, obj.gameObject);
            }
            foreach (var obj in forceEnableLocal) {
                clipBuilder.Enable(onLocalClip, obj.gameObject);
            }
            onLocal.WithAnimation(onLocalClip);
            onRemote.WithAnimation(onRemoteClip);
        }
    }
}
