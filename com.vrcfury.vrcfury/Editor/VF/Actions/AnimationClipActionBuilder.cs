using System.Collections.Immutable;
using JetBrains.Annotations;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;
using VF.Builder;
using VF.Feature.Base;
using VF.Injector;
using VF.Inspector;
using VF.Model;
using VF.Model.StateAction;
using VF.Service;
using VF.Utils;

namespace VF.Actions {
    [FeatureTitle("Animation Clip")]
    [FeatureHideTitleInEditor]
    internal class AnimationClipActionBuilder : ActionBuilder<AnimationClipAction> {
        [VFAutowired] private readonly VFGameObject avatarObject;
        [VFAutowired] [CanBeNull] private readonly FullBodyEmoteService fullBodyEmoteService;
        
        public Motion Build(AnimationClipAction clipAction, VFGameObject animObject) {
            var input = clipAction.motion;
            if (input == null) input = clipAction.clip.Get();
            if (input == null) return NewClip();

            var copy = input.Clone();
            foreach (var clip in new AnimatorIterator.Clips().From(copy)) {
                AddFullBodyClip(clip);
                
                var rewriter = AnimationRewriter.Combine(
                    ClipRewriter.CreateNearestMatchPathRewriter(
                        animObject: animObject,
                        rootObject: avatarObject
                    ),
                    ClipRewriter.AdjustRootScale(avatarObject),
                    ClipRewriter.AnimatorBindingsAlwaysTargetRoot()
                );
                clip.Rewrite(rewriter);
            }

            return copy;
        }

        [FeatureEditor]
        public static VisualElement Editor(SerializedProperty prop, VFGameObject componentObject) {
            var row = new VisualElement().Row();
            row.Add(VRCFuryActionDrawer.Title("Animation Clip").FlexBasis(100));
            var clipProp = prop.FindPropertyRelative("clip");
            row.Add(VRCFuryEditorUtils.Prop(clipProp).FlexGrow(1));
            row.Add(new Button(() => {
                var clip = (clipProp.GetObject() as GuidAnimationClip)?.Get();
                if (clip == null) {
                    var newPath = EditorUtility.SaveFilePanelInProject("VRCFury Recorder", "New Animation", "anim", "Path to new animation");
                    if (string.IsNullOrEmpty(newPath)) return;
                    clip = new AnimationClip();
                    AssetDatabase.CreateAsset(clip, newPath);
                    GuidWrapperPropertyDrawer.SetValue(clipProp, clip);
                    clipProp.serializedObject.ApplyModifiedProperties();
                }
                RecorderUtils.Record(clip, componentObject);
            }) { text = "Record" });
            return row;
        }
        
        private void AddFullBodyClip(AnimationClip clip) {
            if (fullBodyEmoteService == null) return;
            var types = clip.GetMuscleBindingTypes();
            if (types.Contains(EditorCurveBindingExtensions.MuscleBindingType.NonMuscle)) {
                types = types.Remove(EditorCurveBindingExtensions.MuscleBindingType.NonMuscle);
            }
            if (types.Contains(EditorCurveBindingExtensions.MuscleBindingType.Body)) {
                types = ImmutableHashSet.Create(EditorCurveBindingExtensions.MuscleBindingType.Body);
            }
            var copy = clip.Clone();
            foreach (var muscleType in types) {
                var trigger = fullBodyEmoteService.AddClip(copy, muscleType);
                clip.SetAap(trigger, 1);
            }
            clip.Rewrite(AnimationRewriter.RewriteBinding(b => {
                if (b.GetPropType() == EditorCurveBindingType.Muscle) return null;
                return b;
            }));
        }
    }
}
