using System;
using System.Collections.Generic;
using JetBrains.Annotations;
using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;
using UnityEngine.Animations;
using VF.Builder;
using VF.Feature;
using VF.Injector;
using VF.Inspector;
using VF.Utils;
using VF.Utils.Controller;
using VRC.Dynamics;
using VRC.SDK3.Avatars.Components;
using VRC.SDK3.Dynamics.Contact.Components;
using VRC.SDKBase;

namespace VF.Service {
    /**
     * "Fixes" https://feedback.vrchat.com/bug-reports/p/scalefactor-is-not-synchronized-to-late-joiners-or-existing-players-in-newly-joi
     * by using local ScaleFactor to move a contact receiver, then syncing that contact receiver's proximity value
     */
    [VFService]
    internal class ScaleFactorService {
        [VFAutowired] private readonly AvatarManager manager;
        [VFAutowired] private readonly MathService math;
        [VFAutowired] private readonly ForceStateInAnimatorService forceStateInAnimatorService;
        [VFAutowired] private readonly ClipBuilderService clipBuilder;
        private ControllerManager fx => manager.GetFx();

        private int scaleIndex = 0;

        private readonly Dictionary<VFGameObject, (VFGameObject, VFAFloat)> cache =
            new Dictionary<VFGameObject, (VFGameObject, VFAFloat)>();

        [CanBeNull]
        public VFAFloat Get(VFGameObject parent) {
            return GetAdv(parent)?.Item2;
        }

        [CanBeNull]
        public (VFGameObject, VFAFloat)? GetAdv(VFGameObject parent) {
            if (EditorUserBuildSettings.activeBuildTarget == BuildTarget.Android) {
                return null;
            }

            if (cache.TryGetValue(parent, out var cached)) return cached;

            var senderObj = GameObjects.Create("LocalScale", parent);
            senderObj.worldScale = Vector3.one;
            var sender = senderObj.AddComponent<VRCContactSender>();
            sender.radius = 0.001f;
            var tag = $"VRCF_SCALEFACTORFIX_AA_{scaleIndex++}";
            sender.collisionTags.Add(tag);

            var receiverObj = GameObjects.Create("WorldScale", parent);
            receiverObj.worldScale = Vector3.one;
            var receiver = receiverObj.AddComponent<VRCContactReceiver>();
            receiver.allowOthers = false;
            receiver.receiverType = ContactReceiver.ReceiverType.Proximity;
            receiver.collisionTags.Add(tag);
            receiver.radius = 0.1f;
            receiver.position = new Vector3(0.1f, 0, 0);
            var receiverParam = fx.NewFloat($"SFFix {parent.name} - Rcv");
            receiver.parameter = receiverParam;
            var p = receiverObj.AddComponent<ScaleConstraint>();
            p.AddSource(new ConstraintSource() {
                sourceTransform = VRCFuryEditorUtils.GetResource<Transform>("world.prefab"),
                weight = 1
            });
            p.weight = 1;
            p.constraintActive = true;
            p.locked = true;

            var final = math.Multiply($"SFFix {parent.name} - Final", receiverParam, 100);
            
            return cache[parent] = (receiverObj, final);
        }
    }
}
