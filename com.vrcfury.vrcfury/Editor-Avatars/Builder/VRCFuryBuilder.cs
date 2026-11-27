using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using System.Reflection;
using UnityEditor;
using UnityEngine;
using VF.Component;
using VF.Exceptions;
using VF.Feature.Base;
using VF.Hooks.UnityFixes;
using VF.Inspector;
using VF.Model;
using VF.Model.Feature;
using VF.Service;
using VF.Utils;
using VRC.SDK3.Avatars.Components;
using Stopwatch = System.Diagnostics.Stopwatch;

namespace VF.Builder {

    internal class VRCFuryBuilder {
        private static readonly Dictionary<Type, (MethodInfo method, FeatureBuilderActionAttribute attribute)[]> actionMethodsByType = new();
        internal static void RunMain(VFGameObject avatarObject) {
            try {
                MaterialLocker.injectedAvatarObject = avatarObject;
                Run(avatarObject);
            } finally {
                MaterialLocker.injectedAvatarObject = null;
            }
        }

        internal static bool ShouldRun(VFGameObject avatarObject) {
            if (avatarObject
                .GetComponentsInSelfAndChildren<VRCFuryComponent>()
                .Any(c => c is not VRCFuryDebugInfo && !EditorOnlyUtils.IsInsideEditorOnly(c.owner(), avatarObject))) {
                // There's a vrcfury component
                return true;
            }
            return false;
        }

        private static void Run(VFGameObject avatarObject) {
            Unity6RendererFixHook.Register(avatarObject);
            EditorOnlyUtils.RemoveEditorOnlyObjects(avatarObject);

            if (!ShouldRun(avatarObject)) {
                // Debug.Log("VRCFury components not found in avatar. Skipping.");
                return;
            }
            
            /*
             * We call SaveAssets here for two reasons:
             * 1. If the build crashes unity for some reason, the user won't lose changes
             * 2. If we don't call this here, the first time we call AssetDatabase.CreateAsset can randomly
             *   fail with "Global asset import parameters have been changed during the import. Importing is restarted."
             *   followed by "Unable to import newly created asset..."
             */
            AssetDatabase.SaveAssets();

            var progress = VRCFProgressWindow.Create();

            try {
                ApplyFuryConfigs(
                    avatarObject,
                    progress
                );

                if (avatarObject.GetComponent<VRCFuryTest>() == null) {
                    avatarObject.AddComponent<VRCFuryTest>();
                }
            } finally {
                progress.Close();

                // Make sure all new assets we've created have actually been saved to disk
                AssetDatabase.SaveAssets();
            }
        }

