using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using JetBrains.Annotations;
using UnityEditor;
using UnityEditor.Animations;
using UnityEditor.Playables;
using UnityEngine;
using UnityEngine.Animations;
using UnityEngine.Playables;
using VF.Hooks.VrcsdkFixes;
using VF.Utils;

namespace VF.Hooks.UnityFixes {
    /**
     * If you open an Animator window, targeting an Animator on a gameobject using a mixer (like... an avatar using Gesture Manager / av3emu)
     * and then you open any controller aside from the first, it will try to incorrectly get the layer weights from the first controller in the mixer,
     * absolutely SPAMMING the console with tons of warnings every frame. We can fix this by wiring up the methods in Animator to search
     * for the mixer controlling it, find the playable layer for the controller we're previewing, and forward all the methods over to it.
     *
     * Note: You CANNOT use Harmony Prefix on a unity extern!
     */
    internal static class FixAnimatorPreviewBreakingInPlayModeHook {
        [InitializeOnLoadMethod]
        private static void Init() {
            foreach (var prefix in typeof(ShimPrefix).GetMethods(BindingFlags.DeclaredOnly | BindingFlags.Public | BindingFlags.Static)) {
                HarmonyUtils.Patch(
                    typeof(ShimPrefix),
                    prefix.Name,
                    typeof(Animator),
                    prefix.Name,
                    internalReplacementClass: typeof(ShimReplacments)
                );
            }
            Scheduler.Schedule(() => {
                previewedPlayableCache.Clear();
            }, 0);
        }

        [UsedImplicitly(ImplicitUseTargetFlags.WithMembers)]
        private class ShimPrefix {
            public static bool GetLayerWeight(ref float __result, int __0, Animator __instance) {
                var animator = GetAnimator(__instance);
                __result = GetPreviewedPlayable(animator)?.GetLayerWeight(__0)
                       ?? (animator.runtimeAnimatorController as AnimatorController)?.layers[__0].defaultWeight
                       ?? 1;
                return false;
            }
            public static bool SetLayerWeight(int __0, float __1, Animator __instance) {
                var animator = GetAnimator(__instance);
                GetPreviewedPlayable(animator)?.SetLayerWeight(__0, __1);
                return false;
            }
            public static bool IsParameterControlledByCurve(ref bool __result, string __0, Animator __instance) {
                var animator = GetAnimator(__instance);
                __result = GetPreviewedPlayable(animator)?.IsParameterControlledByCurve(__0)
                           ?? (TryGetParameterNameHash(animator, __0, out var hash) ? animator.IsParameterControlledByCurve(hash) : false);
                return false;
            }
            public static bool GetFloat(ref float __result, string __0, Animator __instance) {
                var animator = GetAnimator(__instance);
                __result = GetPreviewedPlayable(animator)?.GetFloat(__0)
                           ?? (TryGetParameterNameHash(animator, __0, out var hash) ? animator.GetFloat(hash) : 0);
                return false;
            }
            public static bool SetFloat(string __0, float __1, Animator __instance) {
                var animator = GetAnimator(__instance);
                var playables = GetPlayables(animator);
                if (playables.Any()) {
                    foreach (var p in playables) {
                        SetWithCoercion(p, __0, __1);
                    }
                } else {
                    if (TryGetParameterNameHash(animator, __0, out var hash)) {
                        animator.SetFloat(hash, __1);
                    }
                }
                return false;
            }
            public static bool GetInteger(ref int __result, string __0, Animator __instance) {
                var animator = GetAnimator(__instance);
                __result = GetPreviewedPlayable(animator)?.GetInteger(__0)
                           ?? (TryGetParameterNameHash(animator, __0, out var hash) ? animator.GetInteger(hash) : 0);
                return false;
            }
            public static bool SetInteger(string __0, int __1, Animator __instance) {
                var animator = GetAnimator(__instance);
                var playables = GetPlayables(animator);
                if (playables.Any()) {
                    foreach (var p in playables) {
                        SetWithCoercion(p, __0, __1);
                    }
                } else {
                    if (TryGetParameterNameHash(animator, __0, out var hash)) {
                        animator.SetInteger(hash, __1);
                    }
                }
                return false;
            }
            public static bool GetBool(ref bool __result, string __0, Animator __instance) {
                var animator = GetAnimator(__instance);
                __result = GetPreviewedPlayable(animator)?.GetBool(__0)
                           ?? (TryGetParameterNameHash(animator, __0, out var hash) ? animator.GetBool(hash) : false);
                return false;
            }
            public static bool SetBool(string __0, bool __1, Animator __instance) {
                var animator = GetAnimator(__instance);
                var playables = GetPlayables(animator);
                if (playables.Any()) {
                    foreach (var p in playables) {
                        SetWithCoercion(p, __0, __1 ? 1 : 0);
                    }
                } else {
                    if (TryGetParameterNameHash(animator, __0, out var hash)) {
                        animator.SetBool(hash, __1);
                    }
                }
                return false;
            }
        }
 
