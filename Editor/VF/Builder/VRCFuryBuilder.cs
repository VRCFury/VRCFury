using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;
using VRC.SDK3.Avatars.Components;
using VRC.SDK3.Avatars.ScriptableObjects;
using VF.Feature;
using VF.Feature.Base;
using VF.Model;
using VF.Model.Feature;
using Object = UnityEngine.Object;

namespace VF.Builder {

public class VRCFuryBuilder {
    public bool SafeRun(GameObject avatarObject, GameObject vrcCloneObject = null) {
        Debug.Log("VRCFury invoked on " + avatarObject.name + " ...");

        if (avatarObject.GetComponentsInChildren<VRCFury>(true).Length == 0) {
            Debug.Log("VRCFury components not found in avatar. Skipping.");
            return true;
        }

        var result = true;
        try {
            Run(avatarObject, vrcCloneObject);
        } catch(Exception e) {
            result = false;
            Debug.LogException(e);
            EditorUtility.DisplayDialog("VRCFury Error", "An exception was thrown by VRCFury. Check the unity console.", "Ok");
        }

        AssetDatabase.SaveAssets();
        EditorUtility.ClearProgressBar();
        return result;
    }

    private void Run(GameObject avatarObject, GameObject vrcCloneObject) {
        var progress = new ProgressBar("VRCFury is building ...");

        // Unhook everything from our assets before we delete them
        progress.Progress(0, "Detaching from avatar");
        DetachFromAvatar(avatarObject);
        if (vrcCloneObject != null) DetachFromAvatar(vrcCloneObject);

        // Nuke all our old generated assets
        progress.Progress(0.1, "Clearing generated assets");
        var avatarPath = avatarObject.scene.path;
        if (string.IsNullOrEmpty(avatarPath)) {
            throw new Exception("Failed to find file path to avatar scene");
        }
        var tmpDir = Path.GetDirectoryName(avatarPath) + "/_VRCFury/" + avatarObject.name;
        if (Directory.Exists(tmpDir)) {
            foreach (var asset in AssetDatabase.FindAssets("", new[] { tmpDir })) {
                var path = AssetDatabase.GUIDToAssetPath(asset);
                AssetDatabase.DeleteAsset(path);
            }
        }
        Directory.CreateDirectory(tmpDir);

        // Figure out what assets we're going to be messing with
        var avatar = avatarObject.GetComponent<VRCAvatarDescriptor>();
        var fxController = GetOrCreateAvatarFx(avatar, tmpDir);
        var menu = GetOrCreateAvatarMenu(avatar, tmpDir);
        var syncedParams = GetOrCreateAvatarParams(avatar, tmpDir);

        // Attach our assets back to the avatar
        progress.Progress(0.2, "Attaching to avatar");
        AttachToAvatar(avatarObject, fxController, menu, syncedParams);
        if (vrcCloneObject != null) AttachToAvatar(vrcCloneObject, fxController, menu, syncedParams);

        // Apply configs
        var manager = new VRCFuryNameManager(menu, syncedParams, fxController, tmpDir, IsVrcfAsset(menu));
        var motions = new ClipBuilder(avatarObject);
        var defaultClip = manager.NewClip("Defaults");
        ApplyFuryConfigs(manager, motions, tmpDir, defaultClip, avatarObject, vrcCloneObject, progress.Partial(0.3, 0.8));
        
        progress.Progress(0.8, "Collecting default states");
        AddDefaultsLayer(manager, avatarObject, defaultClip);

        progress.Progress(0.85, "Adjusting 'Write Defaults'");
        UseWriteDefaultsIfNeeded(manager);
        
        progress.Progress(0.9, "Removing Junk Components");
        foreach (var c in avatarObject.GetComponentsInChildren<Animator>(true)) {
            if (c.gameObject != avatarObject && PrefabUtility.IsPartOfPrefabInstance(c.gameObject)) {
                Object.DestroyImmediate(c);
            }
        }
        if (vrcCloneObject != null) {
            foreach (var c in vrcCloneObject.GetComponentsInChildren<VRCFury>(true)) {
                Object.DestroyImmediate(c);
            }
            foreach (var c in vrcCloneObject.GetComponentsInChildren<Animator>(true)) {
                if (c.gameObject != vrcCloneObject) Object.DestroyImmediate(c);
            }
        }
        
        progress.Progress(0.95, "Splitting Menus");
        manager.SplitMenus();

        progress.Progress(1, "Finishing Up");
        EditorUtility.SetDirty(fxController);
        EditorUtility.SetDirty(menu);
        EditorUtility.SetDirty(syncedParams);

        Debug.Log("VRCFury Finished!");
    }

