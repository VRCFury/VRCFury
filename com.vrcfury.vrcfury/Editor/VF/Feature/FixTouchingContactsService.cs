using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using UnityEngine;
using VF.Builder;
using VF.Feature.Base;
using VF.Injector;
using VRC.SDK3.Dynamics.Contact.Components;

namespace VF.Feature {
    /**
     * When you first load into a world, contact receivers already touching a sender register as 0 proximity
     * until they are removed and then reintroduced to each other.
     */
    [VFService]
    public class FixTouchingContactsService : FeatureBuilder {
        private readonly List<VRCContactReceiver> receivers = new List<VRCContactReceiver>();
        private readonly List<Transform> objectsToForceEnable = new List<Transform>();
        [VFAutowired] private readonly AvatarManager avatarManager;

        public void Add(VRCContactReceiver receiver) {
            receivers.Add(receiver);
        }
        
        public void ForceEnable(Transform t) {
            objectsToForceEnable.Add(t);
        }

        [FeatureBuilderAction(FeatureOrder.FixTouchingContacts)]
        public void Apply() {
            var disableForOneFrame = receivers
                .Where(r => r != null)
                .Select(r => r.transform)
                .ToImmutableHashSet();
            var forceEnable = objectsToForceEnable
                .Where(o => o != null)
                .ToImmutableHashSet();
            if (disableForOneFrame.Count == 0 && forceEnable.Count == 0) return;

            var fx = avatarManager.GetFx();
            var layer = fx.NewLayer("Contacts Off Temporarily Upon Load");
            var off = layer.NewState("Off");
            var on = layer.NewState("On");
            off.TransitionsTo(on).When().WithTransitionExitTime(1);
            
            var firstFrameClip = fx.NewClip("Load (First Frame)");
            foreach (var obj in disableForOneFrame) {
                clipBuilder.Enable(firstFrameClip, obj.gameObject, false);
            }
            off.WithAnimation(firstFrameClip);
            
            var onClip = fx.NewClip("Load (On)");
            foreach (var obj in forceEnable) {
                clipBuilder.Enable(onClip, obj.gameObject);
            }
            on.WithAnimation(onClip);
        }
    }
}