        [UsedImplicitly(ImplicitUseTargetFlags.WithMembers)]
        class ShimReplacments {
            public float GetLayerWeight(int layerIndex) {
                var animator = GetAnimator(this);
                return GetPreviewedPlayable(animator)?.GetLayerWeight(layerIndex)
                       ?? (animator.runtimeAnimatorController as AnimatorController)?.layers[layerIndex].defaultWeight
                       ?? 1;
            }
            public void SetLayerWeight(int layerIndex, float weight) {
                var animator = GetAnimator(this);
                GetPreviewedPlayable(animator)?.SetLayerWeight(layerIndex, weight); 
            }
        }

        private static Animator GetAnimator(object obj) {
            return obj as Animator;
        }

        private static IList<AnimatorControllerPlayable> GetPlayables(Animator animator) {
            if (animator == null) return null;
            return GetPlayablesForAnimator(animator);
        }

        private static readonly Dictionary<Animator, AnimatorControllerPlayable?> previewedPlayableCache =
            new Dictionary<Animator, AnimatorControllerPlayable?>();
        private static AnimatorControllerPlayable? GetPreviewedPlayable(Animator animator) {
            if (animator == null) return null;
            if (previewedPlayableCache.TryGetValue(animator, out var cached)) return cached;
            return previewedPlayableCache[animator] = GetPreviewedPlayableUncached(animator);
        }
        private static AnimatorControllerPlayable? GetPreviewedPlayableUncached(Animator animator) {
            var playables = GetPlayablesForAnimator(animator);
            var previewingController = FixDupAnimatorWindowHook.GetPreviewedAnimatorController();
            var matching = playables.Where(p => GetControllerForPlayable(p) == previewingController).ToArray();
            if (matching.Any()) return matching.First();
            return null;
        }

        private static readonly MethodInfo GetAnimatorControllerInternal = typeof(AnimatorControllerPlayable)
            .GetMethod("GetAnimatorControllerInternal", BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic);
        [CanBeNull]
        private static RuntimeAnimatorController GetControllerForPlayable(AnimatorControllerPlayable playable) {
            if (GetAnimatorControllerInternal == null) return null;
            var handle = playable.GetHandle();
            var c = GetAnimatorControllerInternal.Invoke(null, new object[] { handle }) as RuntimeAnimatorController;
            while (c is AnimatorOverrideController oc) {
                c = oc.runtimeAnimatorController;
            }
            return c;
        }

        private static IList<AnimatorControllerPlayable> GetPlayablesForAnimator(Animator animator) {
            if (!animator.hasBoundPlayables) return new AnimatorControllerPlayable[]{};

            return Utility.GetAllGraphs()
                .Where(g => g.IsValid())
                .SelectMany(graph => {
                    return Enumerable.Range(0, graph.GetOutputCountByType<AnimationPlayableOutput>())
                        .Select(i => graph.GetOutputByType<AnimationPlayableOutput>(i))
                        .Where(output => output.IsOutputValid())
                        .Select(output => (AnimationPlayableOutput)output)
                        .Where(output => output.GetTarget() == animator);
                })
                .Select(output => output.GetSourcePlayable())
                .Where(playable => playable.IsPlayableOfType<AnimationLayerMixerPlayable>())
                .SelectMany(playable => Enumerable.Range(0, playable.GetInputCount()).Select(i => playable.GetInput(i)))
                .Where(playable => playable.IsValid())
                .Where(playable => playable.IsPlayableOfType<AnimatorControllerPlayable>())
                .Select(playable => (AnimatorControllerPlayable)playable)
                .ToArray();
        }

        private static bool TryGetParameterNameHash(Animator animator, string name, out int hash) {
            foreach (var p in animator.parameters) {
                if (p.name == name) {
                    hash = p.nameHash;
                    return true;
                }
            }

            hash = -1;
            return false;
        }

        private static void SetWithCoercion(AnimatorControllerPlayable playable, string name, float val) {
            foreach (var p in Enumerable.Range(0, playable.GetParameterCount()).Select(i => playable.GetParameter(i))) {
                if (p.name != name) continue;
                if (playable.IsParameterControlledByCurve(p.nameHash)) {
                    break;
                }
                switch (p.type) {
                    case AnimatorControllerParameterType.Float:
                        playable.SetFloat(p.nameHash, val);
                        break;
                    case AnimatorControllerParameterType.Int:
                        playable.SetInteger(p.nameHash, (int)Math.Round(val));
                        break;
                    case AnimatorControllerParameterType.Trigger:
                        playable.SetTrigger(p.nameHash);
                        break;
                    case AnimatorControllerParameterType.Bool:
                        playable.SetBool(p.nameHash, val != 0f);
                        break;
                }
                break;
            }

        }
    }
}
