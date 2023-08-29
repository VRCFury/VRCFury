using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using UnityEditor;
using UnityEngine;
using VF.Builder.Exceptions;
using VF.Component;
using VF.Feature;
using VF.Feature.Base;
using VF.Injector;
using VF.Inspector;
using VF.Menu;
using VF.Model;
using VF.Model.Feature;
using VF.Service;
using Object = UnityEngine.Object;

namespace VF.Builder {

public class VRCFuryBuilder {

    public bool SafeRun(VFGameObject avatarObject, VFGameObject originalObject = null) {
        Debug.Log("VRCFury invoked on " + avatarObject.name + " ...");

        var result = VRCFExceptionUtils.ErrorDialogBoundary(() => {
            VRCFuryAssetDatabase.WithAssetEditing(() => {
                Run(avatarObject, originalObject);
            });
        });

        AssetDatabase.SaveAssets();

        return result;
    }

    public static bool ShouldRun(VFGameObject avatarObject) {
        return avatarObject.GetComponentsInSelfAndChildren<VRCFuryComponent>().Length > 0;
    }

    public static void StripAllVrcfComponents(VFGameObject obj) {
        foreach (var c in obj.GetComponentsInSelfAndChildren<VRCFuryComponent>()) {
            Object.DestroyImmediate(c);
        }
    }

    private void Run(GameObject avatarObject, GameObject originalObject) {
        if (VRCFuryTestCopyMenuItem.IsTestCopy(avatarObject)) {
            throw new VRCFBuilderException(
                "VRCFury Test Copies cannot be uploaded. Please upload the original avatar which was" +
                " used to create this test instead.");
        }
        
        if (!ShouldRun(avatarObject)) {
            Debug.Log("VRCFury components not found in avatar. Skipping.");
            return;
        }

        var progress = VRCFProgressWindow.Create();

        try {
            ApplyFuryConfigs(
                avatarObject,
                originalObject,
                progress
            );
        } finally {
            progress.Close();
        }

        Debug.Log("VRCFury Finished!");
    }

