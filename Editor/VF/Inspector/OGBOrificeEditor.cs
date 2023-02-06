using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEditor.UIElements;
using UnityEngine;
using UnityEngine.Animations;
using UnityEngine.UIElements;
using VF.Builder;
using VF.Menu;
using VF.Model;
using VRC.Dynamics;
using VRC.SDK3.Avatars.Components;

namespace VF.Inspector {
    [CustomEditor(typeof(OGBOrifice), true)]
    public class OGBOrificeEditor : Editor {
        public override VisualElement CreateInspectorGUI() {
            var self = (OGBOrifice)target;

            var container = new VisualElement();
            
            container.Add(VRCFuryEditorUtils.Prop(serializedObject.FindProperty("name"), "Name Override"));
            container.Add(VRCFuryEditorUtils.Prop(serializedObject.FindProperty("addLight"), "Add DPS Light"));
            container.Add(VRCFuryEditorUtils.Prop(serializedObject.FindProperty("addMenuItem"), "Add Toggle to Menu?"));
            container.Add(VRCFuryEditorUtils.WrappedLabel("Enable hand touch zone? (Auto will add only if child of Hips)"));
            container.Add(VRCFuryEditorUtils.Prop(serializedObject.FindProperty("enableHandTouchZone2")));
            container.Add(VRCFuryEditorUtils.WrappedLabel("Hand touch zone depth override in meters:\nNote, this zone is only used for hand touches, not penetration"));
            container.Add(VRCFuryEditorUtils.Prop(serializedObject.FindProperty("length")));

            container.Add(VRCFuryEditorUtils.WrappedLabel("Animations when penetrated:"));
            container.Add(VRCFuryEditorUtils.List(serializedObject.FindProperty("depthActions"), (i, prop) => {
                var c = new VisualElement();
                c.Add(VRCFuryEditorUtils.Info(
                    "If you provide an animation clip with more than 2 frames, the clip will run from start " +
                    "to end depending on penetration depth. Otherwise, it will animate from 'off' to 'on' depending on depth."));
                c.Add(VRCFuryEditorUtils.WrappedLabel("Penetrated state:"));
                c.Add(VRCFuryEditorUtils.Prop(prop.FindPropertyRelative("state")));
                c.Add(VRCFuryEditorUtils.WrappedLabel("Depth of minimum penetration in meters (can be slightly negative to trigger outside!):"));
                c.Add(VRCFuryEditorUtils.Prop(prop.FindPropertyRelative("minDepth")));
                c.Add(VRCFuryEditorUtils.WrappedLabel("Depth of maximum penetration in meters (0 for default):"));
                c.Add(VRCFuryEditorUtils.Prop(prop.FindPropertyRelative("maxDepth")));
                c.Add(VRCFuryEditorUtils.WrappedLabel("Enable animation for penetrators on this same avatar?"));
                c.Add(VRCFuryEditorUtils.Prop(prop.FindPropertyRelative("enableSelf")));
                return c;
            }));

            return container;
        }
        
        [DrawGizmo(GizmoType.Selected | GizmoType.Active | GizmoType.InSelectionHierarchy)]
        static void DrawGizmo(OGBOrifice scr, GizmoType gizmoType) {
            var autoInfo = GetInfoFromLights(scr.gameObject);
            var forward = Vector3.forward;
            if (autoInfo != null) {
                forward = autoInfo.Item1;
            }

            // This is *90 because capsule length is actually "height", so we have to rotate it to make it a length
            var tightRot = Quaternion.LookRotation(forward) * Quaternion.Euler(90,0,0);

            var lightType = scr.addLight;
            if (lightType == OGBOrifice.AddLight.Auto)
                lightType = ShouldProbablyBeHole(scr) ? OGBOrifice.AddLight.Hole : OGBOrifice.AddLight.Ring;
            if (lightType == OGBOrifice.AddLight.None && autoInfo != null)
                lightType = autoInfo.Item2 ? OGBOrifice.AddLight.Ring : OGBOrifice.AddLight.Hole;

            var text = "Orifice Missing Light";
            if (lightType == OGBOrifice.AddLight.Hole) text = "Hole";
            if (lightType == OGBOrifice.AddLight.Ring) text = "Ring";

            var handTouchZoneSize = GetHandTouchZoneSize(scr);
            if (handTouchZoneSize != null) {
                var length = handTouchZoneSize.Item1;
                var radius = handTouchZoneSize.Item2;
                OGBPenetratorEditor.DrawCapsule(
                    scr.gameObject,
                    forward * -(length / 2),
                    tightRot,
                    length,
                    radius
                );
                VRCFuryGizmoUtils.DrawText(
                    scr.transform.TransformPoint(forward * -(length / 2) / scr.transform.lossyScale.x),
                    "Hand Touch Zone\n(should be INSIDE)",
                    Color.red
                );
            }

            VRCFuryGizmoUtils.DrawSphere(
                scr.transform.position,
                0.03f,
                Color.green
            );
            VRCFuryGizmoUtils.DrawArrow(
                scr.transform.position,
                scr.transform.TransformPoint(forward * -0.1f / scr.transform.lossyScale.x),
                Color.green
            );
            VRCFuryGizmoUtils.DrawText(
                scr.transform.position,
                text + "\n(Arrow points INWARD)",
                Color.green
            );
        }
        
