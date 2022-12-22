using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;
using UnityEngine.SceneManagement;
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

    public bool SafeRun(GameObject avatarObject, GameObject originalObject) {
        Debug.Log("VRCFury invoked on " + avatarObject.name + " ...");

        var result = true;
        try {
            AssetDatabase.StartAssetEditing();
            Run(avatarObject, originalObject);
        } catch(Exception e) {
            result = false;
            Debug.LogException(e);
            while (e is TargetInvocationException) {
                e = (e as TargetInvocationException).InnerException;
            }
            EditorUtility.DisplayDialog("VRCFury Error", "VRCFury encountered an error.\n\n" + e.Message, "Ok");
        }

        AssetDatabase.StopAssetEditing();
        AssetDatabase.SaveAssets();
        EditorUtility.ClearProgressBar();
        return result;
    }

    private bool ShouldRun(GameObject avatarObject) {
        if (avatarObject.GetComponentsInChildren<VRCFury>(true).Length > 0) return true;
        if (avatarObject.GetComponentsInChildren<OGBPenetrator>(true).Length > 0) return true;
        if (avatarObject.GetComponentsInChildren<OGBOrifice>(true).Length > 0) return true;
        return false;
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

        var name = avatarObject.name;

        // Unhook everything from our assets before we delete them
        progress.Progress(0, "Cleaning up old VRCF cruft from avatar (in case of old builds)");
        LegacyCleaner.Clean(avatarObject);

        // Nuke all our old generated assets
        progress.Progress(0.1, "Clearing generated assets");
        var tmpDir = "Assets/_VRCFury/" + VRCFuryAssetDatabase.MakeFilenameSafe(name);
        if (Directory.Exists(tmpDir)) {
            foreach (var asset in AssetDatabase.FindAssets("", new[] { tmpDir })) {
                var path = AssetDatabase.GUIDToAssetPath(asset);
                AssetDatabase.DeleteAsset(path);
            }
        }
        // Don't reuse subdirs, because if unity reuses an asset path, it randomly explodes and picks up changes from the
        // old asset and messes with the new copy.
        tmpDir = tmpDir + "/" + DateTime.Now.ToString("yyyyMMdd-HHmmss");
        Directory.CreateDirectory(tmpDir);

        // Apply configs
        ApplyFuryConfigs(
            tmpDir,
            avatarObject,
            originalObject,
            progress.Partial(0.2, 0.8)
        );

        progress.Progress(0.9, "Removing Junk Components");
        foreach (var c in avatarObject.GetComponentsInChildren<VRCFury>(true)) {
            var animator = c.gameObject.GetComponent<Animator>();
            if (animator != null && c.gameObject != avatarObject) Object.DestroyImmediate(animator);
            Object.DestroyImmediate(c);
        }

        progress.Progress(1, "Finishing Up");


        Debug.Log("VRCFury Finished!");
    }

    private static void ApplyFuryConfigs(
        string tmpDir,
        GameObject avatarObject,
        GameObject originalObject,
        ProgressBar progress
    ) {
        var currentModelNumber = 0;
        var manager = new AvatarManager(avatarObject, tmpDir, () => currentModelNumber);
        var clipBuilder = new ClipBuilder(avatarObject);
        
        var actions = new List<FeatureBuilderAction>();
        var totalActionCount = 0;
        var totalModelCount = 0;
        var collectedModels = new List<FeatureModel>();
        var collectedBuilders = new List<FeatureBuilder>();

        void AddBuilder(FeatureBuilder builder, GameObject configObject) {
            builder.featureBaseObject = configObject;
            builder.tmpDir = tmpDir;
            builder.addOtherFeature = m => AddModel(m, configObject);
            builder.uniqueModelNum = ++totalModelCount;
            builder.allFeaturesInRun = collectedModels;
            builder.allBuildersInRun = collectedBuilders;
            builder.manager = manager;
            builder.clipBuilder = clipBuilder;
            builder.avatarObject = avatarObject;
            builder.originalObject = originalObject;

            collectedBuilders.Add(builder);
            var builderActions = builder.GetActions();
            actions.AddRange(builderActions);
            totalActionCount += builderActions.Count;
        }

        void AddModel(FeatureModel model, GameObject configObject) {
            collectedModels.Add(model);
            
            var builder = FeatureFinder.GetBuilder(model, configObject);
            if (builder == null) return;
            AddBuilder(builder, configObject);
        }

        progress.Progress(0, "Collecting features");
        foreach (var vrcFury in avatarObject.GetComponentsInChildren<VRCFury>(true)) {
            var configObject = vrcFury.gameObject;
            var config = vrcFury.config;
            config.Upgrade();
            if (config.features != null) {
                Debug.Log("Importing " + config.features.Count + " features from " + configObject.name);
                foreach (var feature in config.features) {
                    AddModel(feature, configObject);
                }
            }
        }

        AddBuilder(new FixWriteDefaultsBuilder(), avatarObject);
        AddBuilder(new BakeOGBBuilder(), avatarObject);
        
        while (actions.Count > 0) {
            var action = actions.Min();
            actions.Remove(action);
            var builder = action.GetBuilder();
            var configPath = AnimationUtility.CalculateTransformPath(builder.featureBaseObject.transform,
                avatarObject.transform);
            
            currentModelNumber = builder.uniqueModelNum;
            
            var statusMessage = "Applying " + action.GetName() + " on " + builder.avatarObject.name + " " + configPath;
            progress.Progress(1 - (actions.Count / (float)totalActionCount), statusMessage);

            action.Call();
        }
        
        progress.Progress(1, "Finalizing avatar changes");
        manager.Finish();
    }
}

}
