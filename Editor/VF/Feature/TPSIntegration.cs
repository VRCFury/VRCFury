using System;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using UnityEditor.Animations;
using UnityEngine;
using VF.Builder;

namespace VF.Feature {
    public class TPSIntegration : BaseFeature<Model.Feature.TPSIntegration> {
        private static readonly BindingFlags b = BindingFlags.NonPublic|BindingFlags.Public|BindingFlags.Instance|BindingFlags.Static;

        public override void Generate(Model.Feature.TPSIntegration model) {
            var tpsSetup = ReflectionUtils.GetTypeFromAnyAssembly("Thry.TPS.TPS_Setup");
            if (tpsSetup == null) {
                Debug.LogError("TPS is not installed!");
                return;
            }
            
            Debug.Log("Running TPS on " + avatarObject + " ...");

            var animator = manager.GetRawController();
            if (operatingOnVrcClone) {
                // If we're working on the clone, just throw away all of TPS's animator changes
                animator = new AnimatorController();
            }

            var setup = ScriptableObject.CreateInstance(tpsSetup);
            tpsSetup.GetField("_avatar", b).SetValue(setup, avatarObject.transform);
            tpsSetup.GetField("_animator", b).SetValue(setup, animator);
            tpsSetup.GetMethod("ScanForTPS", b).Invoke(setup, new object[]{});
            tpsSetup.GetMethod("RemoveTPSFromAnimator", b).Invoke(setup, new object[]{});
            var penetrators = (IList)tpsSetup.GetField("_penetrators", b).GetValue(setup);
            var orifices = (IList)tpsSetup.GetField("_orifices", b).GetValue(setup);

            Debug.Log("" + penetrators.Count + " Penetrators + " + orifices.Count + " Orifices");

            for (var i = 0; i < penetrators.Count; i++) {
                callWithOptionalParams(tpsSetup.GetMethod("SetupPenetrator", b), null, 
                    avatarObject.transform,
                    animator,
                    penetrators[i],
                    penetrators,
                    i,
                    manager.GetTmpDir(),
                    true, // place contacts
                    false, // copy materials
                    !operatingOnVrcClone // configure materials
                );
            }
            for (var i = 0; i < orifices.Count; i++) {
                var o = orifices[i];
                var otype = o.GetType();
                otype.GetMethod("ConfigureLights", b).Invoke(o, new object[]{});
                var Transform = otype.GetField("Transform", b).GetValue(o);
                var Renderer = otype.GetField("Renderer", b).GetValue(o);
                var OrificeType = otype.GetField("OrificeType", b).GetValue(o);
                callWithOptionalParams(tpsSetup.GetMethod("SetupOrifice", b), null,
                    avatarObject.transform,
                    animator,
                    Transform,
                    Renderer,
                    OrificeType,
                    o,
                    i,
                    manager.GetTmpDir()
                );
            }
        }

        public override string GetEditorTitle() {
            return "TPS Integration";
        }

        public override bool AvailableOnProps() {
            return false;
        }

        public override bool ApplyToVrcClone() {
            return true;
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