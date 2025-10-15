using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using System.Reflection;
using UnityEditor;
using UnityEngine;
using VF.Actions;
using VF.Builder.Exceptions;
using VF.Component;
using VF.Feature.Base;
using VF.Hooks;
using VF.Injector;
using VF.Inspector;
using VF.Menu;
using VF.Model;
using VF.Model.Feature;
using VF.Service;
using VF.Utils;
using VRC.SDK3.Avatars.Components;
using Object = UnityEngine.Object;

namespace VF.Builder {

    internal class VRCFuryBuilder {
        internal static void RunMain(VFGameObject avatarObject) {
            Debug.Log("VRCFury invoked on " + avatarObject.name + " ...");

            VRCFuryAssetDatabase.WithAssetEditing(() => {
                try {
                    MaterialLocker.injectedAvatarObject = avatarObject;
                    Run(avatarObject);
                } finally {
                    MaterialLocker.injectedAvatarObject = null;
                }
            });
        }

        internal static bool ShouldRun(VFGameObject avatarObject) {
            if (avatarObject
                .GetComponentsInSelfAndChildren<VRCFuryComponent>()
                .Any(c => !(c is VRCFuryDebugInfo))) {
                // There's a vrcfury component
                return true;
            }
            return false;
        }

        private static void Run(VFGameObject avatarObject) {
            EditorOnlyUtils.RemoveEditorOnlyObjects(avatarObject);

            if (!ShouldRun(avatarObject)) {
                Debug.Log("VRCFury components not found in avatar. Skipping.");
                return;
            }
            
            // If we don't do this, a unity issue in RepaintImmediately can randomly throw a segfault
            RenderTexture.active = null;
            Camera.SetupCurrent(null);

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

            Debug.Log("VRCFury Finished!");
        }

