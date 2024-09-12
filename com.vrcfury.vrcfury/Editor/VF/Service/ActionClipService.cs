using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using JetBrains.Annotations;
using UnityEditor;
using UnityEngine;
using VF.Actions;
using VF.Builder;
using VF.Feature;
using VF.Feature.Base;
using VF.Injector;
using VF.Model;
using VF.Model.StateAction;
using VF.Utils;
using VF.Utils.Controller;
using VRC.SDK3.Avatars.Components;
using static VRC.SDK3.Avatars.ScriptableObjects.VRCExpressionsMenu.Control;
using Action = VF.Model.StateAction.Action;

namespace VF.Service {
    /** Turns VRCFury actions into clips */
    [VFService]
    [VFPrototypeScope]
    internal class ActionClipService {
        [VFAutowired] private readonly VFGameObject avatarObject;
        [VFAutowired] private readonly Func<VFGameObject> componentObject;

        [VFAutowired] [CanBeNull] private readonly ClipFactoryService clipFactory;
        [VFAutowired] [CanBeNull] private readonly ClipBuilderService clipBuilder;
        [VFAutowired] [CanBeNull] private readonly GlobalsService globals;
        private readonly IDictionary<Type,ActionBuilder> modelTypeToBuilder;

        private readonly List<(VFAFloat,string,float)> drivenParams = new List<(VFAFloat,string,float)>();
        private readonly List<(VFAFloat,string,float)> drivenSyncParams = new List<(VFAFloat,string,float)>();
        private readonly List<(VFAFloat,string,float)> drivenToggles = new List<(VFAFloat,string,float)>();
        private readonly List<(VFAFloat,string,float,FeatureBuilder)> drivenTags = new List<(VFAFloat,string,float,FeatureBuilder)>();

        private static VFAFloat triggerParam = null; // may be used across multiple actions

        public ActionClipService(List<ActionBuilder> actionBuilders) {
            modelTypeToBuilder = actionBuilders.ToImmutableDictionary(
                builder => ReflectionUtils.GetGenericArgument(builder.GetType(), typeof(IVRCFuryBuilder<>)),
                builder => builder
            );
        }

        public enum MotionTimeMode {
            Auto,
            Never,
            Always
        }

        public AnimationClip LoadState(string name, State state, VFGameObject animObjectOverride = null, MotionTimeMode motionTime = MotionTimeMode.Never, ToggleBuilder toggleFeature = null) {
            return LoadStateAdv(name, state, animObjectOverride, motionTime, toggleFeature).onClip;
        }
        
        public class BuiltAction {
            // Don't use fx.GetEmptyClip(), since this clip may be mutated later
            public AnimationClip onClip = VrcfObjectFactory.Create<AnimationClip>();
            public AnimationClip implicitRestingClip = VrcfObjectFactory.Create<AnimationClip>();
            public bool useMotionTime = false;
        }
        
        public BuiltAction LoadStateAdv(string name, State state, VFGameObject animObjectOverride = null, MotionTimeMode motionTime = MotionTimeMode.Never, ToggleBuilder toggleFeature = null) {
            triggerParam = null; // always reset when making an animation
            var animObject = animObjectOverride ?? componentObject();

            if (state == null) {
                return new BuiltAction();
            }

            if (state.actions.Any(action => action == null)) {
                throw new Exception("Action list contains a corrupt action");
            }

            var actions = state.actions.Where(action => {
                if (action.desktopActive || action.androidActive) {
                    var isDesktop = BuildTargetUtils.IsDesktop();
                    if (!isDesktop && !action.androidActive) return false;
                    if (isDesktop && !action.desktopActive) return false;
                }
                return true;
            }).ToArray();
            if (actions.Length == 0) {
                return new BuiltAction();
            }

            var offClip = VrcfObjectFactory.Create<AnimationClip>();

            var outputClips = actions
                .Select(a => LoadAction(name, a, offClip, animObject, toggleFeature))
                .ToList();

            bool useMotionTime;
            if (motionTime == MotionTimeMode.Auto) {
                useMotionTime = outputClips.Any(clip => !clip.IsStatic());
            } else if (motionTime == MotionTimeMode.Always) {
                useMotionTime = true;
            } else {
                useMotionTime = false;
            }

            var useLoop = false;
            if (useMotionTime) {
                var finalLength = outputClips.Where(clip => !clip.IsStatic()).Select(clip => clip.GetLengthInSeconds()).DefaultIfEmpty(0).Max();
                if (finalLength == 0) finalLength = 1;
                outputClips = outputClips.Select(clip => {
                    var clipLength = clip.GetLengthInSeconds();
                    if (clip.IsStatic() && clipBuilder != null) {
                        var motionClip = clipBuilder.MergeSingleFrameClips(
                            (0, VrcfObjectFactory.Create<AnimationClip>()),
                            (finalLength, clip)
                        );
                        motionClip.UseLinearTangents();
                        motionClip.name = clip.name;
                        return motionClip;
                    } else if (clipLength == finalLength) {
                        return clip;
                    } else {
                        clip.Rewrite(AnimationRewriter.RewriteCurve((binding, curve) => {
                            return (binding, curve.ScaleTime(finalLength / clipLength), true);
                        }));
                        return clip;
                    }
                }).ToList();
            } else {
                useLoop = outputClips.Any(clip => clip.IsLooping());
            }

            AnimationClip finalClip;
            if (outputClips.Count == 1) {
                finalClip = outputClips.First();
            } else {
                finalClip = VrcfObjectFactory.Create<AnimationClip>();
                foreach (var clip in outputClips) {
                    finalClip.CopyFrom(clip);
                }
            }
            finalClip.SetLooping(useLoop);

            if (clipFactory != null) {
                finalClip.name = $"{clipFactory.GetPrefix()}/{name}";
            }

            return new BuiltAction {
                onClip = finalClip,
                implicitRestingClip = offClip,
                useMotionTime = useMotionTime
            };
        }

        private AnimationClip LoadAction(string name, Action action, AnimationClip offClip, VFGameObject animObject, ToggleBuilder toggleFeature = null) {
            if (action == null) {
                throw new Exception("Action is corrupt");
            }

            var onClip = VrcfObjectFactory.Create<AnimationClip>();

            if (action.desktopActive || action.androidActive) {
                var isDesktop = BuildTargetUtils.IsDesktop();
                if (!isDesktop && !action.androidActive) return onClip;
                if (isDesktop && !action.desktopActive) return onClip;
            }

            if (modelTypeToBuilder.TryGetValue(action.GetType(), out var builder)) {
                var methodInjector = new VRCFuryInjector();
                methodInjector.Set(action);
                methodInjector.Set(this);
                methodInjector.Set("actionName", name);
                methodInjector.Set("animObject", animObject);
                methodInjector.Set("offClip", offClip);
                var buildMethod = builder.GetType().GetMethod("Build");
                return (AnimationClip)methodInjector.FillMethod(buildMethod, builder);
            }

            throw new Exception($"Unknown action type {action.GetType().Name}");
        }
    }
}
