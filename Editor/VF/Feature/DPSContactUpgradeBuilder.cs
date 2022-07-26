using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEditor.UIElements;
using UnityEngine;
using UnityEngine.UIElements;
using VF.Feature.Base;
using VF.Inspector;
using VF.Model.Feature;
using VRC.SDK3.Dynamics.Contact.Components;

namespace VF.Feature {
    public class DPSContactUpgradeBuilder : FeatureBuilder<DPSContactUpgrade> {
        private HashSet<string> useOscOn = null;

        // priority 1 so that it runs after TPS integration, if the user is using that instead,
        // since TPS will handle orifaces for us.
        [FeatureBuilderAction(1)]
        public void Apply() {
            var usingOsc = new HashSet<string>();
            
            var penI = 0;
            foreach (var pair in getPenetrators()) {
                if (pair.Item2) {
                    var path = AnimationUtility.CalculateTransformPath(pair.Item1.transform, avatarObject.transform);
                    usingOsc.Add(path);
                    string paramFloatRootRoot = "TPS_Internal/Pen/" + penI + "/RootRoot";
                    string paramFloatRootFrwd = "TPS_Internal/Pen/" + penI + "/RootForw";
                    string paramFloatBackRoot = "TPS_Internal/Pen/" + penI + "/BackRoot";
                    controller.NewFloat(paramFloatRootRoot, usePrefix: false);
                    controller.NewFloat(paramFloatRootFrwd, usePrefix: false);
                    controller.NewFloat(paramFloatBackRoot, usePrefix: false);
                    penI++;
                }
            }

            var orfI = 0;
            foreach (var pair in getOrifaceLights()) {
                if (pair.Item3) {
                    var path = AnimationUtility.CalculateTransformPath(pair.Item1.transform, avatarObject.transform);
                    usingOsc.Add(path);
                    controller.NewFloat("TPS_Internal/Orf/" + orfI + "/Depth_In", usePrefix: false);
                    orfI++;
                }
            }

            useOscOn = usingOsc;
        }

        [FeatureBuilderAction(1, applyToVrcClone:true)]
        public void ApplyOnClone() {
            var oscDepth = model.oscDepth > 0 ? model.oscDepth : 0.25f;

            var penI = 0;
            foreach (var pair in getPenetrators()) {
                var skin = pair.Item1;
                var useOsc = pair.Item2;

                var mesh = skin.sharedMesh;
                var forward = new Vector3(0, 0, 1);
                var length = mesh.vertices.Select(v => Vector3.Dot(v, forward)).Max();
                float width = mesh.vertices.Select(v => new Vector2(v.x, v.y).magnitude).Average() * 2;

                AddSender(skin.gameObject, Vector3.zero, length, "TPS_Pen_Penetrating");
                AddSender(skin.gameObject, Vector3.zero, Mathf.Max(0.01f, length - width), "TPS_Pen_Width");

                if (useOsc) {
                    string paramFloatRootRoot = "TPS_Internal/Pen/" + penI + "/RootRoot";
                    string paramFloatRootFrwd = "TPS_Internal/Pen/" + penI + "/RootForw";
                    string paramFloatBackRoot = "TPS_Internal/Pen/" + penI + "/BackRoot";
                    penI++;
                    AddReceiver(skin.gameObject, Vector3.zero, paramFloatRootRoot, length, "TPS_Orf_Root");
                    AddReceiver(skin.gameObject, Vector3.zero, paramFloatRootFrwd, length, "TPS_Orf_Norm");
                    AddReceiver(skin.gameObject, Vector3.back * 0.01f, paramFloatBackRoot, length, "TPS_Orf_Root");
                }
            }

            var orfI = 0;
            foreach (var pair in getOrifaceLights()) {
                var light = pair.Item1;
                var normal = pair.Item2;
                var useOsc = pair.Item3;
                var forward = (normal.transform.localPosition - light.transform.localPosition).normalized;

                AddSender(light.gameObject, Vector3.zero, 0.01f, "TPS_Orf_Root");
                AddSender(light.gameObject, forward * 0.01f, 0.01f, "TPS_Orf_Norm");
                if (useOsc) {
                    AddReceiver(light.gameObject, forward * -oscDepth, "TPS_Internal/Orf/" + (orfI++) + "/Depth_In", oscDepth, "TPS_Pen_Penetrating");
                }
            }
        }
        
        private void AddSender(GameObject obj, Vector3 pos, float radius, string tag) {
            var normSender = obj.AddComponent<VRCContactSender>();
            normSender.position = pos;
            normSender.radius = radius;
            normSender.collisionTags = new List<string> { tag };
        }

