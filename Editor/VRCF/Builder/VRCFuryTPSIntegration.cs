using System;
using UnityEngine;
using UnityEditor;
using UnityEditor.Animations;
using System.Collections.Generic;
using System.Reflection;
using System.Text.RegularExpressions;

namespace VRCF.Builder {

public class VRCFuryTPSIntegration {
    private static BindingFlags b = BindingFlags.NonPublic|BindingFlags.Public|BindingFlags.Instance|BindingFlags.Static;

    public static void Run(GameObject avatar, AnimatorController animator, string tmpDir) {
        var tpsSetup = Type.GetType("Thry.TPS.TPS_Setup");
        if (tpsSetup == null) return;

        RevertTPSMats(avatar);
        ApplyTPS(avatar, animator, tmpDir);

        Debug.Log("TPS Done");
    }

    private static Regex isMaterial = new Regex("^m_Materials\\.Array\\.data\\[\\d+\\]$");

    private static void RevertTPSMats(GameObject avatar) {
        Debug.Log("Reverting TPS Materials ...");
        foreach (var skin in avatar.GetComponentsInChildren<SkinnedMeshRenderer>(true)) {
            var mods = PrefabUtility.GetPropertyModifications(skin);
            if (mods == null) continue;
            foreach (var mod in PrefabUtility.GetPropertyModifications(skin)) {
                if (!isMaterial.IsMatch(mod.propertyPath)) continue;
                if (mod.objectReference != null && !mod.objectReference.name.StartsWith("Pen")) continue;
                // For some reason, these don't match using equality? Perhaps one is inside the prefab and one is outside? Check name instead.
                if (mod.target.name != skin.name) continue;

                Debug.Log("Reverting TPS material on " + mod.target.name);
                var sObj = new SerializedObject(skin);
                var sProp = sObj.FindProperty(mod.propertyPath);
                PrefabUtility.RevertPropertyOverride(sProp, InteractionMode.AutomatedAction);
            }
        }
    }

    private static void ApplyTPS(GameObject avatar, AnimatorController animator, string tmpDir) {
        Debug.Log("Invoking TPS ...");

        var tpsSetup = Type.GetType("Thry.TPS.TPS_Setup");
        var setup = ScriptableObject.CreateInstance("Thry.TPS.TPS_Setup");
        tpsSetup.GetField("_avatar", b).SetValue(setup, avatar.transform);
        tpsSetup.GetField("_animator", b).SetValue(setup, animator);
        tpsSetup.GetMethod("ScanForTPS", b).Invoke(setup, new object[]{});
        tpsSetup.GetMethod("RemoveTPSFromAnimator", b).Invoke(setup, new object[]{});
        System.Collections.IList penetrators = (System.Collections.IList)tpsSetup.GetField("_penetrators", b).GetValue(setup);
        System.Collections.IList orifices = (System.Collections.IList)tpsSetup.GetField("_orifices", b).GetValue(setup);

        Debug.Log("" + penetrators.Count + " Penetrators + " + orifices.Count + " Orifices");

        for (int i = 0; i < penetrators.Count; i++)
        {
            callWithOptionalParams(tpsSetup.GetMethod("SetupPenetrator", b), null, avatar.transform, animator, penetrators[i], penetrators, i, tmpDir);
        }
        for (int i = 0; i < orifices.Count; i++)
        {
            var o = orifices[i];
            var otype = o.GetType();
            otype.GetMethod("ConfigureLights", b).Invoke(o, new object[]{});
            var Transform = otype.GetField("Transform", b).GetValue(o);
            var Renderer = otype.GetField("Renderer", b).GetValue(o);
            var OrificeType = otype.GetField("OrificeType", b).GetValue(o);
            callWithOptionalParams(tpsSetup.GetMethod("SetupOrifice", b), null, avatar.transform, animator, Transform, Renderer, OrificeType, o, i, tmpDir);
        }
    }

    private static void callWithOptionalParams(MethodInfo method, object obj, params object[] prms) {
        var list = new List<object>(prms);
        var paramCount = method.GetParameters().Length;
        while (list.Count < paramCount) {
            list.Add(Type.Missing);
        }
        method.Invoke(obj, list.ToArray());
    }
}

}
