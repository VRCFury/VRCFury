using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Reflection.Emit;
using JetBrains.Annotations;
using UnityEditor;
using UnityEditor.Playables;
using UnityEngine;
using UnityEngine.Animations;
using UnityEngine.Playables;
using VF.Utils;

namespace VF.Hooks {
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
            foreach (var replacement in typeof(ShimReplacments).GetMethods(BindingFlags.DeclaredOnly | BindingFlags.Public | BindingFlags.Instance)) {
                var original = typeof(Animator).GetMethod(
                    replacement.Name,
                    BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic,
                    null,
                    replacement.GetParameters().Select(p => p.ParameterType).ToArray(),
                    null
                );
                if (original == null || original.ReturnType != replacement.ReturnType) {
                    Debug.LogWarning($"VRCFury Failed to find method to replace: Animator.{replacement.Name}");
                    continue;
                }
                HarmonyUtils.ReplaceMethod(original, replacement);
            }
            
            Scheduler.Schedule(() => {
                previewedPlayableCache.Clear();
            }, 0);
        }
 
        [UsedImplicitly(ImplicitUseTargetFlags.WithMembers)]
        class ShimReplacments {
            public float GetLayerWeight(int layerIndex) {
                return GetPreviewedPlayable(this)?.GetLayerWeight(layerIndex) ?? 0; 
            }
            public float GetFloatString(string name) { 
                return GetPreviewedPlayable(this)?.GetFloat(name) ?? 0; 
            }
            public void SetFloatString(string name, float value) {
                foreach (var p in GetPlayables(this)) {
                    SetWithCoercion(p, name, value);
                } 
            }
            public int GetIntegerString(string name) {
                return GetPreviewedPlayable(this)?.GetInteger(name) ?? 0; 
            }
            public void SetIntegerString(string name, int value) {
                foreach (var p in GetPlayables(this)) {
                    SetWithCoercion(p, name, value);
                }
            }
            public bool GetBoolString(string name) {
                return GetPreviewedPlayable(this)?.GetBool(name) ?? false; 
            }
            public void SetBoolString(string name, bool value) {
                foreach (var p in GetPlayables(this)) {
                    SetWithCoercion(p, name, value ? 1 : 0);
                }
            }
        }

        private static IList<AnimatorControllerPlayable> GetPlayables(object _animator) {
            var animator = _animator as Animator;
            if (animator == null) return null;

            return GetPlayablesForAnimator(animator);
        }

        private static readonly Dictionary<Animator, AnimatorControllerPlayable?> previewedPlayableCache =
            new Dictionary<Animator, AnimatorControllerPlayable?>();
        private static AnimatorControllerPlayable? GetPreviewedPlayable(object _animator) {
            var animator = _animator as Animator;
            if (animator == null) return null;

            if (previewedPlayableCache.TryGetValue(animator, out var cached)) return cached;

            var playables = GetPlayablesForAnimator(animator);
            var previewingController = FixDupAnimatorWindowHook.GetPreviewedAnimatorController();
            var matching = playables.Where(p => GetControllerForPlayable(p) == previewingController).ToArray();
            return previewedPlayableCache[animator] = matching.Any() ? matching.First() : null;
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

        public static void SetWithCoercion(AnimatorControllerPlayable playable, string name, float val) {
            foreach (var p in Enumerable.Range(0, playable.GetParameterCount()).Select(i => playable.GetParameter(i))) {
                if (p.name != name) continue;
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
