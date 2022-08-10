using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;
using UnityEngine.UIElements;
using VF.Builder;
using VF.Feature.Base;
using VF.Inspector;
using VF.Menu;
using VF.Model.Feature;

namespace VF.Feature {
    public class TPSIntegrationBuilder : FeatureBuilder<TPSIntegration> {
        private static readonly BindingFlags b = BindingFlags.NonPublic|BindingFlags.Public|BindingFlags.Instance|BindingFlags.Static;
        private int matCounter = 0;

        [FeatureBuilderAction]
        public void Apply() {
            addOtherFeature(new OGBIntegration());

            var tpsSetup = ReflectionUtils.GetTypeFromAnyAssembly("Thry.TPS.TPS_Setup");
            if (tpsSetup == null) {
                throw new Exception("TPS Integration Feature cannot run, because Poiyomi TPS is not installed!");
            }

            foreach (var skin in avatarObject.GetComponentsInChildren<SkinnedMeshRenderer>(true)) {
                var mats = skin.sharedMaterials;
                for (var i = 0; i < mats.Length; i++) {
                    ManageMaterial(skin.gameObject, skin, mats[i], m => {
                        mats[i] = m;
                        skin.sharedMaterials = mats;
                    });
                }
            }
            foreach (var mesh in avatarObject.GetComponentsInChildren<MeshRenderer>(true)) {
                var mats = mesh.sharedMaterials;
                for (var i = 0; i < mats.Length; i++) {
                    ManageMaterial(mesh.gameObject, null, mats[i], m => {
                        mats[i] = m;
                        mesh.sharedMaterials = mats;
                    });
                }
            }

            var tpsClipDir = tmpDir;
            Directory.CreateDirectory(tpsClipDir);
            var tpsAnimator = AnimatorController.CreateAnimatorControllerAtPath(tpsClipDir + "/tmp.controller");

            var setup = ScriptableObject.CreateInstance(tpsSetup);
            tpsSetup.GetField("_avatar", b).SetValue(setup, avatarObject.transform);
            tpsSetup.GetField("_animator", b).SetValue(setup, tpsAnimator);
            tpsSetup.GetMethod("ScanForTPS", b).Invoke(setup, new object[]{});
            tpsSetup.GetMethod("RemoveTPSFromAnimator", b).Invoke(setup, new object[]{});
            var penetrators = (IList)tpsSetup.GetField("_penetrators", b).GetValue(setup);
            var orifices = (IList)tpsSetup.GetField("_orifices", b).GetValue(setup);

            Debug.Log("" + penetrators.Count + " Penetrators + " + orifices.Count + " Orifices");

            for (var i = 0; i < penetrators.Count; i++) {
                callWithOptionalParams(tpsSetup.GetMethod("SetupPenetrator", b), null, 
                    avatarObject.transform,
                    tpsAnimator,
                    penetrators[i],
                    penetrators,
                    i,
                    tpsClipDir,
                    true, // place contacts
                    false, // copy materials
                    true // configure materials
                );
            }
            for (var i = 0; i < orifices.Count; i++) {
                var o = orifices[i];
                var otype = o.GetType();
                otype.GetMethod("ConfigureLights", b).Invoke(o, new object[]{});
                var Transform = otype.GetField("Transform", b).GetValue(o);
                var Renderer = otype.GetField("Renderer", b).GetValue(o);
                var OrificeType = otype.GetField("OrificeType", b).GetValue(o);
                otype.GetField("BlendShapeIndexEnter", b).SetValue(o, 0);
                otype.GetField("BlendShapeIndexIn", b).SetValue(o, 0);
                otype.GetField("MaxDepth", b).SetValue(o, 0.25f); // Max penetration depth meters
                callWithOptionalParams(tpsSetup.GetMethod("SetupOrifice", b), null,
                    avatarObject.transform,
                    tpsAnimator,
                    Transform,
                    Renderer,
                    OrificeType,
                    o,
                    i,
                    tpsClipDir
                );
            }

            var merger = new ControllerMerger(rewriteLayerName: layerName => controller.NewLayerName(layerName));
            merger.Merge(tpsAnimator, controller.GetRawController());
        }

        private void ManageMaterial(GameObject obj, SkinnedMeshRenderer skin, Material mat, Action<Material> update) {
            var isTps = mat.HasProperty("_TPSPenetratorEnabled") && mat.GetFloat("_TPSPenetratorEnabled") > 0;
            if (!isTps) return;
            if (AssetDatabase.GetAssetPath(mat).Contains("/TPS_")) {
                throw new VRCFBuilderException(
                    "TPS Integration failed because the material on a penetrator has already been locked using the manual TPS setup wizard." +
                    " Revert the material on this object back to its default Poiyomi file instead of the copy created by the wizard:\n\n" +
                    obj.transform.GetHierarchyPath());
            }
            if (mat.shader.name.Contains("Locked")) {
                throw new VRCFBuilderException(
                    "TPS Integration failed because the material on a penetrator has already been locked by poiyomi." +
                    " Please go to the material on this object and unlock it:\n\n" +
                    obj.transform.GetHierarchyPath());
            }
            var copy = new Material(mat);
            AssetDatabase.CreateAsset(copy, tmpDir + "/VRCFTPS_" + (matCounter++) + ".mat");

            if (skin != null) {
                var bakeUtil = ReflectionUtils.GetTypeFromAnyAssembly("Thry.TPS.BakeToVertexColors");
                var bakeMethod = bakeUtil.GetMethod("BakePositionsToTexture", new[] { typeof(Renderer), typeof(Texture2D) });
                Texture2D tex = (Texture2D)callWithOptionalParams(bakeMethod, null, skin, null);
                copy.SetTexture("_TPS_BakedMesh", tex);
                copy.SetFloat("_TPS_IsSkinnedMeshRenderer", 1);
                copy.EnableKeyword("TPS_IsSkinnedMesh");
            } else {
                copy.SetFloat("_TPS_IsSkinnedMeshRenderer", 0);
                copy.DisableKeyword("TPS_IsSkinnedMesh");
            }

            update(copy);
        }

        public override string GetEditorTitle() {
            return "TPS Integration";
        }
        
        public override VisualElement CreateEditor(SerializedProperty prop) {
            return VRCFuryEditorUtils.WrappedLabel("This feature will automatically run Poiyomi TPS setup on your avatar.");
        }

        public override bool AvailableOnProps() {
            return false;
        }

        private static object callWithOptionalParams(MethodInfo method, object obj, params object[] prms) {
            var list = new List<object>(prms);
            var paramCount = method.GetParameters().Length;
            while (list.Count < paramCount) {
                list.Add(Type.Missing);
            }
            return method.Invoke(obj, list.ToArray());
        }
    }
}