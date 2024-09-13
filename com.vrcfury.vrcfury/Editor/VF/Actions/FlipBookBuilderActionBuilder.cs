using System.Linq;
using JetBrains.Annotations;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;
using VF.Builder;
using VF.Feature.Base;
using VF.Injector;
using VF.Inspector;
using VF.Model.StateAction;
using VF.Service;
using VF.Utils;

namespace VF.Actions {
    [FeatureTitle("Flipbook Builder")]
    internal class FlipBookBuilderActionBuilder : ActionBuilder<FlipBookBuilderAction> {
        [VFAutowired] [CanBeNull] private readonly ClipBuilderService clipBuilder;

        public AnimationClip Build(FlipBookBuilderAction model, VFGameObject animObject, ActionClipService actionClipService) {
            var onClip = NewClip();
            var states = model.pages.Select(page => page.state).ToList();
            if (states.Count == 0) return onClip;
            // Duplicate the last state so the last state still gets an entire frame
            states.Add(states.Last());
            var sources = states
                .Select((substate,i) => {
                    var loaded = actionClipService.LoadStateAdv("tmp", substate, animObject);
                    return ((float)i, loaded.onClip);
                })
                .ToArray();

            if (clipBuilder != null) {
                var built = clipBuilder.MergeSingleFrameClips(sources);
                built.UseConstantTangents();
                onClip.CopyFrom(built);
            } else {
                // This is wrong, but it's fine because this branch is for debug info only
                foreach (var source in sources) {
                    onClip.CopyFrom(source.onClip);
                }
            }
            return onClip;
        }

        [FeatureEditor]
        public static VisualElement Editor(SerializedProperty prop) {
            var output = new VisualElement();
            output.Add(VRCFuryEditorUtils.Info(
                "This will create a clip made up of one frame per child action. This is mostly useful for" +
                " VRCFury Toggles with 'Use a Slider (Radial)' enabled, as you can put various presets in these slots" +
                " and use the slider to select one of them."
            ));
            output.Add(VRCFuryEditorUtils.List(prop.FindPropertyRelative("pages")));
            return output;
        }
    }
}
