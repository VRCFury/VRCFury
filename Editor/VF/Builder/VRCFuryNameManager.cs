using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;
using VRC.SDK3.Avatars.ScriptableObjects;
using Object = UnityEngine.Object;

namespace VF.Builder {

public class VRCFuryNameManager {
    public static string prefix = "VRCFury";
    private readonly VRCExpressionsMenu rootMenu;
    private VRCExpressionsMenu fxMenu;
    private VRCExpressionsMenu lastMenu;
    private int lastMenuNum;
    private readonly VRCExpressionParameters syncedParams;
    private readonly AnimatorController ctrl;
    private readonly string tmpDir;
    private readonly bool useMenuRoot;
    private Object clipStorage;

    public VRCFuryNameManager(VRCExpressionsMenu menu, VRCExpressionParameters syncedParams, AnimatorController controller, string tmpDir, bool useMenuRoot) {
        rootMenu = menu;
        this.syncedParams = syncedParams;
        ctrl = controller;
        this.tmpDir = tmpDir;
        this.useMenuRoot = useMenuRoot;
    }

    public static void PurgeFromAnimator(AnimatorController ctrl) {
        // Clean up layers
        for (var i = 0; i < ctrl.layers.Length; i++) {
            var layer = ctrl.layers[i];
            if (layer.name.StartsWith("["+prefix+"]")) {
                ctrl.RemoveLayer(i);
                i--;
            }
        }
        // Clean up parameters
        for (var i = 0; i < ctrl.parameters.Length; i++) {
            var param = ctrl.parameters[i];
            if (param.name.StartsWith("Senky") || param.name.StartsWith(prefix+"__")) {
                ctrl.RemoveParameter(param);
                i--;
            }
        }
    }
    public static void PurgeFromParams(VRCExpressionParameters syncedParams) {
        var syncedParamsList = new List<VRCExpressionParameters.Parameter>(syncedParams.parameters);
        syncedParamsList.RemoveAll(param => param.name.StartsWith("Senky") || param.name.StartsWith(prefix+"__"));
        syncedParams.parameters = syncedParamsList.ToArray();
        EditorUtility.SetDirty(syncedParams);
    }

    public static void PurgeFromMenu(VRCExpressionsMenu rootMenu) {
        for (var i = 0; i < rootMenu.controls.Count; i++) {
            var remove = false;
            var control = rootMenu.controls[i];
            if (control.type == VRCExpressionsMenu.Control.ControlType.SubMenu && control.subMenu != null) {
                if (control.subMenu.name.StartsWith("VRCFury")) {
                    remove = true;
                }
                if (VRCFuryBuilder.IsVrcfAsset(control.subMenu)) {
                    remove = true;
                }
            }
            if (control.name == "SenkyFX" || control.name == "VRCFury") {
                remove = true;
            }
            if (remove) {
                rootMenu.controls.RemoveAt(i);
                i--;
            }
        }
        foreach (var subAsset in AssetDatabase.LoadAllAssetsAtPath(AssetDatabase.GetAssetPath(rootMenu))) {
            if (subAsset.name.StartsWith("Senky") || subAsset.name.StartsWith("VRCFury")) {
                AssetDatabase.RemoveObjectFromAsset(subAsset);
            }
        }
    }

    public AnimatorController GetRawController() {
        return ctrl;
    }
    
    public string GetTmpDir() {
        return tmpDir;
    }

    private VFAController _controller;
    private AnimationClip _noopClip;
    private VFAController GetController() {
        if (_controller == null) {
            _noopClip = NewClip("noop");
            _noopClip.SetCurve("_ignored", typeof(GameObject), "m_IsActive", AnimationCurve.Constant(0,0,0));
            _controller = new VFAController(ctrl, _noopClip);
        }
        return _controller;
    }

    public AnimationClip GetNoopClip() {
        GetController();
        return _noopClip;
    }

    public VFALayer NewLayer(string name, bool first = false) {
        return GetController().NewLayer("[" + prefix + "] " + name, first);
    }

    public IEnumerable<AnimatorControllerLayer> GetManagedLayers() {
        return ctrl.layers.Where(l => l.name.StartsWith("[" + prefix + "] "));
    }
    public IEnumerable<AnimatorControllerLayer> GetUnmanagedLayers() {
        return ctrl.layers.Where(l => !l.name.StartsWith("[" + prefix + "] "));
    }

    public void AddToClipStorage(Object asset) {
        if (clipStorage == null) {
            clipStorage = new AnimationClip();
            clipStorage.hideFlags = HideFlags.None;
            AssetDatabase.CreateAsset(clipStorage, tmpDir + "/VRCF_Clips.anim");
        }
        AssetDatabase.AddObjectToAsset(asset, clipStorage);
    }

    public AnimationClip NewClip(string name) {
        var clip = new AnimationClip();
        clip.name = prefix + "/" + name;
        clip.hideFlags = HideFlags.None;
        AddToClipStorage(clip);
        return clip;
    }
    public BlendTree NewBlendTree(string name) {
        var tree = new BlendTree();
        tree.name = prefix + "/" + name;
        tree.hideFlags = HideFlags.None;
        AddToClipStorage(tree);
        return tree;
    }

