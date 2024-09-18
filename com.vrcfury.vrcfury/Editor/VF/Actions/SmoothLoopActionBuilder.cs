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
    [FeatureTitle("Smooth Loop Builder (Breathing, etc)")]
    internal class SmoothLoopActionBuilder : ActionBuilder<SmoothLoopAction> {
        [VFAutowired] [CanBeNull] private readonly ClipBuilderService clipBuilder;

        public AnimationClip Build(SmoothLoopAction model, ActionClipService actionClipService, VFGameObject animObject) {
            var onClip = NewClip();
            var clip1 = actionClipService.LoadStateAdv("tmp", model.state1, animObject);
            var clip2 = actionClipService.LoadStateAdv("tmp", model.state2, animObject);

            if (clipBuilder != null) {
                var built = clipBuilder.MergeSingleFrameClips(
                    (0, clip1.onClip.FlattenAll()),
                    (model.loopTime / 2, clip2.onClip.FlattenAll()),
                    (model.loopTime, clip1.onClip.FlattenAll())
                );
                onClip.CopyFrom(built);
            } else {
                // This is wrong, but it's fine because this branch is for debug info only
                onClip.CopyFrom(clip1.onClip.FlattenAll());
                onClip.CopyFrom(clip2.onClip.FlattenAll());
            }

            onClip.SetLooping(true);
            return onClip;
        }

        [FeatureEditor]
        public static VisualElement Editor(SerializedProperty prop) {
            var output = new VisualElement();
            output.Add(VRCFuryEditorUtils.Info(
                "This will create an animation smoothly looping between two states." +
                " You can use this for a breathing cycle or any other type of smooth two-state loop."));
            output.Add(VRCFuryActionSetDrawer.render(prop.FindPropertyRelative("state1"), "State A"));
            output.Add(VRCFuryActionSetDrawer.render(prop.FindPropertyRelative("state2"), "State B"));
            output.Add(VRCFuryEditorUtils.Prop(prop.FindPropertyRelative("loopTime"), "Loop time (seconds)"));
            return output;
        }
    }
}