using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;
using UnityEngine.SceneManagement;
using VRC.SDK3.Avatars.Components;
using VRC.SDK3.Avatars.ScriptableObjects;
using VF.Feature.Base;
using VF.Inspector;
using VF.Model;
using VF.Model.Feature;
using Object = UnityEngine.Object;

namespace VF.Builder {

public class VRCFuryBuilder {
    public void TestRun(GameObject originalObject) {
        if (originalObject.name.StartsWith("VRCF ")) {
            EditorUtility.DisplayDialog("VRCFury Error", "This object is already the output of a VRCF test build.", "Ok");
            return;
        }
        var cloneName = "VRCF Test Build for " + originalObject.name;
        var exists = originalObject.scene
            .GetRootGameObjects()
            .FirstOrDefault(o => o.name == cloneName);
        if (exists) {
            Object.DestroyImmediate(exists);
        }
        var clone = Object.Instantiate(originalObject);
        if (!clone.activeSelf) {
            clone.SetActive(true);
        }
        if (clone.scene != originalObject.scene) {
            SceneManager.MoveGameObjectToScene(clone, originalObject.scene);
        }
        clone.name = cloneName;
        var result = SafeRun(originalObject, clone);
        if (result) {
            Selection.SetActiveObjectWithContext(clone, clone);
        } else {
            Object.DestroyImmediate(clone);
        }
    }
    
    public bool SafeRun(GameObject originalObject, GameObject avatarObject) {
        Debug.Log("VRCFury invoked on " + avatarObject.name + " ...");

        if (avatarObject.GetComponentsInChildren<VRCFury>(true).Length == 0) {
            Debug.Log("VRCFury components not found in avatar. Skipping.");
            return true;
        }

        var result = true;
        try {
            Run(originalObject, avatarObject);
        } catch(Exception e) {
            result = false;
            Debug.LogException(e);
            while (e is TargetInvocationException) {
                e = (e as TargetInvocationException).InnerException;
            }
            EditorUtility.DisplayDialog("VRCFury Error", "VRCFury encountered an error.\n\n" + e.Message, "Ok");
        }

        AssetDatabase.SaveAssets();
        EditorUtility.ClearProgressBar();
        return result;
    }

    private void Run(GameObject originalObject, GameObject avatarObject) {
        var progress = new ProgressBar("VRCFury is building ...");

        // Unhook everything from our assets before we delete them
        progress.Progress(0, "Cleaning up old VRCF cruft from avatar (in case of old builds)");
        DetachFromAvatar(originalObject);
        DetachFromAvatar(avatarObject);

        // Nuke all our old generated assets
        progress.Progress(0.1, "Clearing generated assets");
        var avatarPath = avatarObject.scene.path;
        if (string.IsNullOrEmpty(avatarPath)) {
            throw new Exception("Failed to find file path to avatar scene");
        }
        var tmpDir = "Assets/_VRCFury/" + VRCFuryEditorUtils.MakeFilenameSafe(originalObject.name);
        if (Directory.Exists(tmpDir)) {
            foreach (var asset in AssetDatabase.FindAssets("", new[] { tmpDir })) {
                var path = AssetDatabase.GUIDToAssetPath(asset);
                AssetDatabase.DeleteAsset(path);
            }
        }
        Directory.CreateDirectory(tmpDir);

        // Create our new copies of the assets, and attach them
        progress.Progress(0.2, "Attaching to avatar");
        var avatar = avatarObject.GetComponent<VRCAvatarDescriptor>();
        var fxController = GetOrCreateAvatarFx(avatar, tmpDir, originalObject.name);
        var menu = GetOrCreateAvatarMenu(avatar, tmpDir, originalObject.name);
        var syncedParams = GetOrCreateAvatarParams(avatar, tmpDir, originalObject.name);
        AttachToAvatar(avatarObject, fxController, menu, syncedParams);

        progress.Progress(0.3, "Joining Menus");
        MenuSplitter.JoinMenus(menu);

        // Apply configs
        var menuManager = new MenuManager(menu, tmpDir);
        var paramsManager = new ParamManager(syncedParams);
        var controllerManager = new ControllerManager(fxController, tmpDir, paramsManager, VRCAvatarDescriptor.AnimLayerType.FX);
        var motions = new ClipBuilder(avatarObject);
        ApplyFuryConfigs(
            controllerManager,
            menuManager,
            paramsManager,
            motions,
            tmpDir,
            avatarObject,
            progress.Partial(0.3, 0.8)
        );
        
        progress.Progress(0.8, "Splitting Menus");
        MenuSplitter.SplitMenus(menu);

        progress.Progress(0.9, "Removing Junk Components");
        foreach (var c in avatarObject.GetComponentsInChildren<VRCFury>(true)) {
            Object.DestroyImmediate(c);
        }
        foreach (var c in avatarObject.GetComponentsInChildren<Animator>(true)) {
            if (c.gameObject != avatarObject) Object.DestroyImmediate(c);
        }

        if (syncedParams.CalcTotalCost() > VRCExpressionParameters.MAX_PARAMETER_COST) {
            throw new Exception(
                "Avatar is out of space for parameters! Used "
                + syncedParams.CalcTotalCost() + "/" + VRCExpressionParameters.MAX_PARAMETER_COST
                + ". Delete some params from your avatar's param file, or disable some VRCFury features.");
        }

        progress.Progress(1, "Finishing Up");
        EditorUtility.SetDirty(fxController);
        EditorUtility.SetDirty(menu);
        EditorUtility.SetDirty(syncedParams);

        Debug.Log("VRCFury Finished!");
    }

