using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using System.Reflection;
using UnityEditor;
using UnityEngine;
using VF.Builder.Exceptions;
using VF.Component;
using VF.Feature;
using VF.Feature.Base;
using VF.Hooks;
using VF.Injector;
using VF.Inspector;
using VF.Menu;
using VF.Model;
using VF.Model.Feature;
using VF.Service;
using VF.Utils;
using Object = UnityEngine.Object;

namespace VF.Builder {

internal class VRCFuryBuilder {

    internal enum Status {
        Success,
        Failed
    }

    internal Status SafeRun(VFGameObject avatarObject) {
        Debug.Log("VRCFury invoked on " + avatarObject.name + " ...");

        var result = VRCFExceptionUtils.ErrorDialogBoundary(() => {
            VRCFuryAssetDatabase.WithAssetEditing(() => {
                try {
                    MaterialLocker.injectedAvatarObject = avatarObject;
                    Run(avatarObject);
                } finally {
                    MaterialLocker.injectedAvatarObject = null;
                }
            });
        });

        return result ? Status.Success : Status.Failed;
    }

    internal static bool ShouldRun(VFGameObject avatarObject) {
        return avatarObject
            .GetComponentsInSelfAndChildren<VRCFuryComponent>()
            .Where(c => !(c is VRCFuryDebugInfo || c is VRCFuryTest))
            .Any();
    }

    public static void StripAllVrcfComponents(VFGameObject obj) {
        foreach (var c in obj.GetComponentsInSelfAndChildren<VRCFuryComponent>()) {
            if (c is VRCFuryDebugInfo && !IsActuallyUploadingHook.Get()) {
                continue;
            }
            Object.DestroyImmediate(c);
        }
    }

    private void Run(VFGameObject avatarObject) {
        if (!Application.isPlaying && VRCFuryTestCopyMenuItem.IsTestCopy(avatarObject)) {
            throw new VRCFBuilderException(
                "VRCFury Test Copies cannot be uploaded. Please upload the original avatar which was" +
                " used to create this test instead.");
        }

        EditorOnlyUtils.RemoveEditorOnlyObjects(avatarObject);
        
        if (!ShouldRun(avatarObject)) {
            Debug.Log("VRCFury components not found in avatar. Skipping.");
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
        } finally {
            progress.Close();
            
            // Make absolutely positively certain that we've removed every non-standard component from the avatar before it gets uploaded
            StripAllVrcfComponents(avatarObject);

            // Make sure all new assets we've created have actually been saved to disk
            AssetDatabase.SaveAssets();
        }

        Debug.Log("VRCFury Finished!");
    }

    private static void ApplyFuryConfigs(
        VFGameObject avatarObject,
        VRCFProgressWindow progress
    ) {
        var tmpDirParent = $"{TmpFilePackage.GetPath()}/{VRCFuryAssetDatabase.MakeFilenameSafe(avatarObject.name)}";
        // Don't reuse subdirs, because if unity reuses an asset path, it randomly explodes and picks up changes from the
        // old asset and messes with the new copy.
        var tmpDir = $"{tmpDirParent}/{DateTime.Now.ToString("yyyyMMdd-HHmmss")}";

        var currentModelName = "";
        var currentModelClipPrefix = "?";
        var currentServiceNumber = 0;
        var currentServiceGameObject = avatarObject;

        var actions = new List<FeatureBuilderAction>();
        var totalActionCount = 0;
        var totalServiceCount = 0;
        var collectedModels = new List<FeatureModel>();
        var collectedBuilders = new List<FeatureBuilder>();

        var injector = new VRCFuryInjector();
        injector.OnCachedServiceBuilt(service => {
            AddActionsFromObject(service, avatarObject);
        });
        
        var globals = new GlobalsService {
            tmpDirParent = tmpDirParent,
            tmpDir = tmpDir,
            addOtherFeature = (feature) => AddComponent(feature, currentServiceGameObject, currentServiceNumber),
            allFeaturesInRun = collectedModels,
            allBuildersInRun = collectedBuilders,
            avatarObject = avatarObject,
            currentFeatureNumProvider = () => currentServiceNumber,
            currentFeatureNameProvider = () => currentModelName,
            currentFeatureClipPrefixProvider = () => currentModelClipPrefix,
            currentMenuSortPosition = () => currentServiceNumber,
            currentComponentObject = () => currentServiceGameObject,
        };
        injector.SetService(globals);
        
        foreach (var serviceType in ReflectionUtils.GetTypesWithAttributeFromAnyAssembly<VFServiceAttribute>()) {
            injector.GetService(serviceType);
        }
        injector.GetService(typeof(CleanupLegacyBuilder));
        injector.GetService(typeof(RemoveJunkAnimatorsBuilder));
        injector.GetService(typeof(FixDoubleFxBuilder));
        injector.GetService(typeof(FixWriteDefaultsBuilder));
        injector.GetService(typeof(BakeGlobalCollidersBuilder));
        injector.GetService(typeof(AnimatorLayerControlOffsetBuilder));
        injector.GetService(typeof(CleanupEmptyLayersBuilder));
        injector.GetService(typeof(ResetAnimatorBuilder));
        injector.GetService(typeof(FinalizeMenuBuilder));
        injector.GetService(typeof(FinalizeControllerBuilder));
        injector.GetService(typeof(MarkThingsAsDirtyJustInCaseBuilder));
        injector.GetService(typeof(FixEmptyMotionBuilder));

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
            var configObject = vrcFury.gameObject;
            if (VRCFuryEditorUtils.IsInRagdollSystem(configObject.transform)) {
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
            if (buildersOfType[0].OnlyOneAllowed() && buildersOfType.Length > 1) {
                throw new Exception(
                    $"This avatar contains multiple VRCFury '{buildersOfType[0].GetEditorTitle()}' components, but only one is allowed.");
            }
        }

        AddComponent(new DirectTreeOptimizer { managedOnly = true }, avatarObject);

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
