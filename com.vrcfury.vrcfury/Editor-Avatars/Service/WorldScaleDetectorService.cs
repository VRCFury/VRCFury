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
     * by placing a contact receiver at an upload-scale-normalized local offset, then using its proximity
     * value to reconstruct the target's live world scale.
     */
    [VFService]
    internal class WorldScaleDetectorService {
        [VFAutowired] private readonly VFGameObject avatarObject;
        [VFAutowired] private readonly ControllersService controllers;
        private VFAvatarController fx => controllers.GetFx();
        [VFAutowired] private readonly OverlappingContactsFixService overlappingService;
        [VFAutowired] private readonly DbtLayerService dbtLayerService;
        [VFAutowired] private readonly IsObjectEnabledService isObjectEnabledService;
        [VFAutowired] private readonly ObjectTreeService objectTrees;

        private const string Tag = "VRCF_WSD";
        private const float ContactRadius = 1f;
        internal const float MaxDetectedScale = 100f;

        private readonly Lazy<VFBlendTreeDirect> directTree;
        private readonly Lazy<BlendtreeMath> math;
        private readonly Lazy<Vector3> offset;
        private readonly Lazy<VFGameObject> senderObject;
        private readonly Dictionary<VFGameObject, (VFAParam raw, Lazy<VFAParam> scale, Lazy<VFAParam> inverse)> cache = new();

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

        // Applies local property values using raw scale, without an intermediate AAP.
        public void AddScaledProp(
            VFGameObject scaleReference,
            IList<(UnityEngine.Component component, string PropertyName, float LocalValue)> properties,
            VFBlendTreeDirect tree = null
        ) {
            var rawScale = GetRawWorldScale(scaleReference, "Property scale compensation");
            if (rawScale == null) return;
            var multiplier = MaxDetectedScale * scaleReference.worldScale.x;
            tree ??= objectTrees.Get(scaleReference);
            // Prevent Unity from adding authored defaults when the raw weight is below one.
            var zeroClip = VFClip.Create("Scale compensation zero");
            tree.Add(zeroClip);
            var scaleClip = VFClip.Create("Scale compensation scaled");
            tree.Add(rawScale, scaleClip);
            foreach (var prop in properties) {
                scaleClip.SetCurve(prop.component, prop.PropertyName, prop.LocalValue * multiplier);
                zeroClip.SetCurve(prop.component, prop.PropertyName, 0);
            }
        }

        [CanBeNull]
        // Returns the live absolute world scale of localSpace.
        // An explicit receiver parameter name must be supplied on the first request.
        public VFAParam GetWorldScale(VFGameObject localSpace, string name, string parameterName = null) {
            return GetDetector(localSpace, name, parameterName).scale?.Value;
        }

        [CanBeNull]
        public VFAParam GetRawWorldScale(VFGameObject localSpace, string name, string parameterName = null) {
            return GetDetector(localSpace, name, parameterName).raw;
        }

        [CanBeNull]
        public VFAParam GetInverseWorldScale(VFGameObject localSpace, string name) {
            return GetDetector(localSpace, name).inverse?.Value;
        }

        private (VFAParam raw, Lazy<VFAParam> scale, Lazy<VFAParam> inverse) GetDetector(
            VFGameObject localSpace, string name, string parameterName = null) =>
            cache.GetOrCreate(localSpace, () => Create(localSpace, name, parameterName));

        private (VFAParam raw, Lazy<VFAParam> scale, Lazy<VFAParam> inverse) Create(
            VFGameObject localSpace, string name, string parameterName) {
            if (!BuildTargetUtils.IsDesktop()) {
                return default;
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
            innerObj.localPosition = new Vector3(ContactRadius / (MaxDetectedScale * localSpace.worldScale.x), 0, 0);
            ConstraintUtils.MakeWorldSpace(innerObj);

            var localReceiver = innerObj.AddComponent<VRCContactReceiver>();
            localReceiver.allowOthers = false;
            localReceiver.receiverType = ContactReceiver.ReceiverType.Proximity;
            localReceiver.collisionTags.Add(Tag);
            localReceiver.radius = ContactRadius;
            var receiverParam = parameterName == null
                ? fx.CreateUniqueFloat($"WSD - {name} - Rcv", 1 / MaxDetectedScale)
                : fx.GetOrCreateFloat(parameterName, 1 / MaxDetectedScale);
            localReceiver.parameter = receiverParam;

            var receiverOn = VFClip.Create($"WSD - {name} - On");
            receiverOn.SetEnabled(outerObj, true);
            directTree.Value.Add(BlendtreeMath.GreaterThan(isObjectEnabledService.Get(localSpace), 0)
                .create(receiverOn, null));

            var multiplier = MaxDetectedScale * localSpace.worldScale.x;
            return (
                receiverParam,
                new Lazy<VFAParam>(() => math.Value.Multiply($"WSD - {name} - Final", receiverParam, multiplier)),
                new Lazy<VFAParam>(() => math.Value.Invert($"WSD - {name} - Inverse", receiverParam, multiplier))
            );
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
