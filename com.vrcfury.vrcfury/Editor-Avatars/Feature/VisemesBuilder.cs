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
        private ControllerManager fx => controllers.GetFx();

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

            var VisemeParam = fx.NewFloat("Viseme", usePrefix: false);
            var VolumeParam = fx.NewFloat("Voice", usePrefix: false);
            
            var enabled = fx.MakeAap("AdvancedVisemesEnabled", def: 1);
            var directTree = dbtLayerService.Create();
            var math = dbtLayerService.GetMath(directTree);
            VFAFloat volumeToUse;
            if (model.instant) {
                volumeToUse = math.SetValueWithConditions(
                    $"InstantVolume",
                    (1, BlendtreeMath.GreaterThan(VolumeParam, 0.1f)),
                    (0, null)
                );
            } else {
                var scaledVolume = fx.MakeAap("ScaledVolume");
                directTree.Add(VFBlendTree1D.CreateWithData("ScaledVolume", VolumeParam,
                    (0, scaledVolume.MakeSetter(0)),
                    (0.05f, scaledVolume.MakeSetter(0f)),
                    (0.15f, scaledVolume.MakeSetter(0.8f)),
                    (1f, scaledVolume.MakeSetter(1f))
                ));
                volumeToUse = smooth.Smooth(directTree, "SmoothedVolume", scaledVolume, 0.07f, false);
            }
            var voiceTree = VFBlendTreeDirect.Create("Advanced Visemes");
            var volumeTree = VFBlendTreeDirect.Create("Advanced Visemes");
            volumeTree.Add(volumeToUse, voiceTree);
            directTree.Add(enabled, volumeTree);

            void addViseme(int index, string text, State clipState) {
                var motion = actionClipService.LoadState("Viseme " + text, clipState).EvaluateMotion(1);

                var intensityRaw = math.SetValueWithConditions(
                    $"{text}Raw",
                    (1, BlendtreeMath.Equals(VisemeParam, index)),
                    (0, null)
                );
                VFAFloat intensityToUse;
                if (model.instant) {
                    intensityToUse = intensityRaw;
                } else {
                    intensityToUse = smooth.Smooth(directTree, $"{text}Smooth", intensityRaw, 0.05f, false);
                }
                voiceTree.Add(intensityToUse, motion);
            }

            for (var i = 0; i < visemeNames.Length; i++) {
                var name = visemeNames[i];
                addViseme(i, name, (State)Reflection.StateFields[i].GetValue(model));
            }

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

#if VRCSDK_HAS_FACE_TRACKING
        private bool MigrateBlendshapeVisemesToFaceTracking() {
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
