using System;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;
using VRC.SDK3.Avatars.Components;
using VRC.SDK3.Avatars.ScriptableObjects;
using VRCF.Feature;
using VRCF.Inspector;
using VRCF.Model;
using VRCF.Model.Feature;
using Object = UnityEngine.Object;

namespace VRCF.Builder {

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
        // Unhook everything from our assets before we delete them
        Progress(0, "Detaching from avatar");
        DetachFromAvatar(avatarObject);
        if (vrcCloneObject != null) DetachFromAvatar(vrcCloneObject);

        // Nuke all our old generated assets
        Progress(0.1, "Clearing generated assets");
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
        var avatar = avatarObject.GetComponent(typeof(VRCAvatarDescriptor)) as VRCAvatarDescriptor;
        var fxController = GetOrCreateAvatarFx(avatar, tmpDir);
        var menu = GetOrCreateAvatarMenu(avatar, tmpDir);
        var syncedParams = GetOrCreateAvatarParams(avatar, tmpDir);

        // Attach our assets back to the avatar
        Progress(0.2, "Attaching to avatar");
        AttachToAvatar(avatarObject, fxController, menu, syncedParams);
        if (vrcCloneObject != null) AttachToAvatar(vrcCloneObject, fxController, menu, syncedParams);

        // Third party integrations (if this is a fully-managed controller)
        if (IsVrcfAsset(fxController)) {
            Progress(0.3, "Third-Party Integrations");
            VRCFuryTPSIntegration.Run(avatarObject, fxController, tmpDir);
            // This is kinda broken, since it won't work right during upload with the clone object
            //VRCFuryLensIntegration.Run(avatarObject);
        }

        // Remove components that shouldn't be lying around
        Progress(0.4, "Removing Junk Components");
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

        // Do everything!
        ApplyFuryConfigs(fxController, menu, syncedParams, tmpDir, avatarObject);

        Progress(1, "Finishing Up");
        EditorUtility.SetDirty(fxController);
        EditorUtility.SetDirty(menu);
        EditorUtility.SetDirty(syncedParams);

        Debug.Log("VRCFury Finished!");
    }

    private static void ApplyFuryConfigs(
        AnimatorController fxController,
        VRCExpressionsMenu menu,
        VRCExpressionParameters syncedParams,
        string tmpDir,
        GameObject avatarObject
    ) {
        var manager = new VRCFuryNameManager(menu, syncedParams, fxController, tmpDir, IsVrcfAsset(menu));
        if (manager.IsUsingWriteDefaults) {
            Debug.Log("Detected usage of 'Write Defaults', using it for generated states too.");
        }
        var motions = new VRCFuryClipUtils(avatarObject);
        var noopClip = manager.GetNoopClip();
        var defaultClip = manager.NewClip("Defaults");
        var defaultLayer = manager.NewLayer("Defaults");
        defaultLayer.NewState("Defaults").WithAnimation(defaultClip);

        Progress(0.5, "Scanning for features");
        var features = new List<Tuple<GameObject, FeatureModel>>();
        foreach (var vrcFury in avatarObject.GetComponentsInChildren<VRCFury>(true)) {
            var configObject = vrcFury.gameObject;
            var config = VRCFuryConfigUpgrader.GetConfig(vrcFury);
            if (config.features != null) {
                Debug.Log("Importing " + config.features.Count + " features from " + configObject.name);
                foreach (var feature in config.features) {
                    features.Add(Tuple.Create(configObject, feature));
                }
            }
        }

        var i = 0;
        foreach (var featureTuple in features) {
            i++;
            var configObject = featureTuple.Item1;
            Progress((0.5 + 0.5 * ((double)i/features.Count)), "Adding feature to " + configObject);
            var feature = featureTuple.Item2;
            var isProp = configObject != avatarObject;
            Action<BaseFeature> configureFeature = null;
            configureFeature = f => {
                f.manager = manager;
                f.motions = motions;
                f.defaultClip = defaultClip;
                f.noopClip = noopClip;
                f.avatarObject = avatarObject;
                f.featureBaseObject = configObject;
                f.addOtherFeature = model => FeatureFinder.RunFeature(model, configureFeature, isProp);
            };
            FeatureFinder.RunFeature(feature, configureFeature, isProp);
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

    public static bool IsVrcfAsset(Object obj) {
        return obj != null && AssetDatabase.GetAssetPath(obj).Contains("_VRCFury");
    }

    private static void Progress(double progress, string info) {
        EditorUtility.DisplayProgressBar("VRCFury is building ...", info, (float)progress);
    }
}

}
