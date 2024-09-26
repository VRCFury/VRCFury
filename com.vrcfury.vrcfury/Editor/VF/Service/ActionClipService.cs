using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using JetBrains.Annotations;
using UnityEngine;
using VF.Actions;
using VF.Builder;
using VF.Feature.Base;
using VF.Injector;
using VF.Model;
using VF.Utils;
using Action = VF.Model.StateAction.Action;

namespace VF.Service {
    /** Turns VRCFury actions into clips */
    [VFService]
    [VFPrototypeScope]
    internal class ActionClipService {
        [VFAutowired] private readonly Func<VFGameObject> componentObject;

        [VFAutowired] private readonly ClipFactoryService clipFactory;
        [VFAutowired] [CanBeNull] private readonly ClipBuilderService clipBuilder;
        [VFAutowired] [CanBeNull] private readonly ControllersService controllers;
        [CanBeNull] private ControllerManager fx => controllers?.GetFx();
        private readonly IDictionary<Type,ActionBuilder> modelTypeToBuilder;

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

        public Motion LoadState(string name, State state, VFGameObject animObjectOverride = null, MotionTimeMode motionTime = MotionTimeMode.Never) {
            return LoadStateAdv(name, state, animObjectOverride, motionTime).onClip;
        }
        
        public class BuiltAction {
            public Motion onClip = VrcfObjectFactory.Create<AnimationClip>();
            public AnimationClip implicitRestingClip = VrcfObjectFactory.Create<AnimationClip>();
            public bool useMotionTime = false;
        }
        
        public BuiltAction LoadStateAdv(string name, State state, VFGameObject animObjectOverride = null, MotionTimeMode motionTime = MotionTimeMode.Never, bool useServices = true) {
            var animObject = animObjectOverride ?? componentObject();

            if (state == null) {
                return new BuiltAction();
            }

            if (state.actions.Any(action => action == null)) {
                throw new Exception("Action list contains a corrupt action");
            }

            var offClip = VrcfObjectFactory.Create<AnimationClip>();

            var outputMotions = state.actions
                .Select(a => LoadAction(name, a, offClip, animObject, useServices))
                .Where(motion => new AnimatorIterator.Clips().From(motion).SelectMany(clip => clip.GetAllBindings()).Any())
                .ToList();

            bool useMotionTime;
            if (motionTime == MotionTimeMode.Auto) {
                useMotionTime = outputMotions.Any(clip => !clip.IsStatic());
            } else if (motionTime == MotionTimeMode.Always) {
                useMotionTime = true;
            } else {
                useMotionTime = false;
            }
            
            var outputClips = outputMotions
                .SelectMany(motion => new AnimatorIterator.Clips().From(motion))
                .ToArray();

            if (useMotionTime) {
                foreach (var clip in outputClips) {
                    if (clip.IsStatic()) {
                        if (clipBuilder != null) {
                            var motionClip = clipBuilder.MergeSingleFrameClips(
                                (0, VrcfObjectFactory.Create<AnimationClip>()),
                                (1, clip)
                            );
                            motionClip.UseLinearTangents();
                            motionClip.name = clip.name;
                            clip.Clear();
                            clip.CopyFrom(motionClip);
                        }
                    } else {
                        clip.SetLooping(false);
                    }
                }
            }

            Motion output;
            if (outputMotions.Any()) {
                var dbt = VFBlendTreeDirect.Create(name);
                output = dbt;
                foreach (var motion in outputMotions) {
                    dbt.Add(motion);
                }
            } else {
                output = clipFactory.NewClip(name);
            }

            return new BuiltAction {
                onClip = output,
                implicitRestingClip = offClip,
                useMotionTime = useMotionTime
            };
        }

        private Motion LoadAction(string name, Action action, AnimationClip offClip, VFGameObject animObject, bool useServices) {
            if (action == null) {
                throw new Exception("Action is corrupt");
            }

            var onClip = VrcfObjectFactory.Create<AnimationClip>();

            if (action.desktopActive || action.androidActive) {
                var isDesktop = BuildTargetUtils.IsDesktop();
                if (!isDesktop && !action.androidActive) return onClip;
                if (isDesktop && !action.desktopActive) return onClip;
            }

            if (!modelTypeToBuilder.TryGetValue(action.GetType(), out var builder)) {
                throw new Exception($"Unknown action type {action.GetType().Name}");
            }

            var methodInjector = new VRCFuryInjector();
            methodInjector.Set(action);
            methodInjector.Set(this);
            methodInjector.Set("actionName", name);
            methodInjector.Set("animObject", animObject);
            methodInjector.Set("offClip", offClip);
            methodInjector.Set("useServices", useServices);
            var buildMethod = builder.GetType().GetMethod("Build");
            var clip = (Motion)methodInjector.FillMethod(buildMethod, builder);

            Motion output = clip;
            if (fx != null && (action.localOnly || action.remoteOnly)) {
                if (action.localOnly) {
                    output = BlendtreeMath.GreaterThan(fx.IsLocal().AsFloat(), 0).create(output, null);
                } else {
                    output = BlendtreeMath.GreaterThan(fx.IsLocal().AsFloat(), 0).create(null, output);
                }
            }

            return output;
        }
    }
}
