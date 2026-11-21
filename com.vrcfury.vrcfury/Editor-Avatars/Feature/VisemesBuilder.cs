using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;
using VF.Builder;
using VF.Feature.Base;
using VF.Injector;
using VF.Inspector;
using VF.Model;
using VF.Model.Feature;
using VF.Model.StateAction;
using VF.Service;
using VF.Utils;
using VF.Utils.Controller;
using VRC.SDK3.Avatars.Components;
using VRC.SDKBase;

namespace VF.Feature {

    [FeatureTitle("Advanced Visemes")]
    internal class VisemesBuilder : FeatureBuilder<Visemes> {
        private abstract class Reflection : ReflectionHelper {
            public static readonly FieldInfo[] StateFields = Enumerable.Range(0, visemeNames.Length)
                .Select(i => "state_" + (i == 0 ? "aa" : visemeNames[i]))
                .Select(name => typeof(Visemes).VFField(name))
                .ToArray();
        }

        [VFAutowired] private readonly TrackingConflictResolverService trackingConflictResolverService;
        [VFAutowired] private readonly ActionClipService actionClipService;
        [VFAutowired] private readonly SmoothingService smooth;
        [VFAutowired] private readonly ClipFactoryService clipFactory;
        [VFAutowired] private readonly VRCAvatarDescriptor avatar;
        [VFAutowired] private readonly VFGameObject avatarObject;
        [VFAutowired] private readonly ControllersService controllers;
        [VFAutowired] private readonly DbtLayerService dbtLayerService;
        [VFAutowired] private readonly GlobalsService globals;
#if VRCSDK_HAS_FACE_TRACKING
        [VFAutowired] private readonly FaceTrackingService faceTracking;
#endif
        private VFAvatarController fx => controllers.GetFx();

        private static readonly string[] visemeNames = {
            "sil", "PP", "FF", "TH", "DD", "kk", "CH", "SS", "nn", "RR", "aa", "E", "I", "O", "U"
        };
        
        [FeatureBuilderAction]
        public void Apply() {
#if VRCSDK_HAS_FACE_TRACKING
            if (!MigrateBlendshapeVisemesToFaceTracking()) return;
#endif
            var isFaceTrackingVisemes = false;
#if VRCSDK_HAS_FACE_TRACKING
            isFaceTrackingVisemes = avatar.lipSync == VRC_AvatarDescriptor.LipSyncStyle.VisemeBlendShapeFaceTracking;
#endif
            if (avatar.lipSync != VRC_AvatarDescriptor.LipSyncStyle.VisemeBlendShape
                && !isFaceTrackingVisemes) {
                avatar.lipSync = VRC_AvatarDescriptor.LipSyncStyle.VisemeParameterOnly;
            }

            var visemeParam = fx.GetOrCreateFloat("Viseme");
            var volumeParam = fx.GetOrCreateFloat("Voice");
            var enabled = fx.CreateUniqueFloat("AdvancedVisemesEnabled", def: 1);
            var directTree = dbtLayerService.Create();
            var motions = visemeNames.Select((name, index) => {
                var clipState = (State)Reflection.StateFields[index].GetValue(model);
                return actionClipService.LoadState("Viseme " + name, clipState).EvaluateMotion(1);
            }).ToArray();

            VFMotion voiceMotion;
            if (model.instant) {
                var lookup = CreateLookup("Select viseme", visemeParam, motions);
                voiceMotion = BlendtreeMath.GreaterThan(volumeParam, 0.1f, name: "Instant volume")
                    .create(lookup, clipFactory.GetEmptyClip());
            } else {
                var scaledVolume = fx.CreateUniqueFloat("ScaledVolume");
                directTree.Add(VFBlendTree1D.CreateWithData(
                    "ScaledVolume",
                    volumeParam,
                    (0, scaledVolume.MakeSetter(0)),
                    (0.05f, scaledVolume.MakeSetter(0f)),
                    (0.15f, scaledVolume.MakeSetter(0.8f)),
                    (1f, scaledVolume.MakeSetter(1f))
                ));
                var volumeToUse = smooth.Smooth(directTree, "SmoothedVolume", scaledVolume, 0.07f, false);

                var voiceTree = VFBlendTreeDirect.Create("Advanced Visemes");
                var weights = new VFMotion[motions.Length];
                for (var index = 0; index < motions.Length; index++) {
                    var name = visemeNames[index];
                    var intensityRaw = fx.CreateUniqueFloat($"{name}Raw", def: visemeParam.GetDefault() == index ? 1 : 0);
                    // The layer's AAP zero clip clears unselected weights before smoothing.
                    weights[index] = intensityRaw.MakeSetter(1);
                    var intensity = smooth.Smooth(directTree, $"{name}Smooth", intensityRaw, 0.05f, false);
                    voiceTree.Add(intensity, motions[index]);
                }
                directTree.Add(CreateLookup("Viseme weights", visemeParam, weights));
                var volumeTree = VFBlendTreeDirect.Create("Advanced Visemes");
                volumeTree.Add(volumeToUse, voiceTree);
                voiceMotion = volumeTree;
            }
            directTree.Add(enabled, voiceMotion);

            trackingConflictResolverService.WhenCollected(() => {
                var inhibitors =
                    trackingConflictResolverService.GetInhibitors(TrackingConflictResolverService.TrackingMouth);

                var enabledWhen = BlendtreeMath.True();
                foreach (var inhibitor in inhibitors) {
                    enabledWhen = enabledWhen.And(BlendtreeMath.LessThan(inhibitor, 0, true));
                }

                directTree.Add(enabledWhen.create(
                    enabled.MakeSetter(1),
                    clipFactory.GetEmptyClip()
                ));
            });
        }