        public static Tuple<string,Vector3> Bake(OGBOrifice orifice, List<string> usedNames = null, bool onlySenders = false) {
            if (usedNames == null) usedNames = new List<string>();
            var obj = orifice.gameObject;
            OGBUtils.RemoveTPSSenders(obj);
            
            OGBUtils.AssertValidScale(obj, "orifice");

            var autoInfo = GetInfoFromLights(obj);

            var forward = Vector3.forward;
            if (autoInfo != null) {
                forward = autoInfo.Item1;
            }

            var name = orifice.name;
            if (string.IsNullOrWhiteSpace(name)) {
                name = obj.name;
            }

            Debug.Log("Baking OGB " + obj + " as " + name);

            // Senders
            OGBUtils.AddSender(obj, Vector3.zero, "Root", 0.01f, OGBUtils.CONTACT_ORF_MAIN);
            OGBUtils.AddSender(obj, forward * 0.01f, "Front", 0.01f, OGBUtils.CONTACT_ORF_NORM);
            
            var paramPrefix = OGBUtils.GetNextName(usedNames, "OGB/Orf/" + name.Replace('/','_'));

            if (onlySenders) {
                var bake = new GameObject("OGB_Baked_Orf");
                bake.transform.SetParent(obj.transform, false);
                if (!string.IsNullOrWhiteSpace(orifice.name)) {
                    var nameObj = new GameObject("name=" + orifice.name);
                    nameObj.transform.SetParent(bake.transform, false);
                }
            } else {
                // Receivers
                var handTouchZoneSize = GetHandTouchZoneSize(orifice);
                if (handTouchZoneSize != null) {
                    var oscDepth = handTouchZoneSize.Item1;
                    var closeRadius = handTouchZoneSize.Item2;
                    // This is *90 because capsule length is actually "height", so we have to rotate it to make it a length
                    var tightRot = Quaternion.LookRotation(forward) * Quaternion.Euler(90,0,0);
                    OGBUtils.AddReceiver(obj, forward * -oscDepth, paramPrefix + "/TouchSelf", "TouchSelf", oscDepth, OGBUtils.SelfContacts, allowOthers:false, localOnly:true);
                    OGBUtils.AddReceiver(obj, forward * -(oscDepth/2), paramPrefix + "/TouchSelfClose", "TouchSelfClose", closeRadius, OGBUtils.SelfContacts, allowOthers:false, localOnly:true, height: oscDepth, rotation: tightRot, type: ContactReceiver.ReceiverType.Constant);
                    OGBUtils.AddReceiver(obj, forward * -oscDepth, paramPrefix + "/TouchOthers", "TouchOthers", oscDepth, OGBUtils.BodyContacts, allowSelf:false, localOnly:true);
                    OGBUtils.AddReceiver(obj, forward * -(oscDepth/2), paramPrefix + "/TouchOthersClose", "TouchOthersClose", closeRadius, OGBUtils.BodyContacts, allowSelf:false, localOnly:true, height: oscDepth, rotation: tightRot, type: ContactReceiver.ReceiverType.Constant);
                    // Legacy non-OGB TPS penetrator detection
                    OGBUtils.AddReceiver(obj, forward * -oscDepth, paramPrefix + "/PenOthers", "PenOthers", oscDepth, new []{OGBUtils.CONTACT_PEN_MAIN}, allowSelf:false, localOnly:true);
                    OGBUtils.AddReceiver(obj, forward * -(oscDepth/2), paramPrefix + "/PenOthersClose", "PenOthersClose", closeRadius, new []{OGBUtils.CONTACT_PEN_MAIN}, allowSelf:false, localOnly:true, height: oscDepth, rotation: tightRot, type: ContactReceiver.ReceiverType.Constant);
                    
                    var frotRadius = 0.1f;
                    var frotPos = 0.05f;
                    OGBUtils.AddReceiver(obj, forward * frotPos, paramPrefix + "/FrotOthers", "FrotOthers", frotRadius, new []{OGBUtils.CONTACT_ORF_MAIN}, allowSelf:false, localOnly:true);
                }
                
                OGBUtils.AddReceiver(obj, Vector3.zero, paramPrefix + "/PenSelfNewRoot", "PenSelfNewRoot", 1f, new []{OGBUtils.CONTACT_PEN_ROOT}, allowOthers:false, localOnly:true);
                OGBUtils.AddReceiver(obj, Vector3.zero, paramPrefix + "/PenSelfNewTip", "PenSelfNewTip", 1f, new []{OGBUtils.CONTACT_PEN_MAIN}, allowOthers:false, localOnly:true);
                OGBUtils.AddReceiver(obj, Vector3.zero, paramPrefix + "/PenOthersNewRoot", "PenOthersNewRoot", 1f, new []{OGBUtils.CONTACT_PEN_ROOT}, allowSelf:false, localOnly:true);
                OGBUtils.AddReceiver(obj, Vector3.zero, paramPrefix + "/PenOthersNewTip", "PenOthersNewTip", 1f, new []{OGBUtils.CONTACT_PEN_MAIN}, allowSelf:false, localOnly:true);
            }
            
            OGBUtils.AddVersionContacts(obj, paramPrefix, onlySenders, false);

            var addLight = orifice.addLight;
            if (addLight == OGBOrifice.AddLight.Auto) {
                addLight = ShouldProbablyBeHole(orifice) ? OGBOrifice.AddLight.Hole : OGBOrifice.AddLight.Ring;
            }
            if (autoInfo == null && addLight != OGBOrifice.AddLight.None) {
                foreach (var light in obj.GetComponentsInChildren<Light>(true)) {
                    AvatarCleaner.RemoveComponent(light);
                }

#if !UNITY_ANDROID
                var main = new GameObject("Root");
                main.transform.SetParent(obj.transform, false);
                var mainLight = main.AddComponent<Light>();
                mainLight.type = LightType.Point;
                mainLight.color = Color.black;
                mainLight.range = addLight == OGBOrifice.AddLight.Ring ? 0.42f : 0.41f;
                mainLight.shadows = LightShadows.None;
                mainLight.renderMode = LightRenderMode.ForceVertex;

                var front = new GameObject("Front");
                front.transform.SetParent(obj.transform, false);
                var frontLight = front.AddComponent<Light>();
                front.transform.localPosition = new Vector3(0, 0, 0.01f / obj.transform.lossyScale.x);
                frontLight.type = LightType.Point;
                frontLight.color = Color.black;
                frontLight.range = 0.45f;
                frontLight.shadows = LightShadows.None;
                frontLight.renderMode = LightRenderMode.ForceVertex;
#endif
            }

            return Tuple.Create(name, forward);
        }