    public VRCExpressionsMenu GetFxMenu() {
        if (useMenuRoot) {
            return rootMenu;
        }
        if (fxMenu == null) {
            if (rootMenu.controls.Count >= VRCExpressionsMenu.MAX_CONTROLS) {
                throw new Exception("Root menu can't fit any more controls!");
            }
            fxMenu = ScriptableObject.CreateInstance<VRCExpressionsMenu>();
            AssetDatabase.CreateAsset(fxMenu, tmpDir + "/VRCF_Menu.asset");
            var control = new VRCExpressionsMenu.Control();
            rootMenu.controls.Add(control);
            control.name = prefix;
            control.subMenu = fxMenu;
            control.type = VRCExpressionsMenu.Control.ControlType.SubMenu;
        }
        return fxMenu;
    }
    public VRCExpressionsMenu NewTopLevelMenu(string name) {
        var fxMenu = GetFxMenu();
        if (fxMenu.controls.Count >= VRCExpressionsMenu.MAX_CONTROLS) {
            throw new Exception("Out of room for new submenus!");
        }
        var newMenu = ScriptableObject.CreateInstance<VRCExpressionsMenu>();
        AssetDatabase.CreateAsset(newMenu, tmpDir + "/VRCF_Menu_" + name + ".asset");
        var control = new VRCExpressionsMenu.Control();
        fxMenu.controls.Add(control);
        control.name = name;
        control.subMenu = newMenu;
        control.type = VRCExpressionsMenu.Control.ControlType.SubMenu;
        return newMenu;
    }
    public VRCExpressionsMenu GetNumMenu() {
        if (lastMenu == null || lastMenu.controls.Count >= VRCExpressionsMenu.MAX_CONTROLS) {
            lastMenuNum++;
            lastMenu = NewTopLevelMenu(""+lastMenuNum);
        }
        return lastMenu;
    }
    public VRCExpressionsMenu.Control NewMenuItem() {
        var menu = GetNumMenu();
        var control = new VRCExpressionsMenu.Control();
        menu.controls.Add(control);
        return control;
    }
    public void NewMenuToggle(string name, VFAParam param, float value = 1) {
        var control = NewMenuItem();
        control.name = name;
        control.type = VRCExpressionsMenu.Control.ControlType.Toggle;
        var menuParam = new VRCExpressionsMenu.Control.Parameter();
        menuParam.name = param.Name();
        control.parameter = menuParam;
        control.value = value;
    }
    public void NewMenuSlider(string name, VFANumber param) {
        var control = NewMenuItem();
        control.name = name;
        control.type = VRCExpressionsMenu.Control.ControlType.RadialPuppet;
        var menuParam = new VRCExpressionsMenu.Control.Parameter();
        menuParam.name = param.Name();
        control.subParameters = new[]{menuParam};
    }
    public void NewMenuPuppet(string name, VFANumber x, VFANumber y) {
        var control = NewMenuItem();
        control.name = name;
        control.type = VRCExpressionsMenu.Control.ControlType.TwoAxisPuppet;
        var menuParamX = new VRCExpressionsMenu.Control.Parameter();
        menuParamX.name = (x != null) ? x.Name() : "";
        var menuParamY = new VRCExpressionsMenu.Control.Parameter();
        menuParamY.name = (y != null) ? y.Name() : "";
        control.subParameters = new[]{menuParamX, menuParamY};
    }

    public VFABool NewTrigger(string name, bool usePrefix = true) {
        if (usePrefix) name = newParamName(name);
        return GetController().NewTrigger(name);
    }
    public VFABool NewBool(string name, bool synced = false, bool def = false, bool saved = false, bool usePrefix = true, bool defTrueInEditor = false) {
        if (usePrefix) name = newParamName(name);
        if (synced) {
            var param = new VRCExpressionParameters.Parameter();
            param.name = name;
            param.valueType = VRCExpressionParameters.ValueType.Bool;
            param.saved = saved;
            param.defaultValue = def ? 1 : 0;
            addSyncedParam(param);
        }
        return GetController().NewBool(name, def || defTrueInEditor);
    }
    public VFANumber NewInt(string name, bool synced = false, int def = 0, bool saved = false, bool usePrefix = true) {
        if (usePrefix) name = newParamName(name);
        if (synced) {
            var param = new VRCExpressionParameters.Parameter();
            param.name = name;
            param.valueType = VRCExpressionParameters.ValueType.Int;
            param.saved = saved;
            param.defaultValue = def;
            addSyncedParam(param);
        }
        return GetController().NewInt(name, def);
    }
    public VFANumber NewFloat(string name, bool synced = false, float def = 0, bool saved = false, bool usePrefix = true) {
        if (usePrefix) name = newParamName(name);
        if (synced) {
            var param = new VRCExpressionParameters.Parameter();
            param.name = name;
            param.valueType = VRCExpressionParameters.ValueType.Float;
            param.saved = saved;
            param.defaultValue = def;
            addSyncedParam(param);
        }
        return GetController().NewFloat(name, def);
    }
    private string newParamName(string name) {
        return prefix + "__" + name;
    }

    public void addSyncedParam(VRCExpressionParameters.Parameter param) {
        var exists = Array.FindIndex(syncedParams.parameters, p => p.name == param.name) >= 0;
        if (exists) return;
        var syncedParamsList = new List<VRCExpressionParameters.Parameter>(syncedParams.parameters);
        syncedParamsList.Add(param);
        syncedParams.parameters = syncedParamsList.ToArray();
        EditorUtility.SetDirty(syncedParams);
    }
}

}