        private void AddReceiver(GameObject obj, Vector3 pos, String param, float radius, string tag) {
            var receiver = obj.AddComponent<VRCContactReceiver>();
            receiver.position = pos;
            receiver.parameter = param;
            receiver.radius = radius;
            receiver.receiverType = VRC.Dynamics.ContactReceiver.ReceiverType.Proximity;
            receiver.collisionTags = new List<string> { tag };
        }
        
        private List<Tuple<SkinnedMeshRenderer,bool>> getPenetrators() {
            var pairs = new List<Tuple<SkinnedMeshRenderer,bool>>();
            foreach (var skin in avatarObject.GetComponentsInChildren<SkinnedMeshRenderer>(true)) {
                if (skin.GetComponent<VRCContactReceiver>() != null || skin.GetComponent<VRCContactSender>() != null) {
                    continue;
                }
                var usesDps = skin.sharedMaterials.Any(mat => mat != null && mat.shader && mat.shader.name == "Raliv/Penetrator");
                if (!usesDps) continue;
                var osc = useOsc(skin.gameObject);
                Debug.Log("Found DPS penetrator: " + skin + " (osc " + osc + ")");
                pairs.Add(Tuple.Create(skin, osc));
            }

            return pairs;
        }

        private List<Tuple<Light,Light,bool>> getOrifaceLights() {
            var pairs = new List<Tuple<Light,Light,bool>>();
            foreach (var light in avatarObject.GetComponentsInChildren<Light>(true)) {
                if (light.transform.parent.GetComponentsInChildren<VRCContactReceiver>(true).Length > 0
                    || light.transform.parent.GetComponentsInChildren<VRCContactSender>(true).Length > 0) {
                    continue;
                }

                if (light.range >= 0.405f && light.range <= 0.425f) {
                    Light normal = null;
                    foreach (Transform sibling in light.gameObject.transform.parent) {
                        var siblingLight = sibling.gameObject.GetComponent<Light>();
                        if (siblingLight != null && siblingLight.range >= 0.445f && siblingLight.range <= 0.455f) {
                            normal = siblingLight;
                        }
                    }
                    if (normal == null) {
                        Debug.Log("Failed to find normal sibling light for light: " + light);
                        continue;
                    }
                    var osc = useOsc(light.gameObject);
                    Debug.Log("Found DPS oriface: " + light + " (osc " + osc + ")");
                    pairs.Add(Tuple.Create(light, normal, osc));
                }
            }

            return pairs;
        }

        private bool useOsc(GameObject obj) {
            if (!model.oscEnabled) return false;
            if (useOscOn != null) {
                var path = AnimationUtility.CalculateTransformPath(obj.transform, avatarObject.transform);
                return useOscOn.Contains(path);
            }
            foreach (var parent in model.oscObjects) {
                if (obj.transform.IsChildOf(parent.transform)) {
                    return true;
                }
            }
            return false;
        }

        public override bool AvailableOnProps() {
            return false;
        }
        
        public override string GetEditorTitle() {
            return "DPS Contact Upgrader";
        }
        
        public override VisualElement CreateEditor(SerializedProperty prop) {
            var content = new VisualElement();
            content.Add(new Label() {
                text = "This feature will automatically add TPS contacts to all DPS orifaces and penetrators.",
                style = {
                    whiteSpace = WhiteSpace.Normal
                }
            });
            var oscEnabled = prop.FindPropertyRelative("oscEnabled");
            content.Add(new PropertyField(oscEnabled, "Enable OSC Receivers"));

            content.Add(VRCFuryEditorUtils.RefreshOnChange(() => {
                var section = new VisualElement();
                if (oscEnabled.boolValue) {
                    section.Add(new Label() {
                        text = "Which objects should have OSC receivers enabled?",
                        style = {
                            whiteSpace = WhiteSpace.Normal
                        }
                    });
                    section.Add(VRCFuryEditorUtils.List(prop.FindPropertyRelative("oscObjects")));
            
                    section.Add(new Label() {
                        text = "What is the maximum OSC penetration depth in meters? (use 0 for a good default setting)",
                        style = {
                            whiteSpace = WhiteSpace.Normal
                        }
                    });
                    section.Add(VRCFuryEditorUtils.PropWithoutLabel(prop.FindPropertyRelative("oscDepth")));
                }
                return section;
            }, oscEnabled));

            return content;
        }
    }
}