        private static Tuple<float, float> GetHandTouchZoneSize(OGBOrifice orifice) {
            bool enableHandTouchZone = false;
            if (orifice.enableHandTouchZone2 == OGBOrifice.EnableTouchZone.On) {
                enableHandTouchZone = true;
            } else if (orifice.enableHandTouchZone2 == OGBOrifice.EnableTouchZone.Auto) {
                enableHandTouchZone = ShouldProbablyHaveTouchZone(orifice);
            }
            if (!enableHandTouchZone) {
                return null;
            }
            var length = orifice.length;
            if (length <= 0) length = 0.25f;
            var radius = length / 2.5f;
            return Tuple.Create(length, radius);
        }
        
        public static bool IsHole(Light light) {
            var rangeId = light.range % 0.1;
            return rangeId >= 0.005f && rangeId < 0.015f;
        }
        public static bool IsRing(Light light) {
            var rangeId = light.range % 0.1;
            return rangeId >= 0.015f && rangeId < 0.025f;
        }
        public static bool IsNormal(Light light) {
            var rangeId = light.range % 0.1;
            return rangeId >= 0.045f && rangeId <= 0.055f;
        }
        
        public static Tuple<Vector3, bool> GetInfoFromLights(GameObject obj) {
            var isRing = false;
            Light main = null;
            Light normal = null;
            foreach (Transform child in obj.transform) {
                var light = child.gameObject.GetComponent<Light>();
                if (light != null) {
                    if (main == null) {
                        if (IsHole(light)) {
                            main = light;
                        } else if (IsRing(light)) {
                            main = light;
                            isRing = true;
                        }
                    }
                    if (normal == null && IsNormal(light)) {
                        normal = light;
                    }
                }
            }

            if (main == null || normal == null) return null;

            var forward = Vector3.forward;
            if (normal != null) {
                forward = (normal.transform.localPosition - main.transform.localPosition).normalized;
            }

            return Tuple.Create(forward, isRing);
        }

