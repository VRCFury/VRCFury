using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using UnityEditor;
using UnityEngine;
using UnityEngine.Animations;
using UnityEngine.UIElements;
using VF.Builder;
using VF.Feature.Base;
using VF.Injector;
using VF.Inspector;
using VF.Model.Feature;
using VF.Service;
using VF.Utils;
using VRC.Dynamics;
using VRC.SDK3.Dynamics.Constraint.Components;

namespace VF.Feature {
    [FeatureTitle("Anchor Override Fix")]
    [FeatureOnlyOneAllowed]
    [FeatureRootOnly]
    internal class AnchorOverrideFixBuilder : FeatureBuilder<AnchorOverrideFix2> {
        [VFAutowired] private readonly VFGameObject avatarObject;
        [VFAutowired] private readonly ControllersService controllers;

        [FeatureBuilderAction(FeatureOrder.AnchorOverrideFix)]
        public void Apply() {
            VFGameObject root;
            try {
                root = VRCFArmatureUtils.FindBoneOnArmatureOrException(avatarObject, HumanBodyBones.Chest);
            } catch (Exception) {
                root = VRCFArmatureUtils.FindBoneOnArmatureOrException(avatarObject, HumanBodyBones.Hips);
            }

            var worldAnimatedObjs = controllers.GetAllUsedControllers()
                .SelectMany(c => c.GetClips())
                .SelectMany(clip => clip.GetAllBindings())
                .Where(b => b.propertyName == "FreezeToWorld")
                .Select(b => b.path)
                .Select(path => avatarObject.Find(path))
                .ToImmutableHashSet();
            foreach (var skin in avatarObject.GetComponentsInSelfAndChildren<Renderer>()) {
                if (skin.owner().GetComponentInSelfOrParent<Rigidbody>() != null) {
                    continue;
                }
                if (skin.owner().GetConstraints(includeParents: true).Any(c => IsWorldConstraint(c, worldAnimatedObjs))) {
                    continue;
                }
                skin.probeAnchor = root;
            }
        }

        private static bool IsWorldConstraint(VFConstraint c, ISet<VFGameObject> worldAnimatedObjs) {
            if (c.GetComponent() is VRCConstraintBase vc) {
                if (vc.FreezeToWorld) return true;
                if (worldAnimatedObjs.Contains(c.GetComponent().owner())) return true;
            }
            if (c.GetSources().Any(o => o.IsAssetTransform())) {
                return true;
            }
            return false;
        }
        
        [FeatureEditor]
        public static VisualElement Editor() {
            var content = new VisualElement();
            content.Add(VRCFuryEditorUtils.Info(
                "This feature will set the anchor override for every mesh on your avatar to your chest. " +
                "This will prevent different meshes from being lit differently on your body."));
            return content;
        }
    }
}