        private static void ApplyFuryConfigs(
            VFGameObject avatarObject,
            VRCFProgressWindow progress
        ) {
            var currentModelName = "";
            var currentModelClipPrefix = "?";
            var currentServiceNumber = 0;
            var currentServiceGameObject = avatarObject;
            var currentObjectPath = "";

            var actions = new List<FeatureBuilderAction>();
            var totalActionCount = 0;
            var totalServiceCount = 0;
            var collectedModels = new List<FeatureModel>();
            var collectedBuilders = new List<FeatureBuilder>();

            var avatar = avatarObject.GetComponent<VRCAvatarDescriptor>();
            if (avatar == null) {
                throw new Exception("Failed to find VRCAvatarDescriptor on avatar object");
            }

            var injector = new VRCFuryInjector();
            injector.ImportScan(typeof(VFServiceAttribute));
            injector.ImportScan(typeof(ActionBuilder));
            injector.Set(avatar);
            injector.Set("avatarObject", avatarObject);
            injector.Set("componentObject", new Func<VFGameObject>(() => currentServiceGameObject));
            
            var globals = new GlobalsService {
                addOtherFeature = (feature) => AddComponent(feature, currentServiceGameObject, currentServiceNumber),
                allFeaturesInRun = collectedModels,
                allBuildersInRun = collectedBuilders,
                avatarObject = avatarObject,
                currentFeatureNumProvider = () => currentServiceNumber,
                currentFeatureNameProvider = () => currentModelName,
                currentFeatureClipPrefixProvider = () => currentModelClipPrefix,
                currentMenuSortPosition = () => currentServiceNumber,
                currentFeatureObjectPath = () => currentObjectPath,
            };
            injector.Set(globals);
            
            foreach (var service in injector.GetServices<object>()) {
                AddActionsFromObject(service, avatarObject);
            }

            void AddComponent(FeatureModel component, VFGameObject configObject, int? serviceNumOverride = null) {
                collectedModels.Add(component);

                FeatureBuilder builder;
                try {
                    builder = FeatureFinder.GetBuilder(component, configObject, injector, avatarObject);
                } catch (Exception e) {
                    throw new ExceptionWithCause(
                        $"Failed to load VRCFury component on object {configObject.GetPath(avatarObject)}",
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

                var actionMethods = service.GetType().GetMethods()
                    .Select(m => (m, m.GetCustomAttribute<FeatureBuilderActionAttribute>()))
                    .Where(tuple => tuple.Item2 != null)
                    .ToArray();
                if (actionMethods.Length == 0) return;

                var list = new List<FeatureBuilderAction>();
                foreach (var (method, attr) in actionMethods) {
                    list.Add(new FeatureBuilderAction(attr, method, service, serviceNum, configObject));
                }
                actions.AddRange(list);
                totalActionCount += list.Count;
            }

            progress.Progress(0, "Collecting VRCFury components");
            foreach (var c in avatarObject.GetComponentsInSelfAndChildren<VRCFuryComponent>()) {
                c.Upgrade();
            }
            foreach (var vrcFury in avatarObject.GetComponentsInSelfAndChildren<VRCFury>()) {
                var configObject = vrcFury.owner();
                if (VRCFuryEditorUtils.IsInRagdollSystem(configObject)) {
                    continue;
                }

                var loadFailure = vrcFury.GetBrokenMessage();
                if (loadFailure != null) {
                    throw new VRCFBuilderException($"VRCFury component is corrupted on {configObject.name} ({loadFailure})");
                }

                if (vrcFury.content == null) {
                    continue;
                }

                var debugLogString = $"Importing {vrcFury.content.GetType().Name} from {configObject.name}";
                AddComponent(vrcFury.content, configObject);
                Debug.Log(debugLogString);
            }

            foreach (var type in collectedBuilders.Select(builder => builder.GetType()).ToImmutableHashSet()) {
                var buildersOfType = collectedBuilders.Where(builder => builder.GetType() == type).ToArray();
                if (buildersOfType.Length > 1) {
                    var first = buildersOfType[0];
                    if (first.GetType().GetCustomAttribute<FeatureOnlyOneAllowedAttribute>() != null) {
                        var title = first.GetType().GetCustomAttribute<FeatureTitleAttribute>().Title;
                        throw new Exception(
                            $"This avatar contains multiple VRCFury '{title}' components, but only one is allowed.");
                    }
                }
            }

            FeatureOrder? lastPriority = null;
            while (actions.Count > 0) {
                var action = actions.Min();
                actions.Remove(action);
                var service = action.GetService();
                if (action.configObject == null) {
                    var statusSkipMessage = $"{service.GetType().Name} ({currentServiceNumber}) Skipped (Object no longer exists)";
                    progress.Progress(1 - (actions.Count / (float)totalActionCount), statusSkipMessage);
                    continue;
                }

                var priority = action.GetPriorty();
                if (lastPriority != priority) {
                    lastPriority = priority;
                    injector.GetService<RestingStateService>().OnPhaseChanged();
                }

                currentServiceNumber = action.serviceNum;
                var objectName = action.configObject.GetPath(avatarObject, prettyRoot: true);
                currentModelName = $"{service.GetType().Name}.{action.GetName()} on {objectName}";
                currentModelClipPrefix = $"VF{currentServiceNumber} {(service as FeatureBuilder)?.GetClipPrefix() ?? service.GetType().Name}";
                currentServiceGameObject = action.configObject;
                currentObjectPath = action.configObject.GetPath(avatarObject);

                var statusMessage = $"{service.GetType().Name}.{action.GetName()} on {objectName} ({currentServiceNumber})";
                progress.Progress(1 - (actions.Count / (float)totalActionCount), statusMessage);

                try {
                    action.Call();
                } catch (Exception e) {
                    throw new ExceptionWithCause($"Failed to build VRCFury component: {currentModelName}", VRCFExceptionUtils.GetGoodCause(e));
                }
            }
        }
    }

}
