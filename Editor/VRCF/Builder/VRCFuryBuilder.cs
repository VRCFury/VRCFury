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
    public bool SafeRun(GameObject avatarObject) {
        Debug.Log("VRCFury invoked on " + avatarObject.name + " ...");

        if (avatarObject.GetComponentsInChildren<VRCFury>(true).Length == 0) {
            Debug.Log("VRCFury components not found in avatar. Skipping.");
            return true;
        }

        EditorUtility.DisplayProgressBar("VRCFury is building ...", "", 0.5f);
        bool result = true;
        try {
            Run(avatarObject);
        } catch(Exception e) {
            result = false;
            Debug.LogException(e);
            EditorUtility.DisplayDialog("VRCFury Error", "An exception was thrown by VRCFury. Check the unity console.", "Ok");
        }

        AssetDatabase.SaveAssets();
        EditorUtility.ClearProgressBar();
        return result;
    }

    private void Run(GameObject avatarObject) {
        // When vrchat is uploading our avatar, we are actually operating on a clone of the avatar object.
        // Let's get a reference to the original avatar, so we can apply our changes to it as well.
        GameObject vrcCloneObject = null;
        if (avatarObject.name.EndsWith("(Clone)")) {
            GameObject original = null;
            foreach (var desc in GameObject.FindObjectsOfType<VRCAvatarDescriptor>()) {
                if (desc.gameObject.name+"(Clone)" == avatarObject.name && desc.gameObject.activeInHierarchy) {
                    original = desc.gameObject;
                    break;
                }
            }
            if (original == null) {
                throw new Exception("Failed to find original avatar object during vrchat upload");
            }
            Debug.Log("Found original avatar object for VRC upload: " + original);
            vrcCloneObject = avatarObject;
            avatarObject = original;
        }

        // Unhook everything from our assets before we delete them
        DetachFromAvatar(avatarObject);
        if (vrcCloneObject != null) DetachFromAvatar(vrcCloneObject);

        // Nuke all our old generated assets
        var avatarPath = avatarObject.scene.path;
        if (string.IsNullOrEmpty(avatarPath)) {
            throw new Exception("Failed to find file path to avatar scene");
        }
        var tmpDir = Path.GetDirectoryName(avatarPath) + "/_VRCFury/" + avatarObject.name;
        if (Directory.Exists(tmpDir)) {
            foreach (var asset in AssetDatabase.FindAssets("", new string[] { tmpDir })) {
                var path = AssetDatabase.GUIDToAssetPath(asset);
                AssetDatabase.DeleteAsset(path);
            }
        }
        Directory.CreateDirectory(tmpDir);

        // Figure out what assets we're going to be messing with
        var avatar = avatarObject.GetComponent(typeof(VRCAvatarDescriptor)) as VRCAvatarDescriptor;
        var fxController = GetOrCreateAvatarFx(avatar, tmpDir);
        var menu = GetOrCreateAvatarMenu(avatar, tmpDir);
        var syncedParams = GetOrCreateAvatarParams(avatar, tmpDir);

        // Attach our assets back to the avatar
        AttachToAvatar(avatarObject, fxController, menu, syncedParams);
        if (vrcCloneObject != null) AttachToAvatar(vrcCloneObject, fxController, menu, syncedParams);

        // Third party integrations (if this is a fully-managed controller)
        if (IsVrcfAsset(fxController)) {
            VRCFuryTPSIntegration.Run(avatarObject, fxController, tmpDir);
            // This is kinda broken, since it won't work right during upload with the clone object
            //VRCFuryLensIntegration.Run(avatarObject);
        }

        // Remove components that shouldn't be lying around
        foreach (var c in avatarObject.GetComponentsInChildren<Animator>(true)) {
            if (c.gameObject != avatarObject && PrefabUtility.IsPartOfPrefabInstance(c.gameObject)) {
                GameObject.DestroyImmediate(c);
            }
        }
        if (vrcCloneObject != null) {
            foreach (var c in vrcCloneObject.GetComponentsInChildren<VRCFury>(true)) {
                GameObject.DestroyImmediate(c);
            }
            foreach (var c in vrcCloneObject.GetComponentsInChildren<Animator>(true)) {
                if (c.gameObject != vrcCloneObject) GameObject.DestroyImmediate(c);
            }
        }

        // Do everything!
        ApplyFuryConfigs(fxController, menu, syncedParams, tmpDir, avatarObject);

        EditorUtility.SetDirty(fxController);
        EditorUtility.SetDirty(menu);
        EditorUtility.SetDirty(syncedParams);

        Debug.Log("VRCFury Finished!");
    }

    private void ApplyFuryConfigs(
        AnimatorController fxController,
        VRCExpressionsMenu menu,
        VRCExpressionParameters syncedParams,
        string tmpDir,
        GameObject avatarObject
    ) {
        var manager = new VRCFuryNameManager(menu, syncedParams, fxController, tmpDir, IsVrcfAsset(menu));
        var baseFile = AssetDatabase.GetAssetPath(fxController);
        var motions = new VRCFuryClipUtils(avatarObject);
        var noopClip = manager.GetNoopClip();
        var defaultClip = manager.NewClip("Defaults");
        var defaultLayer = manager.NewLayer("Defaults");
        defaultLayer.NewState("Defaults").WithAnimation(defaultClip);

        foreach (var vrcFury in avatarObject.GetComponentsInChildren<VRCFury>(true)) {
            var configObject = vrcFury.gameObject;
            Debug.Log("Importing config from " + configObject.name);
            var config = VRCFuryConfigUpgrader.GetConfig(vrcFury);
            if (config.features != null) {
                foreach (var feature in config.features) {
                    Action<BaseFeature> configureFeature = null;
                    configureFeature = f => {
                        f.manager = manager;
                        f.motions = motions;
                        f.defaultClip = defaultClip;
                        f.noopClip = noopClip;
                        f.avatarObject = avatarObject;
                        f.featureBaseObject = configObject;
                        f.addOtherFeature = model => FeatureFinder.RunFeature(model, configureFeature);
                    };
                    FeatureFinder.RunFeature(feature, configureFeature);
                }
            }
        }
    }

    private static AnimatorController GetAvatarFx(VRCAvatarDescriptor avatar) {
        var fxLayer = avatar.baseAnimationLayers[4];
        return avatar.customizeAnimationLayers && !fxLayer.isDefault ? (AnimatorController)fxLayer.animatorController : null;
    }
    private static AnimatorController GetOrCreateAvatarFx(VRCAvatarDescriptor avatar, string tmpDir) {
        var fx = GetAvatarFx(avatar);
        if (fx == null) fx = AnimatorController.CreateAnimatorControllerAtPath(tmpDir + "/VRCFury for " + avatar.gameObject.name + ".controller");
        return fx;
    }
    private static VRCExpressionsMenu GetAvatarMenu(VRCAvatarDescriptor avatar) {
        return avatar.customExpressions ? avatar.expressionsMenu : null;
    }
    private static VRCExpressionsMenu GetOrCreateAvatarMenu(VRCAvatarDescriptor avatar, string tmpDir) {
        var menu = GetAvatarMenu(avatar);
        if (menu == null) {
            menu = ScriptableObject.CreateInstance<VRCExpressionsMenu>();
            menu.controls = new List<VRCExpressionsMenu.Control>();
            AssetDatabase.CreateAsset(menu, tmpDir + "/VRCFury Menu for " + avatar.gameObject.name + ".asset");
        }
        return menu;
    }
    private static VRCExpressionParameters GetAvatarParams(VRCAvatarDescriptor avatar) {
        return avatar.customExpressions ? avatar.expressionParameters : null;
    }
    private static VRCExpressionParameters GetOrCreateAvatarParams(VRCAvatarDescriptor avatar, string tmpDir) {
        var prms = GetAvatarParams(avatar);
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

        var avatar = avatarObject.GetComponent(typeof(VRCAvatarDescriptor)) as VRCAvatarDescriptor;
        var fx = GetAvatarFx(avatar);
        if (IsVrcfAsset(fx)) {
            var fxLayer = avatar.baseAnimationLayers[4];
            fxLayer.animatorController = null;
            avatar.baseAnimationLayers[4] = fxLayer;
        } else if (fx != null) {
            VRCFuryNameManager.PurgeFromAnimator(fx);
        }

        var menu = GetAvatarMenu(avatar);
        if (IsVrcfAsset(menu)) {
            avatar.expressionsMenu = null;
        } else if (menu != null) {
            VRCFuryNameManager.PurgeFromMenu(menu);
        }

        var prms = GetAvatarParams(avatar);
        if (IsVrcfAsset(prms)) {
            avatar.expressionParameters = null;
        } else if (prms != null) {
            VRCFuryNameManager.PurgeFromParams(prms);
        }

        EditorUtility.SetDirty(avatar);
    }

    private static void AttachToAvatar(GameObject avatarObject, AnimatorController fx, VRCExpressionsMenu menu, VRCExpressionParameters prms) {
        var avatar = avatarObject.GetComponent(typeof(VRCAvatarDescriptor)) as VRCAvatarDescriptor;
        var animator = avatarObject.GetComponent<Animator>();

        var fxLayer = avatar.baseAnimationLayers[4];
        avatar.customizeAnimationLayers = true;
        fxLayer.isDefault = false;
        fxLayer.type = VRCAvatarDescriptor.AnimLayerType.FX;
        fxLayer.animatorController = fx;
        avatar.baseAnimationLayers[4] = fxLayer;
        if (animator != null) animator.runtimeAnimatorController = fx;
        avatar.customExpressions = true;
        avatar.expressionsMenu = menu;
        avatar.expressionParameters = prms;

        EditorUtility.SetDirty(avatar);
    }

    public static bool IsVrcfAsset(UnityEngine.Object obj) {
        return obj != null && AssetDatabase.GetAssetPath(obj).Contains("_VRCFury");
    }


}

}
