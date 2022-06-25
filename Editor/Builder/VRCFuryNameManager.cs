using System;
using UnityEngine;
using UnityEditor;
using UnityEditor.Animations;
using System.Collections.Generic;
using VRC.SDK3.Avatars.ScriptableObjects;

namespace VRCF.Builder {

public class VRCFuryNameManager {
    private string prefix;
    private VRCExpressionsMenu rootMenu;
    private VRCExpressionsMenu fxMenu;
    private VRCExpressionsMenu lastMenu;
    private int lastMenuNum;
    private VRCExpressionParameters syncedParams;
    private AnimatorController ctrl;

    public VRCFuryNameManager(string prefix, VRCExpressionsMenu menu, VRCExpressionParameters syncedParams, AnimatorController controller) {
        this.prefix = prefix;
        this.rootMenu = menu;
        this.syncedParams = syncedParams;
        this.ctrl = controller;
    }

    public void Purge() {
        _noopClip = null;
        _controller = null;
        fxMenu = null;
        lastMenu = null;
        lastMenuNum = 0;
        // Clean up assets
        foreach (var subAsset in AssetDatabase.LoadAllAssetsAtPath(AssetDatabase.GetAssetPath(ctrl))) {
            if (subAsset.name.StartsWith("Senky") || subAsset.name.StartsWith(prefix+"/")) {
                AssetDatabase.RemoveObjectFromAsset(subAsset);
            }
        }
        // Clean up layers
        for (var i = 0; i < ctrl.layers.Length; i++) {
            var layer = ctrl.layers[i];
            if (layer.name.StartsWith("Senky") || layer.name.StartsWith("["+prefix+"]")) {
                ctrl.RemoveLayer(i);
                i--;
            }
        }
        // Clean up controller params
        for (var i = 0; i < ctrl.parameters.Length; i++) {
            var param = ctrl.parameters[i];
            if (param.name.StartsWith("Senky") || param.name.StartsWith(prefix+"__")) {
                ctrl.RemoveParameter(param);
                i--;
            }
        }
        // Clean up synced params
        {
            var syncedParamsList = new List<VRCExpressionParameters.Parameter>(syncedParams.parameters);
            syncedParamsList.RemoveAll(param => param.name.StartsWith("Senky") || param.name.StartsWith(prefix+"__"));
            syncedParams.parameters = syncedParamsList.ToArray();
            EditorUtility.SetDirty(syncedParams);
        }
        // Clean up menu
        {
            for (var i = 0; i < rootMenu.controls.Count; i++) {
                var remove = false;
                if (rootMenu.controls[i].type == VRCExpressionsMenu.Control.ControlType.SubMenu
                    && rootMenu.controls[i].subMenu.name.StartsWith(prefix+"/")) {
                    remove = true;
                }
                if (rootMenu.controls[i].name == "SenkyFX" || rootMenu.controls[i].name == "VRCFury") {
                    remove = true;
                }
                if (remove) {
                    rootMenu.controls.RemoveAt(i);
                    i--;
                }
            }
            foreach (var subAsset in AssetDatabase.LoadAllAssetsAtPath(AssetDatabase.GetAssetPath(rootMenu))) {
                if (subAsset.name.StartsWith("Senky") || subAsset.name.StartsWith(prefix+"/")) {
                    AssetDatabase.RemoveObjectFromAsset(subAsset);
                }
            }
        }
    }

    private VFAController _controller = null;
    private AnimationClip _noopClip = null;
    private VFAController GetController() {
        if (_controller == null) {
            _noopClip = NewClip("noop");
            _noopClip.SetCurve("_ignored", typeof(GameObject), "m_IsActive", AnimationCurve.Constant(0,1/60f,0));
            _controller = new VFAController(ctrl, _noopClip);
        }
        return _controller;
    }

    public AnimationClip GetNoopClip() {
        GetController();
        return _noopClip;
    }

    public VFALayer NewLayer(string name) {
        return GetController().NewLayer("[" + prefix + "] " + name);
    }

    public AnimationClip NewClip(string name) {
        var clip = new AnimationClip();
        clip.name = prefix + "/" + name;
        clip.hideFlags = HideFlags.None;
        AssetDatabase.AddObjectToAsset(clip, ctrl);
        return clip;
    }
    public BlendTree NewBlendTree(string name) {
        var tree = new BlendTree();
        tree.name = prefix + "/" + name;
        tree.hideFlags = HideFlags.None;
        AssetDatabase.AddObjectToAsset(tree, ctrl);
        return tree;
    }

    public VRCExpressionsMenu GetFxMenu() {
        if (fxMenu == null) {
            if (rootMenu.controls.Count >= VRCExpressionsMenu.MAX_CONTROLS) {
                throw new Exception("Root menu can't fit any more controls!");
            }
            fxMenu = ScriptableObject.CreateInstance<VRCExpressionsMenu>();
            fxMenu.name = prefix + "/Main";
            AssetDatabase.AddObjectToAsset(fxMenu, rootMenu);
            var control = new VRCExpressionsMenu.Control();
            rootMenu.controls.Add(control);
            control.name = prefix;
            control.subMenu = fxMenu;
            control.type = VRCExpressionsMenu.Control.ControlType.SubMenu;
        }
        return fxMenu;
    }
    public VRCExpressionsMenu GetNumMenu() {
        if (lastMenu == null || lastMenu.controls.Count >= VRCExpressionsMenu.MAX_CONTROLS) {
            var fxMenu = GetFxMenu();
            if (fxMenu.controls.Count >= VRCExpressionsMenu.MAX_CONTROLS) {
                throw new Exception("Out of room for new menu pages!");
            }
            lastMenuNum++;
            lastMenu = ScriptableObject.CreateInstance<VRCExpressionsMenu>();
            lastMenu.name = prefix + "/" + lastMenuNum;
            AssetDatabase.AddObjectToAsset(lastMenu, rootMenu);
            var control = new VRCExpressionsMenu.Control();
            fxMenu.controls.Add(control);
            control.name = ""+lastMenuNum;
            control.subMenu = lastMenu;
            control.type = VRCExpressionsMenu.Control.ControlType.SubMenu;
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
        control.subParameters = new VRCExpressionsMenu.Control.Parameter[]{menuParam};
    }
    public void NewMenuPuppet(string name, VFANumber x, VFANumber y) {
        var control = NewMenuItem();
        control.name = name;
        control.type = VRCExpressionsMenu.Control.ControlType.TwoAxisPuppet;
        var menuParamX = new VRCExpressionsMenu.Control.Parameter();
        menuParamX.name = (x != null) ? x.Name() : "";
        var menuParamY = new VRCExpressionsMenu.Control.Parameter();
        menuParamY.name = (y != null) ? y.Name() : "";
        control.subParameters = new VRCExpressionsMenu.Control.Parameter[]{menuParamX, menuParamY};
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

    private void addSyncedParam(VRCExpressionParameters.Parameter param) {
        var syncedParamsList = new List<VRCExpressionParameters.Parameter>(syncedParams.parameters);
        syncedParamsList.Add(param);
        syncedParams.parameters = syncedParamsList.ToArray();
        EditorUtility.SetDirty(syncedParams);
    }
}

}
