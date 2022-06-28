using System;
using UnityEngine;
using UnityEditor;
using UnityEditor.Animations;
using System.Collections.Generic;
using VRC.SDK3.Avatars.Components;
using VRCF.Model;
using System.IO;
using VRC.SDK3.Avatars.ScriptableObjects;
using VRCF.Feature;

namespace VRCF.Builder {

public class VRCFuryBuilder {
    public bool SafeRun(VRCFuryConfig config, GameObject avatarObject) {
        EditorUtility.DisplayProgressBar("VRCFury is building ...", "", 0.5f);
        bool result;
        try {
            result = Run(config, avatarObject);
        } catch(Exception e) {
            result = false;
            Debug.LogException(e);
            EditorUtility.DisplayDialog("VRCFury Error", "An exception was thrown by VRCFury. Check the unity console.", "Ok");
        }

        AssetDatabase.SaveAssets();
        EditorUtility.ClearProgressBar();
        return result;
    }

    private bool Run(VRCFuryConfig config, GameObject avatarObject) {
        this.avatarObject = avatarObject;

        Debug.Log("VRCFury is running for " + avatarObject.name + "...");
        var avatar = avatarObject.GetComponent(typeof(VRCAvatarDescriptor)) as VRCAvatarDescriptor;
        var fxLayer = avatar.baseAnimationLayers[4];
        Action saveFxLayer = () => { avatar.baseAnimationLayers[4] = fxLayer; };

        // Unhook everything from our assets before we delete them
        var animator = avatarObject.GetComponent<Animator>();
        if (animator != null) {
            if (IsVrcfAsset(animator.runtimeAnimatorController)) {
                animator.runtimeAnimatorController = null;
            }
        }
        AnimatorController fxController = null;
        if (IsVrcfAsset(fxLayer.animatorController)) {
            fxLayer.animatorController = null;
            saveFxLayer();
        } else if (avatar.customizeAnimationLayers && !fxLayer.isDefault && fxLayer.animatorController != null) {
            fxController = (AnimatorController)fxLayer.animatorController;
            VRCFuryNameManager.PurgeFromAnimator(fxController);
        }
        VRCExpressionsMenu menu = null;
        if (IsVrcfAsset(avatar.expressionsMenu)) {
            avatar.expressionsMenu = null;
        } else if (avatar.customExpressions && avatar.expressionsMenu != null) {
            menu = avatar.expressionsMenu;
            VRCFuryNameManager.PurgeFromMenu(menu);
        }
        VRCExpressionParameters syncedParams = null;
        if (IsVrcfAsset(avatar.expressionParameters)) {
            avatar.expressionParameters = null;
        } else if (avatar.customExpressions && avatar.expressionParameters != null) {
            syncedParams = avatar.expressionParameters;
            VRCFuryNameManager.PurgeFromParams(syncedParams);
        }

        // Nuke all our old generated assets
        var avatarPath = avatarObject.scene.path;
        if (string.IsNullOrEmpty(avatarPath)) {
            EditorUtility.DisplayDialog("VRCFury Error", "Failed to find file path to avatar scene", "Ok");
            return false;
        }
        var tmpDir = Path.GetDirectoryName(avatarPath) + "/_VRCFury/" + avatarObject.name;
        if (Directory.Exists(tmpDir)) {
            foreach (var asset in AssetDatabase.FindAssets("", new string[] { tmpDir })) {
                var path = AssetDatabase.GUIDToAssetPath(asset);
                AssetDatabase.DeleteAsset(path);
            }
        }
        Directory.CreateDirectory(tmpDir);

        var thirdPartyIntegrations = false;
        if (fxController == null) {
            fxController = AnimatorController.CreateAnimatorControllerAtPath(tmpDir + "/VRCFury for " + avatarObject.name + ".controller");
            avatar.customizeAnimationLayers = true;
            fxLayer.isDefault = false;
            fxLayer.type = VRCAvatarDescriptor.AnimLayerType.FX;
            fxLayer.animatorController = fxController;
            saveFxLayer();
            if (animator != null) animator.runtimeAnimatorController = fxController;
            thirdPartyIntegrations = true;
        }
        var useMenuRoot = false;
        if (menu == null) {
            useMenuRoot = true;
            avatar.customExpressions = true;
            menu = avatar.expressionsMenu = ScriptableObject.CreateInstance<VRCExpressionsMenu>();
            menu.controls = new List<VRCExpressionsMenu.Control>();
            AssetDatabase.CreateAsset(menu, tmpDir + "/VRCFury Menu for " + avatarObject.name + ".asset");
        }
        if (syncedParams == null) {
            avatar.customExpressions = true;
            syncedParams = avatar.expressionParameters = ScriptableObject.CreateInstance<VRCExpressionParameters>();
            syncedParams.parameters = new VRCExpressionParameters.Parameter[]{};
            AssetDatabase.CreateAsset(syncedParams, tmpDir + "/VRCFury Params for " + avatarObject.name + ".asset");
        }

        EditorUtility.SetDirty(avatar);

        if (thirdPartyIntegrations) {
            VRCFuryTPSIntegration.Run(avatarObject, fxController, tmpDir);
            VRCFuryLensIntegration.Run(avatarObject);
        }

        manager = new VRCFuryNameManager(menu, syncedParams, fxController, tmpDir, useMenuRoot);
        baseFile = AssetDatabase.GetAssetPath(fxController);
        motions = new VRCFuryClipUtils(avatarObject);

        // REMOVE ANIMATORS FROM PREFAB INSTANCES (often used for prop testing)
        foreach (var otherAnimator in avatarObject.GetComponentsInChildren<Animator>(true)) {
            if (otherAnimator.gameObject != avatarObject && PrefabUtility.IsPartOfPrefabInstance(otherAnimator.gameObject)) {
                UnityEngine.Object.DestroyImmediate(otherAnimator);
            }
        }

        // DEFAULTS
        noopClip = manager.GetNoopClip();
        defaultClip = manager.NewClip("Defaults");
        var defaultLayer = manager.NewLayer("Defaults");
        defaultLayer.NewState("Defaults").WithAnimation(defaultClip);

        if (config != null) {
            handleConfig(config, null);
        }
        foreach (var otherConfig in avatarObject.GetComponentsInChildren<VRCFury>(true)) {
            if (otherConfig.gameObject == avatarObject) continue;
            Debug.Log("Importing config from " + otherConfig.gameObject.name);
            handleConfig(otherConfig.GetConfig(), otherConfig.gameObject);
        }

        Debug.Log("VRCFury Finished!");

        return true;
    }