        public static void ClaimLights(OGBOrifice orifice) {
            var info = GetInfoFromLights(orifice.gameObject);
            foreach (var light in orifice.gameObject.GetComponentsInChildren<Light>(true)) {
                if (IsRing(light) || IsHole(light) || IsNormal(light)) {
                    AvatarCleaner.RemoveComponent(light);
                }
            }
            if (info != null) {
                var forward = info.Item1;
                var isRing = info.Item2;
                orifice.transform.rotation *= Quaternion.LookRotation(forward);
                orifice.addLight = isRing ? OGBOrifice.AddLight.Ring : OGBOrifice.AddLight.Hole;
            }
        }

        public static bool ShouldProbablyHaveTouchZone(OGBOrifice orf) {
            var avatarObject = orf.gameObject.GetComponentInParent<VRCAvatarDescriptor>()?.gameObject;
            if (!avatarObject) return false;
            if (!IsChildOfBone(avatarObject, orf, HumanBodyBones.Hips)) return false;
            if (IsChildOfBone(avatarObject, orf, HumanBodyBones.Chest)) return false;
            if (IsChildOfBone(avatarObject, orf, HumanBodyBones.Spine)) return false;
            if (IsChildOfBone(avatarObject, orf, HumanBodyBones.LeftUpperArm)) return false;
            if (IsChildOfBone(avatarObject, orf, HumanBodyBones.LeftUpperLeg)) return false;
            if (IsChildOfBone(avatarObject, orf, HumanBodyBones.RightUpperArm)) return false;
            if (IsChildOfBone(avatarObject, orf, HumanBodyBones.RightUpperLeg)) return false;
            return true;
        }
        
        public static bool ShouldProbablyBeHole(OGBOrifice orf) {
            var avatarObject = orf.gameObject.GetComponentInParent<VRCAvatarDescriptor>()?.gameObject;
            if (!avatarObject) return false;
            if (IsChildOfBone(avatarObject, orf, HumanBodyBones.Head)) return true;
            if (!IsChildOfBone(avatarObject, orf, HumanBodyBones.Hips)) return false;
            if (IsChildOfBone(avatarObject, orf, HumanBodyBones.Chest)) return false;
            if (IsChildOfBone(avatarObject, orf, HumanBodyBones.Spine)) return false;
            if (IsChildOfBone(avatarObject, orf, HumanBodyBones.LeftUpperArm)) return false;
            if (IsChildOfBone(avatarObject, orf, HumanBodyBones.LeftUpperLeg)) return false;
            if (IsChildOfBone(avatarObject, orf, HumanBodyBones.RightUpperArm)) return false;
            if (IsChildOfBone(avatarObject, orf, HumanBodyBones.RightUpperLeg)) return false;
            return true;
        }

        private static bool IsChildOfBone(GameObject avatarObject, OGBOrifice orf, HumanBodyBones bone) {
<<<<<<< HEAD
            var boneObj = VRCFArmatureUtils.FindBoneOnArmature(avatarObject, bone);
            return boneObj && IsChildOf(boneObj.transform, orf.transform);
=======
            try {
                var boneObj = VRCFArmatureUtils.FindBoneOnArmature(avatarObject, bone);
                return boneObj && IsChildOf(boneObj.transform, orf.transform);
            } catch (Exception e) {
                return false;
            }
>>>>>>> 0772fa9975bfc14ddd8f9170dc02fa372562381b
        }

        private static bool IsChildOf(Transform parent, Transform child) {
            var alreadyChecked = new HashSet<Transform>();
            var current = child;
            while (current != null) {
                alreadyChecked.Add(current);
                if (current == parent) return true;
                var constraint = current.GetComponent<IConstraint>();
                if (constraint != null && constraint.sourceCount > 0) {
                    var source = constraint.GetSource(0).sourceTransform;
                    if (source != null && !alreadyChecked.Contains(source)) {
                        current = source;
                        continue;
                    }
                }
                current = current.parent;
            }
            return false;
        }
    }
}
