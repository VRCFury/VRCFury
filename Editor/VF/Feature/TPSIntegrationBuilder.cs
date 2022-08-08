using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;
using VF.Builder;
using VF.Feature.Base;
using VF.Menu;
using VF.Model.Feature;

namespace VF.Feature {
    public class TPSIntegrationBuilder : FeatureBuilder<TPSIntegration> {
        private static readonly BindingFlags b = BindingFlags.NonPublic|BindingFlags.Public|BindingFlags.Instance|BindingFlags.Static;

        [FeatureBuilderAction]
        public void Apply() {
            addOtherFeature(new OGBIntegration());

            var tpsSetup = ReflectionUtils.GetTypeFromAnyAssembly("Thry.TPS.TPS_Setup");
            if (tpsSetup == null) {
                Debug.LogError("TPS is not installed!");
                return;
            }
            
            Debug.Log("Running TPS on " + avatarObject + " ...");

            var badMaterials = new List<string>();
            foreach (var skin in avatarObject.GetComponentsInChildren<SkinnedMeshRenderer>(true)) {
                foreach (var mat in skin.sharedMaterials) {
                    if (AssetDatabase.GetAssetPath(mat).Contains("/TPS_")) {
                        badMaterials.Add(skin.gameObject.transform.GetHierarchyPath());
                    }
                }
            }
            foreach (var mesh in avatarObject.GetComponentsInChildren<MeshRenderer>(true)) {
                foreach (var mat in mesh.sharedMaterials) {
                    if (AssetDatabase.GetAssetPath(mat).Contains("/TPS_")) {
                        badMaterials.Add(mesh.gameObject.transform.GetHierarchyPath());
                    }
                }
            }
            if (badMaterials.Count > 0) {
                throw new VRCFBuilderException(
                    "TPSIntegration failed because some materials have already been locked using the manual TPS setup wizard." +
                    " Revert the materials on these objects back to their default Poiyomi file instead of the copy created by the wizard:\n\n" +
                    string.Join("\n", badMaterials));
            }

            var tpsClipDir = tmpDir;
            Directory.CreateDirectory(tpsClipDir);
            AnimatorController tpsAnimator = controller.GetRawController();

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
                    true, // copy materials
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
        }

        public override string GetEditorTitle() {
            return "TPS Integration";
        }

        public override bool AvailableOnProps() {
            return false;
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