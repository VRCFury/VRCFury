using System;
using System.Collections.Generic;
using JetBrains.Annotations;
using UnityEngine;
using UnityEngine.Animations;
using VF.Injector;
using VF.Utils;
using VF.Utils.Controller;
using VRC.Dynamics;
using VRC.SDK3.Dynamics.Contact.Components;
using Random = UnityEngine.Random;

namespace VF.Service {
    /**
     * "Fixes" https://feedback.vrchat.com/bug-reports/p/scalefactor-is-not-synchronized-to-late-joiners-or-existing-players-in-newly-joi
     * by placing a contact receiver at an upload-scale-normalized local offset, then using the synced proximity
     * value to reconstruct the target's live world scale.
     */
    [VFService]
    internal class WorldScaleDetectorService {
        [VFAutowired] private readonly VFGameObject avatarObject;
        [VFAutowired] private readonly ControllersService controllers;
        private ControllerManager fx => controllers.GetFx();
        [VFAutowired] private readonly OverlappingContactsFixService overlappingService;
        [VFAutowired] private readonly DbtLayerService dbtLayerService;
        [VFAutowired] private readonly IsObjectEnabledService isObjectEnabledService;

        private const string Tag = "VRCF_WSD";
        private const float ContactRadius = 1f;
        private const float MaxDetectedScale = 100f;

        private readonly Lazy<VFBlendTreeDirect> directTree;
        private readonly Lazy<BlendtreeMath> math;
        private readonly Lazy<Vector3> offset;
        private readonly Lazy<VFGameObject> senderObject;
        private readonly Dictionary<VFGameObject, VFAFloat> cache = new Dictionary<VFGameObject, VFAFloat>();

        public WorldScaleDetectorService() {
            directTree = new Lazy<VFBlendTreeDirect>(() => dbtLayerService.Create());
            math = new Lazy<BlendtreeMath>(() => dbtLayerService.GetMath(directTree.Value));
            offset = new Lazy<Vector3>(() => new Vector3(
                -30f + (Random.value * 60f),
                10f + (Random.value * 30f),
                -30f + (Random.value * 60f)
            ));
            senderObject = new Lazy<VFGameObject>(CreateSender);
        }

        [CanBeNull]
        // Returns the live absolute world scale of localSpace.
        public VFAFloat GetWorldScale(VFGameObject localSpace, string name) {
            return cache.GetOrCreate(localSpace, () => Create(localSpace, name));
        }

        [CanBeNull]
        private VFAFloat Create(VFGameObject localSpace, string name) {
            if (!BuildTargetUtils.IsDesktop()) {
                return null;
            }

            var outerObj = GameObjects.Create(name, senderObject.Value.parent);
            outerObj.localPosition = new Vector3(-ContactRadius, 0, 0);
            outerObj.active = false;
            var constraint = outerObj.AddComponent<ScaleConstraint>();
            constraint.AddSource(new ConstraintSource {
                sourceTransform = localSpace,
                weight = 1
            });
            constraint.weight = 1;
            constraint.constraintActive = true;
            constraint.locked = true;

            overlappingService.Activate();

            var innerObj = GameObjects.Create("Inner", outerObj);
            innerObj.localPosition =  new Vector3(ContactRadius / MaxDetectedScale, 0, 0);
            ConstraintUtils.MakeWorldSpace(innerObj);

            var localReceiver = innerObj.AddComponent<VRCContactReceiver>();
            localReceiver.allowOthers = false;
            localReceiver.receiverType = ContactReceiver.ReceiverType.Proximity;
            localReceiver.collisionTags.Add(Tag);
            localReceiver.radius = ContactRadius;
            var receiverParam = fx.NewFloat($"WSD - {name} - Rcv", def: 1 / MaxDetectedScale);
            localReceiver.parameter = receiverParam;

            var receiverOn = VFClip.Create($"WSD - {name} - On");
            receiverOn.SetEnabled(outerObj, true);
            directTree.Value.Add(BlendtreeMath.GreaterThan(isObjectEnabledService.Get(localSpace), 0)
                .create(receiverOn, null));

            return math.Value.Multiply($"WSD - {name} - Final", receiverParam, MaxDetectedScale * localSpace.worldScale.x);
        }

        private VFGameObject CreateSender() {
            var worldAnchor = GameObjects.Create("World Scale Detector", avatarObject);
            worldAnchor.localPosition = offset.Value;
            worldAnchor.worldRotation = Quaternion.identity;
            worldAnchor.worldScale = Vector3.one;
            ConstraintUtils.MakeWorldSpace(worldAnchor);

            var obj = GameObjects.Create("Sender", worldAnchor);
            obj.localPosition = new Vector3(ContactRadius, 0, 0);

            var sender = obj.AddComponent<VRCContactSender>();
            sender.radius = ContactRadius;
            sender.collisionTags.Add(Tag);
            return obj;
        }
    }
}