    private static void ApplyFuryConfigs(
        ControllerManager controller,
        MenuManager menu,
        ParamManager prms,
        ClipBuilder motions,
        string tmpDir,
        GameObject avatarObject,
        ProgressBar progress
    ) {
        var actions = new List<FeatureBuilderAction>();
        var totalActionCount = 0;
        var totalModelCount = 0;
        var collectedFeatures = new List<FeatureModel>();

        void AddModel(FeatureModel model, GameObject configObject) {
            collectedFeatures.Add(model);
            var isProp = configObject != avatarObject;
            var builder = FeatureFinder.GetBuilder(model, configObject);
            builder.featureBaseObject = configObject;
            builder.tmpDir = tmpDir;
            builder.addOtherFeature = m => AddModel(m, configObject);
            builder.uniqueModelNum = ++totalModelCount;
            builder.allFeaturesInRun = collectedFeatures;
            var builderActions = builder.GetActions();
            actions.AddRange(builderActions);
            totalActionCount += builderActions.Count;
        }

        progress.Progress(0, "Collecting features");
        foreach (var vrcFury in avatarObject.GetComponentsInChildren<VRCFury>(true)) {
            var configObject = vrcFury.gameObject;
            var config = vrcFury.config;
            if (config.features != null) {
                Debug.Log("Importing " + config.features.Count + " features from " + configObject.name);
                foreach (var feature in config.features) {
                    AddModel(feature, configObject);
                }
            }
        }

        AddModel(new FixWriteDefaults(), avatarObject);
        
        while (actions.Count > 0) {
            var action = actions.Min();
            actions.Remove(action);
            var builder = action.GetBuilder();
            var configPath = AnimationUtility.CalculateTransformPath(builder.featureBaseObject.transform,
                avatarObject.transform);

            builder.controller = controller;
            builder.menu = menu;
            builder.prms = prms;
            builder.motions = motions;
            builder.avatarObject = avatarObject;
            
            var statusMessage = "Applying " + action.GetName() + " on " + builder.avatarObject.name + " " + configPath;
            progress.Progress(1 - (actions.Count / (float)totalActionCount), statusMessage);

            action.Call();
        }
    }

    private static AnimatorController GetOrCreateAvatarFx(VRCAvatarDescriptor avatar, string tmpDir, string avatarName) {
        var origFx = VRCAvatarUtils.GetAvatarFx(avatar);
        var newPath = tmpDir + "/VRCFury for " + VRCFuryEditorUtils.MakeFilenameSafe(avatarName) + ".controller";
        if (origFx == null) {
            return AnimatorController.CreateAnimatorControllerAtPath(newPath);
        }
        AssetDatabase.CopyAsset(AssetDatabase.GetAssetPath(origFx), newPath);
        return AssetDatabase.LoadAssetAtPath<AnimatorController>(newPath);
    }