        private VFMotion CreateLookup(string name, VFAParam visemeParam, IReadOnlyList<VFMotion> motions) {
            var lookup = VFBlendTree1D.Create(name, visemeParam);
            var noViseme = clipFactory.GetEmptyClip();
            for (var index = 0; index < motions.Count; index++) {
                // Adjacent float stops preserve exact selection between viseme indices.
                lookup.Add(VRCFuryEditorUtils.NextFloatDown(index), noViseme);
                lookup.Add(index, motions[index]);
                lookup.Add(VRCFuryEditorUtils.NextFloatUp(index), noViseme);
            }
            return lookup;
        }

#if VRCSDK_HAS_FACE_TRACKING
        private bool MigrateBlendshapeVisemesToFaceTracking() {
            // Native visemes blend their weights; instant mode needs the animator's discrete selection.
            if (model.instant) return true;

            var blendshapeLinks = globals.allFeaturesInRun
                .OfType<BlendShapeLink>()
                .ToArray();
            var migrated = new List<(int viseme, SkinnedMeshRenderer skin, BlendShapeAction action)>();
            var states = Reflection.StateFields.Select(field => (State)field.GetValue(model)).ToArray();
            // Read original actions before removing them; multiple slots may share a State.
            var actions = states.Select(state => state.actions.ToArray()).ToArray();

            // Do not copy the legacy animator's implicit state_aa alias into FT silence.
            for (var viseme = 1; viseme < visemeNames.Length; viseme++) {
                var state = states[viseme];
                var remainingActions = new List<Action>();
                foreach (var action in actions[viseme]) {
                    if (!(action is BlendShapeAction blendshapeAction)) {
                        remainingActions.Add(action);
                        continue;
                    }

                    var targets = avatarObject.GetComponentsInSelfAndChildren<SkinnedMeshRenderer>()
                        .Where(skin => blendshapeAction.allRenderers || blendshapeAction.renderer == skin)
                        .Where(skin => skin.HasBlendshape(blendshapeAction.blendShape))
                        .ToArray();
                    if (targets.Length == 0) {
                        remainingActions.Add(action);
                        continue;
                    }

                    foreach (var target in targets) {
                        if (BlendShapeLinkBuilder.HandlesBlendshape(
                            avatarObject,
                            blendshapeLinks,
                            target,
                            blendshapeAction.blendShape
                        )) continue;
                        migrated.Add((viseme, target, blendshapeAction));
                    }
                }
                state.actions = remainingActions;
            }

            if (migrated.Count > 0) {
                faceTracking.EnsureVisemesEnabled();
                var sets = new List<(FaceTrackingService.PendingFtBlendshapeSet set, HashSet<int> visemes)>();
                foreach (var (viseme, skin, action) in migrated) {
                    var set = sets.FirstOrDefault(entry => !entry.visemes.Contains(viseme));
                    if (set.set == null) {
                        set = (faceTracking.AddSet("_vrcf_advanced_visemes"), new HashSet<int>());
                        sets.Add(set);
                    }
                    set.visemes.Add(viseme);
                    faceTracking.SetVisemeMapping(
                        set.set,
                        viseme,
                        skin,
                        action.blendShape,
                        action.blendShapeValue / 100f
                    );
                }
            }

            return states.Any(state => state.actions.Any());
        }
#endif

        [FeatureEditor]
        public static VisualElement Editor(SerializedProperty prop) {
            var content = new VisualElement();
            content.Add(VRCFuryEditorUtils.Info(
                "This feature will allow you to use animations for your avatar's visemes."
            ));
            foreach (var name in visemeNames) {
                if (name == "sil") continue;
                var row = new VisualElement().Row();
                row.Add(new Label(name).FlexBasis(30));
                row.Add(VRCFuryEditorUtils.Prop(prop.FindPropertyRelative("state_" + name)).FlexGrow(1));
                content.Add(row);
            }
            
            content.Add(VRCFuryEditorUtils.Prop(
                prop.FindPropertyRelative("instant"),
                "Instant Mode (Unusual)",
                tooltip: "Transitions between visemes instantly, with no blending. Used for billboard flipbook mouths."
            ));
            
            return content;
        }
    }

}