    private void handleConfig(VRCFuryConfig config, GameObject featureBaseObject) {
        if (config.features != null) {
            foreach (var feature in config.features) {
                handleFeature(feature, featureBaseObject);
            }
        }
    }

    private void handleFeature(VRCF.Model.Feature.FeatureModel feature, GameObject featureBaseObject) {
        Action<BaseFeature> configureFeature = null;
        configureFeature = f => {
            f.manager = manager;
            f.motions = motions;
            f.defaultClip = defaultClip;
            f.noopClip = noopClip;
            f.avatarObject = avatarObject;
            f.featureBaseObject = featureBaseObject;
            f.addOtherFeature = model => FeatureFinder.RunFeature(model, configureFeature);
        };
        FeatureFinder.RunFeature(feature, configureFeature);
    }

    private VRCFuryNameManager manager;
    private VRCFuryClipUtils motions;
    private GameObject avatarObject;
    private string baseFile;
    private AnimationClip noopClip;
    private AnimationClip defaultClip;

    private GameObject find(string path) {
        var found = avatarObject.transform.Find(path)?.gameObject;
        if (found == null) {
            throw new Exception("Failed to find path '" + path + "'");
        }
        return found;
    }

    private SkinnedMeshRenderer findSkin(string path) {
        return find(path).GetComponent(typeof(SkinnedMeshRenderer)) as SkinnedMeshRenderer;
    }

    public static bool IsVrcfAsset(UnityEngine.Object obj) {
        return obj != null && AssetDatabase.GetAssetPath(obj).Contains("_VRCFury");
    }


}

}