    private static void ApplyFuryConfigs(
        VFGameObject avatarObject,
        VFGameObject originalObject,
        VRCFProgressWindow progress
    ) {
        var tmpDirParent = $"{TmpFilePackage.GetPath()}/{VRCFuryAssetDatabase.MakeFilenameSafe(avatarObject.name)}";
        // Don't reuse subdirs, because if unity reuses an asset path, it randomly explodes and picks up changes from the
        // old asset and messes with the new copy.
        var tmpDir = $"{tmpDirParent}/{DateTime.Now.ToString("yyyyMMdd-HHmmss")}";

        var mutableManager = new MutableManager(tmpDir);

        var currentModelNumber = 0;
        var currentModelName = "";
        var currentModelClipPrefix = "?";
        var currentMenuSortPosition = 0;
        var currentComponentObject = avatarObject;
        var manager = new AvatarManager(
            avatarObject,
            tmpDir,
            () => currentModelNumber,
            () => currentModelName,
            () => currentModelClipPrefix,
            () => currentMenuSortPosition,
            () => currentComponentObject,
            mutableManager
        );

        var actions = new List<FeatureBuilderAction>();
        var totalActionCount = 0;
        var totalModelCount = 0;
        var collectedModels = new List<FeatureModel>();
        var collectedBuilders = new List<FeatureBuilder>();

        var injector = new VRCFuryInjector();
        injector.RegisterService(mutableManager);
        injector.RegisterService(manager);
        foreach (var serviceType in ReflectionUtils.GetTypesWithAttributeFromAnyAssembly<VFServiceAttribute>()) {
            injector.RegisterService(serviceType);
        }
        
        var globals = new GlobalsService {
            tmpDirParent = tmpDirParent,
            tmpDir = tmpDir,
            addOtherFeature = AddModel,
            allFeaturesInRun = collectedModels,
            allBuildersInRun = collectedBuilders,
            avatarObject = avatarObject,
            originalObject = originalObject,
        };
        injector.RegisterService(globals);

        void AddBuilder(Type t) {
            injector.RegisterService(t);
        }
        AddBuilder(typeof(CleanupLegacyBuilder));
        AddBuilder(typeof(RemoveJunkAnimatorsBuilder));
        AddBuilder(typeof(FixDoubleFxBuilder));
        AddBuilder(typeof(FixWriteDefaultsBuilder));
        AddBuilder(typeof(BakeGlobalCollidersBuilder));
        AddBuilder(typeof(ControllerConflictBuilder));
        AddBuilder(typeof(AnimatorLayerControlOffsetBuilder));
        AddBuilder(typeof(FixMasksBuilder));
        AddBuilder(typeof(CleanupEmptyLayersBuilder));
        AddBuilder(typeof(ResetAnimatorBuilder));
        AddBuilder(typeof(FixBadVrcParameterNamesBuilder));
        AddBuilder(typeof(FinalizeMenuBuilder));
        AddBuilder(typeof(FinalizeParamsBuilder));
        AddBuilder(typeof(FinalizeControllerBuilder));
        AddBuilder(typeof(MarkThingsAsDirtyJustInCaseBuilder));
        AddBuilder(typeof(FixMaterialSwapWithMaskBuilder));
        AddBuilder(typeof(RestingStateBuilder));
        AddBuilder(typeof(PullMusclesOutOfFxBuilder));
        AddBuilder(typeof(RestoreProxyClipsBuilder));
        AddBuilder(typeof(FixEmptyMotionBuilder));

        foreach (var service in injector.GetAllServices()) {
            AddActionsFromObject(service, avatarObject);
        }

        void AddModel(FeatureModel model, VFGameObject configObject) {
            collectedModels.Add(model);

            var builder = FeatureFinder.GetBuilder(model, configObject, injector);
            if (builder == null) return;
            AddActionsFromObject(builder, configObject);
        }

        void AddActionsFromObject(object obj, VFGameObject configObject) {
            var serviceNum = ++totalModelCount;
            if (obj is FeatureBuilder builder) {
                builder.uniqueModelNum = serviceNum;
                builder.featureBaseObject = configObject;
                collectedBuilders.Add(builder);
            }

            var actionMethods = obj.GetType().GetMethods()
                .Select(m => (m, m.GetCustomAttribute<FeatureBuilderActionAttribute>()))
                .Where(tuple => tuple.Item2 != null)
                .ToArray();
            if (actionMethods.Length == 0) return;

            // If we're in the middle of processing a service action, the newly added service should
            // inherit the menu sort position from the current one
            var menuSortPosition = currentMenuSortPosition > 0 ? currentMenuSortPosition : serviceNum;

            var list = new List<FeatureBuilderAction>();
            foreach (var (method, attr) in actionMethods) {
                list.Add(new FeatureBuilderAction(attr, method, obj, serviceNum, menuSortPosition, configObject));
            }
            actions.AddRange(list);
            totalActionCount += list.Count;
        }

        progress.Progress(0, "Collecting features");
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
            var config = vrcFury.config;
            if (config.features != null) {
                Debug.Log("Importing " + config.features.Count + " features from " + configObject.name);
                foreach (var feature in config.features) {
                    AddModel(feature, configObject);
                }
            }
        }

        AddModel(new DirectTreeOptimizer { managedOnly = true }, avatarObject);

        while (actions.Count > 0) {
            var action = actions.Min();
            actions.Remove(action);
            var service = action.GetService();

            currentModelNumber = action.serviceNum;
            var objectName = action.configObject.GetPath(avatarObject);
            currentModelName = $"{service.GetType().Name}.{action.GetName()} on {objectName}";
            currentModelClipPrefix = $"VF{currentModelNumber} {(service as FeatureBuilder)?.GetClipPrefix() ?? service.GetType().Name}";
            currentMenuSortPosition = action.menuSortOrder;
            currentComponentObject = action.configObject;

            var statusMessage = $"{objectName}\n{service.GetType().Name} ({currentModelNumber})\n{action.GetName()}";
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
