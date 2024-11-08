using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;
using UnityEngine.Animations;
using VF.Builder;
using VF.Model;
using VF.Utils;
using VRC.SDK3.Avatars;

namespace VF.Hooks {
    internal static class ConstraintUpgradeHook {
#if VRCSDK_HAS_VRCCONSTRAINTS
        [InitializeOnLoadMethod]
        private static void Init() {
            if (BuildTargetUtils.IsDesktop()) {
                AvatarDynamicsSetup.IsUnityConstraintAutoConverted += constraint => true;
            }

            AvatarDynamicsSetup.OnConvertUnityConstraintsAcrossGameObjects += (objs, isAutoFix) => {
                if (AnimationMode.InAnimationMode()) return false;

                var usesVrcf = objs
                    .NotNull()
                    .SelectMany(obj => obj.asVf().GetComponentsInSelfAndChildren<VRCFury>())
                    .Any();

                if (!usesVrcf) return false;

                var roots = objs
                    .NotNull()
                    .Select(obj => obj.asVf())
                    .Select(obj => VRCAvatarUtils.GuessAvatarObject(obj) ?? obj.root)
                    .Distinct();

                var controllers = new HashSet<AnimatorController>();
                var clips = new HashSet<AnimationClip>();
                var unityConstraints = new HashSet<IConstraint>();
                foreach (var component in roots.SelectMany(obj => obj.GetComponentsInSelfAndChildren<UnityEngine.Component>())) {
                    var scan = GetClipsFromComponent(component);
                    controllers.UnionWith(scan.Item1);
                    clips.UnionWith(scan.Item2);
                    if (component is IConstraint c) unityConstraints.Add(c);
                }

                var overLimit = new HashSet<string>();
                foreach (var binding in clips.SelectMany(clip => clip.GetFloatBindings())) {
                    if (binding.IsOverLimitConstraint(out _)) {
                        overLimit.Add(binding.path);
                    }
                }

                var overLimitWarning = "";
                if (overLimit.Any()) {
                    overLimitWarning =
                        "WARNING! You have a constraint with more than 16 sources. If this is upgraded to a" +
                        " VRC Constraint, it will fail to be able to turn on those higher sources.\n" +
                        overLimit.Join('\n') +
                        "\n\n";
                }
                
                var ok = DialogUtils.DisplayDialog("Auto Convert Constraints",
                    $"This object uses VRCFury.\n\n" +
                    $"To ensure all merged animations are properly upgraded, the ENTIRE root object {roots.Select(root => root.name).Join(", ")} will have ALL of its constraints upgraded.\n\n" +
                    $"Scanned {controllers.Count} controllers\n" +
                    $"Will upgrade {unityConstraints.Count} constraint components\n" +
                    $"Will upgrade {clips.Count} animation clips\n\n" +
                    overLimitWarning +
                    "If your animator setup is very complex, you may want to back up your project first!",
                    "Proceed", "Cancel");
                if (!ok) return true;

                Undo.SetCurrentGroupName("Convert Constraints");
                var undoGroup = Undo.GetCurrentGroup();

                try {
                    foreach (var clip in clips) {
                        AvatarDynamicsSetup.RebindConstraintAnimationClip(clip);
                    }
                    AvatarDynamicsSetup.DoConvertUnityConstraints(unityConstraints.ToArray(), null, false);
                } finally {
                    Undo.CollapseUndoOperations(undoGroup);
                }
                return true;
            };
        }
        
        private static (AnimatorController[],AnimationClip[]) GetClipsFromComponent(object component) {
            var controllers = new HashSet<AnimatorController>();
            var clips = new HashSet<AnimationClip>();

            void FoundController(RuntimeAnimatorController _c) {
                if (!(_c is AnimatorController c)) return;
                if (c == null) return;
                controllers.Add(c);
                clips.UnionWith(new AnimatorIterator.Clips().From(c));
            }
            void FoundClip(AnimationClip clip) {
                if (clip == null) return;
                clips.Add(clip);
            }
            UnitySerializationUtils.Iterate(component, visit => {
                if (visit.value is GuidController gc) {
                    FoundController(gc.Get());
                    return UnitySerializationUtils.IterateResult.Skip;
                }
                if (visit.value is GuidAnimationClip ga) {
                    FoundClip(ga.Get());
                    return UnitySerializationUtils.IterateResult.Skip;
                }
                if (visit.value is AnimatorController ac && ac != null) {
                    FoundController(ac);
                    clips.UnionWith(new AnimatorIterator.Clips().From(ac));
                    return UnitySerializationUtils.IterateResult.Skip;
                }
                if (visit.value is AnimationClip c2 && c2 != null) {
                    FoundClip(c2);
                    return UnitySerializationUtils.IterateResult.Skip;
                }
                if (visit.value is Animator a && a != null) {
                    FoundController(a.runtimeAnimatorController);
                    return UnitySerializationUtils.IterateResult.Skip;
                }
                return UnitySerializationUtils.IterateResult.Continue;
            });
            return (controllers.ToArray(),clips.Where(ContainsUnityConstraintBindings).ToArray());
        }

        private static bool ContainsUnityConstraintBindings(AnimationClip clip) {
            return AnimationUtility.GetCurveBindings(clip)
                .Concat(AnimationUtility.GetObjectReferenceCurveBindings(clip))
                .Any(binding => typeof(IConstraint).IsAssignableFrom(binding.type));
        }
#endif
    }
}