        private static void ApplyFuryConfigs(
            VFGameObject avatarObject,
            VRCFProgressWindow progress
        ) {
            var currentModelName = "";
            var currentServiceNumber = 0;
            var currentServiceGameObject = avatarObject;

            // Actions can enqueue more actions. CompareTo ties on equal priority and offset because it
            // uses the same service number on both sides, so we use the insertion order as the "tie breaker".
            var actions = new SortedSet<(FeatureBuilderAction action, int insertionOrder)>();
            var totalActionCount = 0;
            var totalServiceCount = 0;
            var collectedModels = new List<FeatureModel>();
            var collectedBuilders = new List<FeatureBuilder>();

            var avatar = avatarObject.GetComponent<VRCAvatarDescriptor>();
            if (avatar == null) {
                throw new Exception("Failed to find VRCAvatarDescriptor on avatar object");
            }

            var injector = VRCFuryInjectorBuilder.GetInjector(avatar);
            injector.Set("componentObject", new Func<VFGameObject>(() => currentServiceGameObject));

            var globals = injector.GetService<GlobalsService>();
            globals.addOtherFeature = (feature) => AddComponent(feature, currentServiceGameObject, currentServiceNumber);
            globals.allFeaturesInRun = collectedModels;
            globals.allBuildersInRun = collectedBuilders;
            
            foreach (var service in injector.GetServices<object>()) {
                AddActionsFromObject(service, avatarObject);
            }

            void AddComponent(FeatureModel component, VFGameObject configObject, int? serviceNumOverride = null) {
                collectedModels.Add(component);

                FeatureBuilder builder;
                try {
                    builder = FeatureFinder.GetBuilder(component, configObject, injector);
                } catch (Exception e) {
                    throw new ExceptionWithCause(
                        $"Failed to load VRCFury component: {configObject.GetDebugPath()}",
                        e
                    );
                }

                if (builder == null) return;
                AddActionsFromObject(builder, configObject, serviceNumOverride);
            }

            void AddActionsFromObject(object service, VFGameObject configObject, int? serviceNumOverride = null) {
                var serviceNum = serviceNumOverride ?? ++totalServiceCount;
                if (service is FeatureBuilder builder) {
                    builder.uniqueModelNum = serviceNum;
                    builder.featureBaseObject = configObject;
                    collectedBuilders.Add(builder);
                }

                var type = service.GetType();
                if (!actionMethodsByType.TryGetValue(type, out var actionMethods)) {
                    actionMethodsByType[type] = actionMethods = type.GetMethods()
                        .Select(m => (m, m.GetCustomAttribute<FeatureBuilderActionAttribute>()))
                        .Where(tuple => tuple.Item2 != null)
                        .ToArray();
                }
                foreach (var (method, attr) in actionMethods) {
                    var action = new FeatureBuilderAction(attr, method, service, serviceNum, configObject);
                    actions.Add((action, totalActionCount++));
                }
            }

            var timings = new List<(double milliseconds, string description)>();
            const string collectMessage = "Collecting VRCFury components";
            progress.Progress(0, collectMessage);
            var timer = Stopwatch.StartNew();
            foreach (var c in avatarObject.GetComponentsInSelfAndChildren<VRCFuryComponent>()) {
                c.Upgrade();
            }
            var externalReferences = new List<string>();
            foreach (var component in avatarObject.GetComponentsInSelfAndChildren<VRCFuryComponent>()) {
                MutableManager.ForEachChildObjectReference(component, (path, target, set) => {
                    var targetObject = target switch {
                        GameObject gameObject => gameObject.asVf(),
                        UnityEngine.Component targetComponent => targetComponent.owner(),
                        _ => null
                    };
                    if (targetObject == null) return;
                    if (targetObject.IsSameOrChildOf(avatarObject)) return;

                        var targetPath = EditorUtility.IsPersistent(target)
                            ? target.GetPathAndName()
                            : targetObject.GetDebugPath();
                        externalReferences.Add(
                            $"{component.owner().GetDebugPath()} -> {targetPath}"
                        );
                });
            }
            if (externalReferences.Count > 0) {
                throw new Exception(
                    "One of your VRCFury components is referencing an object outside of the avatar. This is a mistake, and can " +
                    "happen if you incorrectly copied individual components between avatars. You need to fix these references, or just " +
                    "delete the VRCFury component since it probably doesn't work anyways.\n\n"
                    + externalReferences.JoinWithMore(10)
                );
            }

            foreach (var vrcFury in avatarObject.GetComponentsInSelfAndChildren<VRCFury>()) {
                var configObject = vrcFury.owner();
                if (VRCFuryEditorUtils.IsInRagdollSystem(configObject)) {
                    continue;
                }

                if (vrcFury.content == null) {
                    continue;
                }

                AddComponent(vrcFury.content, configObject);
            }

            foreach (var type in collectedBuilders.Select(builder => builder.GetType()).ToImmutableHashSet()) {
                var buildersOfType = collectedBuilders.Where(builder => builder.GetType() == type).ToArray();
                if (buildersOfType.Length > 1) {
                    var first = buildersOfType[0];
                    if (first.GetType().GetCustomAttribute<FeatureOnlyOneAllowedAttribute>() != null) {
                        var title = first.GetType().GetCustomAttribute<FeatureTitleAttribute>().Title;
                        throw new Exception(
                            $"This avatar contains multiple VRCFury '{title}' components, but only one is allowed.\n\n"
                            + string.Join("\n", buildersOfType.Select(builder => builder.featureBaseObject.GetDebugPath()))
                        );
                    }
                }
            }

            timings.Add((timer.Elapsed.TotalMilliseconds, collectMessage));
            FeatureOrder? lastPriority = null;
            while (actions.Count > 0) {
                var next = actions.Min;
                actions.Remove(next);
                var action = next.action;
                var service = action.GetService();
                if (action.configObject == null) {
                    var statusSkipMessage = $"{service.GetType().Name} ({currentServiceNumber}) Skipped (Object no longer exists)";
                    progress.Progress(1 - (actions.Count / (float)totalActionCount), statusSkipMessage);
                    continue;
                }

                var priority = action.GetPriorty();
                if (lastPriority != priority) {
                    if (lastPriority.HasValue) {
                        injector.GetService<RestingStateService>().OnPhaseChanged(lastPriority.Value);
                    }
                    lastPriority = priority;
                }

                globals.currentMenuSortPosition = globals.currentFeatureNum = currentServiceNumber = action.serviceNum;
                var objectName = action.configObject.GetDebugPath();
                globals.currentFeatureName = currentModelName = $"{service.GetType().Name}.{action.GetName()} on {objectName}";
                currentServiceGameObject = action.configObject;

                var statusMessage = $"{service.GetType().Name}.{action.GetName()} on {objectName} ({currentServiceNumber})";
                progress.Progress(1 - (actions.Count / (float)totalActionCount), statusMessage);

                timer.Restart();
                try {
                    action.Call();
                } catch (Exception e) {
                    throw new ExceptionWithCause($"Failed to build VRCFury component: {currentModelName}", VRCFExceptionUtils.GetGoodCause(e));
                }
                timings.Add((timer.Elapsed.TotalMilliseconds, statusMessage));
            }

            if (lastPriority.HasValue) {
                injector.GetService<RestingStateService>().OnPhaseChanged(lastPriority.Value);
            }

            var workLog = injector.GetService<SaveAssetsService>().Session;
            var timingLines = timings.OrderByDescending(timing => timing.milliseconds)
                .Select(timing => $"* {timing.milliseconds:F1} ms  {timing.description}");
            workLog.AddWorkLogSection("Build Step Timing", string.Join(Environment.NewLine, timingLines));
        }
    }

}
