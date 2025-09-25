using System.Linq;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;
using VF.Feature.Base;
using VF.Injector;
using VF.Inspector;
using VF.Model;
using VF.Model.Feature;
using VF.Service;
using VF.Utils;
using VF.Utils.Controller;
using VRC.SDK3.Avatars.Components;
using VRC.SDKBase;

namespace VF.Feature {

    [FeatureTitle("Advanced Visemes")]
    internal class VisemesBuilder : FeatureBuilder<Visemes> {
        [VFAutowired] private readonly TrackingConflictResolverService trackingConflictResolverService;
        [VFAutowired] private readonly ActionClipService actionClipService;
        [VFAutowired] private readonly SmoothingService smooth;
        [VFAutowired] private readonly ClipFactoryService clipFactory;
        [VFAutowired] private readonly VRCAvatarDescriptor avatar;
        [VFAutowired] private readonly ControllersService controllers;
        [VFAutowired] private readonly DbtLayerService dbtLayerService;
        private ControllerManager fx => controllers.GetFx();

        private static readonly string[] visemeNames = {
            "sil", "PP", "FF", "TH", "DD", "kk", "CH", "SS", "nn", "RR", "aa", "E", "I", "O", "U"
        };
        
        [FeatureBuilderAction]
        public void Apply() {
            if (avatar.lipSync != VRC_AvatarDescriptor.LipSyncStyle.VisemeBlendShape) {
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
                var clip = actionClipService.LoadState("Viseme " + text, clipState).GetLastFrame();

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
                voiceTree.Add(intensityToUse, clip);
            }

            for (var i = 0; i < visemeNames.Length; i++) {
                var name = visemeNames[i];
                var fieldName = "state_" + ((i == 0) ? "aa" : name);
                addViseme(i, name, (State)model.GetType().GetField(fieldName).GetValue(model));
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