    private static VRCExpressionsMenu GetOrCreateAvatarMenu(VRCAvatarDescriptor avatar, string tmpDir, string avatarName) {
        var origMenu = VRCAvatarUtils.GetAvatarMenu(avatar);
        var newPath = tmpDir + "/VRCFury Menu for " + VRCFuryEditorUtils.MakeFilenameSafe(avatarName) + ".asset";
        var menu = ScriptableObject.CreateInstance<VRCExpressionsMenu>();
        AssetDatabase.CreateAsset(menu, newPath);
        if (origMenu != null) {
            var menuManager = new MenuManager(menu, tmpDir);
            menuManager.MergeMenu(origMenu);
        }
        return menu;
    }

    private static VRCExpressionParameters GetOrCreateAvatarParams(VRCAvatarDescriptor avatar, string tmpDir, string avatarName) {
        var origParams = VRCAvatarUtils.GetAvatarParams(avatar);
        var newPath = tmpDir + "/VRCFury Params for " + VRCFuryEditorUtils.MakeFilenameSafe(avatarName) + ".asset";
        if (origParams == null) {
            var prms = ScriptableObject.CreateInstance<VRCExpressionParameters>();
            prms.parameters = new VRCExpressionParameters.Parameter[]{};
            AssetDatabase.CreateAsset(prms, newPath);
            return prms;
        }
        AssetDatabase.CopyAsset(AssetDatabase.GetAssetPath(origParams), newPath);
        return AssetDatabase.LoadAssetAtPath<VRCExpressionParameters>(newPath);
    }

    public static void DetachFromAvatar(GameObject avatarObject) {
        var animator = avatarObject.GetComponent<Animator>();
        if (animator != null) {
            if (IsVrcfAsset(animator.runtimeAnimatorController)) {
                animator.runtimeAnimatorController = null;
            }
        }

        var avatar = avatarObject.GetComponent<VRCAvatarDescriptor>();

        var fx = VRCAvatarUtils.GetAvatarFx(avatar);
        if (IsVrcfAsset(fx)) {
            VRCAvatarUtils.SetAvatarFx(avatar, null);
        } else if (fx != null) {
            ControllerManager.PurgeFromAnimator(fx, VRCAvatarDescriptor.AnimLayerType.FX);
        }

        var menu = VRCAvatarUtils.GetAvatarMenu(avatar);
        if (IsVrcfAsset(menu)) {
            VRCAvatarUtils.SetAvatarMenu(avatar, null);
        } else if (menu != null) {
            MenuSplitter.JoinMenus(menu);
            MenuManager.PurgeFromMenu(menu);
            MenuSplitter.SplitMenus(menu);
        }

        var prms = VRCAvatarUtils.GetAvatarParams(avatar);
        if (IsVrcfAsset(prms)) {
            VRCAvatarUtils.SetAvatarParams(avatar, null);
        } else if (prms != null) {
            ParamManager.PurgeFromParams(prms);
        }

        EditorUtility.SetDirty(avatar);
    }

    private static void AttachToAvatar(GameObject avatarObject, AnimatorController fx, VRCExpressionsMenu menu, VRCExpressionParameters prms) {
        var avatar = avatarObject.GetComponent<VRCAvatarDescriptor>();
        var animator = avatarObject.GetComponent<Animator>();

        VRCAvatarUtils.SetAvatarFx(avatar, fx);
        if (animator != null) animator.runtimeAnimatorController = fx;
        avatar.customExpressions = true;
        avatar.expressionsMenu = menu;
        avatar.expressionParameters = prms;

        EditorUtility.SetDirty(avatar);
    }

    public static bool IsVrcfAsset(Object obj) {
        return obj != null && AssetDatabase.GetAssetPath(obj).Contains("_VRCFury");
    }
}

}
