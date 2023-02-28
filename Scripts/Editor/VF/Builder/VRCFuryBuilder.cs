using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;
using UnityEngine.SceneManagement;
using VF.Builder.Exceptions;
using VF.Feature;
using VRC.SDK3.Avatars.Components;
using VRC.SDK3.Avatars.ScriptableObjects;
using VF.Feature.Base;
using VF.Inspector;
using VF.Menu;
using VF.Model;
using VF.Model.Feature;
using Object = UnityEngine.Object;

namespace VF.Builder {

public class VRCFuryBuilder {

    public bool SafeRun(GameObject avatarObject, GameObject originalObject = null) {
        Debug.Log("VRCFury invoked on " + avatarObject.name + " ...");

        var result = VRCFExceptionUtils.ErrorDialogBoundary(() => {
            VRCFuryAssetDatabase.WithAssetEditing(() => {
                Run(avatarObject, originalObject);
            });
        });

        AssetDatabase.SaveAssets();
        EditorUtility.ClearProgressBar();
        return result;
    }

    public static bool ShouldRun(GameObject avatarObject) {
        return Startup.GetVRCFuryComponentTypes()
            .Any(type => avatarObject.GetComponentsInChildren(type, true).Length > 0);
    }

    public static void StripAllVrcfComponents(GameObject obj) {
        // Make absolutely positively certain that we've removed every non-standard component from the avatar
        // before it gets uploaded
        foreach (var type in Startup.GetVRCFuryComponentTypes()) {
            foreach (var c in obj.GetComponentsInChildren(type, true)) {
                Object.DestroyImmediate(c);
            }
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

        var progress = new ProgressBar("VRCFury is building ...");
        
        // If GestureManager (or someone else) started animating our avatar already, we need to undo their changes
        // to get the avatar back into the default position. Tell the animator to put things back the way they were,
        // then nuke and recreate it so it resets its internal state.
        var animator = avatarObject.GetComponent<Animator>();
        if (animator) {
            animator.WriteDefaultValues();
            ToggleBuilder.WithoutAnimator(avatarObject, () => { });
        }

        // Apply configs
        ApplyFuryConfigs(
            avatarObject,
            originalObject,
            progress
        );

        Debug.Log("VRCFury Finished!");
    }

    private static void ApplyFuryConfigs(
        GameObject avatarObject,
        GameObject originalObject,
        ProgressBar progress
    ) {
        var tmpDirParent = $"Assets/_VRCFury/{VRCFuryAssetDatabase.MakeFilenameSafe(avatarObject.name)}";
        // Don't reuse subdirs, because if unity reuses an asset path, it randomly explodes and picks up changes from the
        // old asset and messes with the new copy.
        var tmpDir = $"{tmpDirParent}/{DateTime.Now.ToString("yyyyMMdd-HHmmss")}";

        var mutableManager = new MutableManager(tmpDir);

        var currentModelNumber = 0;
        var currentModelName = "";
        var currentMenuSortPosition = 0;
        var manager = new AvatarManager(
            avatarObject,
            tmpDir,
            () => currentModelNumber,
            () => currentModelName,
            () => currentMenuSortPosition,
            mutableManager
        );
        var clipBuilder = new ClipBuilder(avatarObject);

        var actions = new List<FeatureBuilderAction>();
        var totalActionCount = 0;
        var totalModelCount = 0;
        var collectedModels = new List<FeatureModel>();
        var collectedBuilders = new List<FeatureBuilder>();
        var menuSortPositionByBuilder = new Dictionary<FeatureBuilder, int>();

        void AddBuilder(FeatureBuilder builder, GameObject configObject, int menuSortPosition = -1) {
            builder.featureBaseObject = configObject;
            builder.tmpDirParent = tmpDirParent;
            builder.tmpDir = tmpDir;
            builder.uniqueModelNum = ++totalModelCount;
            if (menuSortPosition < 0) menuSortPosition = builder.uniqueModelNum;
            menuSortPositionByBuilder[builder] = menuSortPosition;
            builder.addOtherFeature = m => {
                AddModel(m, configObject, menuSortPosition);
            };
            builder.allFeaturesInRun = collectedModels;
            builder.allBuildersInRun = collectedBuilders;
            builder.manager = manager;
            builder.clipBuilder = clipBuilder;
            builder.avatarObject = avatarObject;
            builder.originalObject = originalObject;
            builder.mutableManager = mutableManager;

            collectedBuilders.Add(builder);
            var builderActions = builder.GetActions();
            actions.AddRange(builderActions);
            totalActionCount += builderActions.Count;
        }

        void AddModel(FeatureModel model, GameObject configObject, int menuSortPosition = -1) {
            collectedModels.Add(model);

            var builder = FeatureFinder.GetBuilder(model, configObject);
            if (builder == null) return;
            AddBuilder(builder, configObject, menuSortPosition);
        }

        progress.Progress(0, "Collecting features");
        foreach (var vrcFury in avatarObject.GetComponentsInChildren<VRCFury>(true)) {
            var configObject = vrcFury.gameObject;
            if (VRCFuryEditorUtils.IsInRagdollSystem(configObject.transform)) {
                continue;
            }
            var config = vrcFury.config;
            config.Upgrade();
            if (config.features != null) {
                Debug.Log("Importing " + config.features.Count + " features from " + configObject.name);
                foreach (var feature in config.features) {
                    AddModel(feature, configObject);
                }
            }
        }

        AddBuilder(new RemoveJunkAnimatorsBuilder(), avatarObject);
        AddBuilder(new CleanupLegacyBuilder(), avatarObject);
        AddBuilder(new FixDoubleFxBuilder(), avatarObject);
        AddBuilder(new FixDuplicateArmatureBuilder(), avatarObject);
        AddBuilder(new FixWriteDefaultsBuilder(), avatarObject);
        AddBuilder(new BakeOGBBuilder(), avatarObject);
        AddBuilder(new BakeGlobalCollidersBuilder(), avatarObject);
        AddBuilder(new ControllerConflictBuilder(), avatarObject);
        AddBuilder(new D4rkOptimizerBuilder(), avatarObject);
        AddBuilder(new FakeHeadBuilder(), avatarObject);
        AddBuilder(new ObjectMoveBuilder(), avatarObject);
        AddBuilder(new AnimatorLayerControlOffsetBuilder(), avatarObject);
        AddBuilder(new CleanupBaseMasksBuilder(), avatarObject);
        AddBuilder(new CleanupEmptyLayersBuilder(), avatarObject);
        
        while (actions.Count > 0) {
            var action = actions.Min();
            actions.Remove(action);
            var builder = action.GetBuilder();
            var configPath = AnimationUtility.CalculateTransformPath(builder.featureBaseObject.transform,
                avatarObject.transform);
            if (configPath == "") configPath = "Avatar Root";
            
            currentModelNumber = builder.uniqueModelNum;
            currentModelName = action.GetName() + " (Feature " + currentModelNumber + ") from " + configPath;
            currentMenuSortPosition = menuSortPositionByBuilder[builder];
            
            var statusMessage = "Applying " + action.GetName() + " on " + builder.avatarObject.name + " " + configPath;
            progress.Progress(1 - (actions.Count / (float)totalActionCount), statusMessage);

            try {
                action.Call();
            } catch (Exception e) {
                throw new VRCFActionException(currentModelName, e);
            }
        }
        
        progress.Progress(1, "Finalizing avatar changes");
        var menuSettings = collectedModels.OfType<OverrideMenuSettings>().FirstOrDefault();
        manager.Finish(menuSettings);
    }
}

}