    private static void ApplyFuryConfigs(
        VRCFuryNameManager manager,
        ClipBuilder motions,
        string tmpDir,
        AnimationClip defaultClip,
        GameObject avatarObject,
        GameObject vrcCloneAvatarObject,
        ProgressBar progress
    ) {
        var actions = new List<FeatureBuilderAction>();
        var totalActionCount = 0;

        void AddModel(FeatureModel model, GameObject configObject) {
            var isProp = configObject != avatarObject;
            var builder = FeatureFinder.GetBuilder(model, isProp);
            builder.featureBaseObject = configObject;
            builder.tmpDir = tmpDir;
            builder.addOtherFeature = m => AddModel(m, configObject);
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
        
        while (actions.Count > 0) {
            var action = actions.Min();
            actions.Remove(action);
            var builder = action.GetBuilder();
            
            var statusMessage = "Applying " + action.GetName() + " on " + builder.featureBaseObject.name;
            progress.Progress(1 - (actions.Count / (float)totalActionCount), statusMessage);
            
            var applyToClone = action.applyToVrcClone();
            if (applyToClone && vrcCloneAvatarObject == null) continue;
            builder.manager = applyToClone ? null : manager;
            builder.motions = applyToClone ? null : motions;
            builder.defaultClip = applyToClone ? null : defaultClip;
            builder.avatarObject = applyToClone ? vrcCloneAvatarObject : avatarObject;
            var featureBaseObjectBak = builder.featureBaseObject;
            if (applyToClone) {
                var configPath = AnimationUtility.CalculateTransformPath(builder.featureBaseObject.transform,
                    builder.avatarObject.transform);
                var configOnClone = vrcCloneAvatarObject.transform.Find(configPath).gameObject;
                builder.featureBaseObject = configOnClone;
            }
            action.Call();
            builder.featureBaseObject = featureBaseObjectBak;
        }
    }

    private static AnimatorController GetOrCreateAvatarFx(VRCAvatarDescriptor avatar, string tmpDir) {
        var fx = VRCAvatarUtils.GetAvatarFx(avatar);
        if (fx == null) fx = AnimatorController.CreateAnimatorControllerAtPath(tmpDir + "/VRCFury for " + avatar.gameObject.name + ".controller");
        return fx;
    }

    private static VRCExpressionsMenu GetOrCreateAvatarMenu(VRCAvatarDescriptor avatar, string tmpDir) {
        var menu = VRCAvatarUtils.GetAvatarMenu(avatar);
        if (menu == null) {
            menu = ScriptableObject.CreateInstance<VRCExpressionsMenu>();
            menu.controls = new List<VRCExpressionsMenu.Control>();
            AssetDatabase.CreateAsset(menu, tmpDir + "/VRCFury Menu for " + avatar.gameObject.name + ".asset");
        }
        return menu;
    }

    private static VRCExpressionParameters GetOrCreateAvatarParams(VRCAvatarDescriptor avatar, string tmpDir) {
        var prms = VRCAvatarUtils.GetAvatarParams(avatar);
        if (prms == null) {
            prms = ScriptableObject.CreateInstance<VRCExpressionParameters>();
            prms.parameters = new VRCExpressionParameters.Parameter[]{};
            AssetDatabase.CreateAsset(prms, tmpDir + "/VRCFury Params for " + avatar.gameObject.name + ".asset");
        }
        return prms;
    }

    private static void DetachFromAvatar(GameObject avatarObject) {
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
            VRCFuryNameManager.PurgeFromAnimator(fx);
        }

        var menu = VRCAvatarUtils.GetAvatarMenu(avatar);
        if (IsVrcfAsset(menu)) {
            VRCAvatarUtils.SetAvatarMenu(avatar, null);
        } else if (menu != null) {
            VRCFuryNameManager.PurgeFromMenu(menu);
        }

        var prms = VRCAvatarUtils.GetAvatarParams(avatar);
        if (IsVrcfAsset(prms)) {
            VRCAvatarUtils.SetAvatarParams(avatar, null);
        } else if (prms != null) {
            VRCFuryNameManager.PurgeFromParams(prms);
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

    private static void AddDefaultsLayer(VRCFuryNameManager manager, GameObject avatarObject, AnimationClip defaultClip) {
        var defaultLayer = manager.NewLayer("Defaults", true);
        defaultLayer.NewState("Defaults").WithAnimation(defaultClip);
        foreach (var layer in manager.GetManagedLayers()) {
            DefaultClipBuilder.CollectDefaults(layer, defaultClip, avatarObject);
        }
    }
    
    private static void UseWriteDefaultsIfNeeded(VRCFuryNameManager manager) {
        var offStates = 0;
        var onStates = 0;
        foreach (var layer in manager.GetUnmanagedLayers()) {
            DefaultClipBuilder.ForEachState(layer, state => {
                if (state.writeDefaultValues) onStates++;
                else offStates++;
            });
        }

        if (onStates > 0 && offStates > 0) {
            Debug.LogWarning("Your animation controller contains a mix of Write Defaults ON and Write Defaults OFF states." +
                           " (" + onStates + " on, " + offStates + " off)." +
                           " Doing this may cause weird issues to happen with your animations in game." +
                           " This is not an issue with VRCFury, but an issue with your avatar's custom animation controller.");
        }
        
        // If half of the old states use writeDefaults, safe to assume it should be used everywhere
        var shouldUseWriteDefaults = onStates >= offStates && onStates > 0;
        if (shouldUseWriteDefaults) {
            Debug.Log("Detected usage of 'Write Defaults', adjusting generated states to use it too.");
            foreach (var layer in manager.GetManagedLayers()) {
                DefaultClipBuilder.ForEachState(layer, state => state.writeDefaultValues = true);
            }
        }
    }
}

}
