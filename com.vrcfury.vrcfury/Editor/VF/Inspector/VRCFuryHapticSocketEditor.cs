using System;
using System.Collections.Generic;
using System.Globalization;
using System.Runtime.CompilerServices;
using System.Text.RegularExpressions;
using UnityEditor;
using UnityEngine;
using UnityEngine.Animations;
using UnityEngine.UIElements;
using VF.Builder;
using VF.Builder.Haptics;
using VF.Component;
using VF.Menu;
using VRC.Dynamics;
using VRC.SDK3.Avatars.Components;
using VRC.SDK3.Dynamics.Contact.Components;

namespace VF.Inspector {
    [CustomEditor(typeof(VRCFuryHapticSocket), true)]
    public class VRCFuryHapticSocketEditor : VRCFuryComponentEditor<VRCFuryHapticSocket> {
        public override VisualElement CreateEditor(SerializedObject serializedObject, VRCFuryHapticSocket target) {
            var container = new VisualElement();
            
            container.Add(VRCFuryEditorUtils.BetterProp(serializedObject.FindProperty("name"), "Name in menu / connected apps"));
            container.Add(VRCFuryEditorUtils.BetterProp(serializedObject.FindProperty("addLight"), "Add DPS Light"));
            container.Add(VRCFuryEditorUtils.BetterProp(serializedObject.FindProperty("addMenuItem"), "Add Toggle to Menu?"));
            container.Add(VRCFuryEditorUtils.BetterProp(serializedObject.FindProperty("enableAuto"), "Include in Auto selection?"));
            container.Add(VRCFuryEditorUtils.BetterProp(
                serializedObject.FindProperty("enableHandTouchZone2"),
                "Enable hand touch zone? (Auto will add only if child of Hips)"
            ));
            container.Add(VRCFuryEditorUtils.BetterProp(
                serializedObject.FindProperty("length"),
                "Hand touch zone depth override in meters:\nNote, this zone is only used for hand touches, not penetration"
            ));
            
            container.Add(VRCFuryEditorUtils.BetterProp(
                serializedObject.FindProperty("activeActions"),
                "Additional animation when socket is active"
            ));

            container.Add(VRCFuryEditorUtils.WrappedLabel("Animations when plug is present"));
            container.Add(VRCFuryEditorUtils.List(serializedObject.FindProperty("depthActions"), (i, prop) => {
                var c = new VisualElement();
                c.Add(VRCFuryEditorUtils.Info(
                    "If you provide a non-static (moving) animation clip, the clip will run from start " +
                    "to end depending on penetration depth. Otherwise, it will animate from 'off' to 'on' depending on depth."));
                c.Add(VRCFuryEditorUtils.BetterProp(prop.FindPropertyRelative("state"), "Penetrated state"));
                c.Add(VRCFuryEditorUtils.BetterProp(prop.FindPropertyRelative("minDepth"), "Depth of minimum penetration in meters (can be slightly negative to trigger outside!)"));
                c.Add(VRCFuryEditorUtils.BetterProp(prop.FindPropertyRelative("maxDepth"), "Depth of maximum penetration in meters (0 for default)"));
                c.Add(VRCFuryEditorUtils.BetterProp(prop.FindPropertyRelative("enableSelf"), "Allow avatar to trigger its own animation?"));
                return c;
            }));
            
            var adv = new Foldout {
                text = "Advanced",
                value = false,
            };
            container.Add(adv);
            adv.Add(VRCFuryEditorUtils.BetterProp(serializedObject.FindProperty("position"), "Position"));
            adv.Add(VRCFuryEditorUtils.BetterProp(serializedObject.FindProperty("rotation"), "Rotation"));
            //adv.Add(VRCFuryEditorUtils.BetterProp(serializedObject.FindProperty("channel"), "Channel"));

            return container;
        }

        [CustomEditor(typeof(VRCFurySocketGizmo), true)]
        public class VRCFuryHapticPlaySocketEditor : Editor {
            [DrawGizmo(GizmoType.Selected | GizmoType.NonSelected | GizmoType.Pickable)]
            static void DrawGizmo2(VRCFurySocketGizmo gizmo, GizmoType gizmoType) {
                if (!gizmo.show) return;
                DrawGizmo(gizmo.transform.TransformPoint(gizmo.pos), gizmo.transform.rotation * gizmo.rot, gizmo.type, "");
            }
        }

        static void DrawGizmo(Vector3 worldPos, Quaternion worldRot, VRCFuryHapticSocket.AddLight type, string name) {
            var text = "Socket";
            if (!string.IsNullOrWhiteSpace(name)) text += $" '{name}'";
            if (type == VRCFuryHapticSocket.AddLight.Hole) text += " (Hole)\nPlug follows orange arrow";
            else if (type == VRCFuryHapticSocket.AddLight.Ring) text += " (Ring)\nPlug follows orange arrow";
            else text += " (SPS disabled)";

            var orange = new Color(1f, 0.5f, 0);

            var worldForward = worldRot * Vector3.forward;
            VRCFuryGizmoUtils.WithHandles(() => {
                Handles.color = orange;
                Handles.DrawWireDisc(worldPos, worldForward, 0.02f);
            });
            VRCFuryGizmoUtils.WithHandles(() => {
                Handles.color = orange;
                Handles.DrawWireDisc(worldPos, worldForward, 0.04f);
            });
            if (type == VRCFuryHapticSocket.AddLight.Ring) {
                VRCFuryGizmoUtils.DrawArrow(
                    worldPos + worldForward * 0.05f,
                    worldPos + worldForward * -0.05f,
                    orange
                );
            } else {
                VRCFuryGizmoUtils.DrawArrow(
                    worldPos + worldForward * 0.1f,
                    worldPos,
                    orange
                );
            }
            VRCFuryGizmoUtils.DrawText(
                worldPos,
                "\n" + text,
                Color.gray,
                true,
                true
            );

            // So that it's actually clickable
            Gizmos.color = Color.clear;
            Gizmos.DrawSphere(worldPos, 0.04f);
        }

        [DrawGizmo(GizmoType.Selected | GizmoType.NonSelected | GizmoType.Pickable)]
        static void DrawGizmo(VRCFuryHapticSocket socket, GizmoType gizmoType) {
            var transform = socket.transform;

            var autoInfo = GetInfoFromLightsOrComponent(socket);
            var handTouchZoneSize = GetHandTouchZoneSize(socket);

            var (lightType, localPosition, localRotation) = autoInfo;
            var localForward = localRotation * Vector3.forward;
            // This is *90 because capsule length is actually "height", so we have to rotate it to make it a length
            var localCapsuleRotation = localRotation * Quaternion.Euler(90,0,0);

            if (handTouchZoneSize != null) {
                var worldLength = handTouchZoneSize.Item1;
                var localLength = worldLength / socket.transform.lossyScale.x;
                var worldRadius = handTouchZoneSize.Item2;
                VRCFuryHapticPlugEditor.DrawCapsule(
                    transform,
                    localPosition + localForward * -(localLength / 2),
                    localCapsuleRotation,
                    worldLength,
                    worldRadius
                );
                VRCFuryGizmoUtils.DrawText(
                    transform.TransformPoint(localPosition + localForward * -(localLength / 2)),
                    "Hand Touch Zone\n(should be INSIDE)",
                    Color.red,
                    true
                );
            }

            DrawGizmo(transform.TransformPoint(localPosition), transform.rotation * localRotation, lightType, GetName(socket));
        }

        public static Tuple<string,VFGameObject> Bake(VRCFuryHapticSocket socket, List<string> usedNames = null, bool onlySenders = false) {
            var transform = socket.transform;
            HapticUtils.RemoveTPSSenders(transform);
            
            HapticUtils.AssertValidScale(transform, "socket");

            var (lightType, localPosition, localRotation) = GetInfoFromLightsOrComponent(socket);
            // This is *90 because capsule length is actually "height", so we have to rotate it to make it a length
            var capsuleRotation = Quaternion.Euler(90,0,0);

            var name = GetName(socket);
            if (usedNames != null) name = HapticUtils.GetNextName(usedNames, name);

            Debug.Log("Baking haptic component in " + transform + " as " + name);

            var bakeRoot = GameObjects.Create("BakedHapticSocket", transform);
            bakeRoot.localPosition = localPosition;
            bakeRoot.localRotation = localRotation;

            var senders = GameObjects.Create("Senders", bakeRoot);

            // Senders
            HapticUtils.AddSender(senders, Vector3.zero, "Root", 0.01f, HapticUtils.CONTACT_ORF_MAIN);
            HapticUtils.AddSender(senders, Vector3.forward * 0.01f, "Front", 0.01f, HapticUtils.CONTACT_ORF_NORM);
            
            var paramPrefix = "OGB/Orf/" + name.Replace('/','_');

            if (onlySenders) {
                var info = GameObjects.Create("Info", bakeRoot);
                if (!string.IsNullOrWhiteSpace(socket.name)) {
                    var nameObj = GameObjects.Create("name=" + socket.name, info);
                }
            } else {
                // Receivers
                var handTouchZoneSize = GetHandTouchZoneSize(socket);
                var receivers = GameObjects.Create("Receivers", bakeRoot);
                if (handTouchZoneSize != null) {
                    var oscDepth = handTouchZoneSize.Item1;
                    var closeRadius = handTouchZoneSize.Item2;
                    HapticUtils.AddReceiver(receivers, Vector3.forward * -oscDepth, paramPrefix + "/TouchSelf", "TouchSelf", oscDepth, HapticUtils.SelfContacts, allowOthers:false, localOnly:true);
                    HapticUtils.AddReceiver(receivers, Vector3.forward * -(oscDepth/2), paramPrefix + "/TouchSelfClose", "TouchSelfClose", closeRadius, HapticUtils.SelfContacts, allowOthers:false, localOnly:true, height: oscDepth, rotation: capsuleRotation, type: ContactReceiver.ReceiverType.Constant);
                    HapticUtils.AddReceiver(receivers, Vector3.forward * -oscDepth, paramPrefix + "/TouchOthers", "TouchOthers", oscDepth, HapticUtils.BodyContacts, allowSelf:false, localOnly:true);
                    HapticUtils.AddReceiver(receivers, Vector3.forward * -(oscDepth/2), paramPrefix + "/TouchOthersClose", "TouchOthersClose", closeRadius, HapticUtils.BodyContacts, allowSelf:false, localOnly:true, height: oscDepth, rotation: capsuleRotation, type: ContactReceiver.ReceiverType.Constant);
                    // Legacy non-upgraded TPS detection
                    HapticUtils.AddReceiver(receivers, Vector3.forward * -oscDepth, paramPrefix + "/PenOthers", "PenOthers", oscDepth, new []{HapticUtils.CONTACT_PEN_MAIN}, allowSelf:false, localOnly:true);
                    HapticUtils.AddReceiver(receivers, Vector3.forward * -(oscDepth/2), paramPrefix + "/PenOthersClose", "PenOthersClose", closeRadius, new []{HapticUtils.CONTACT_PEN_MAIN}, allowSelf:false, localOnly:true, height: oscDepth, rotation: capsuleRotation, type: ContactReceiver.ReceiverType.Constant);
                    
                    var frotRadius = 0.1f;
                    var frotPos = 0.05f;
                    HapticUtils.AddReceiver(receivers, Vector3.forward * frotPos, paramPrefix + "/FrotOthers", "FrotOthers", frotRadius, new []{HapticUtils.CONTACT_ORF_MAIN}, allowSelf:false, localOnly:true);
                }
                
                HapticUtils.AddReceiver(receivers, Vector3.zero, paramPrefix + "/PenSelfNewRoot", "PenSelfNewRoot", 1f, new []{HapticUtils.CONTACT_PEN_ROOT}, allowOthers:false, localOnly:true);
                HapticUtils.AddReceiver(receivers, Vector3.zero, paramPrefix + "/PenSelfNewTip", "PenSelfNewTip", 1f, new []{HapticUtils.CONTACT_PEN_MAIN}, allowOthers:false, localOnly:true);
                HapticUtils.AddReceiver(receivers, Vector3.zero, paramPrefix + "/PenOthersNewRoot", "PenOthersNewRoot", 1f, new []{HapticUtils.CONTACT_PEN_ROOT}, allowSelf:false, localOnly:true);
                HapticUtils.AddReceiver(receivers, Vector3.zero, paramPrefix + "/PenOthersNewTip", "PenOthersNewTip", 1f, new []{HapticUtils.CONTACT_PEN_MAIN}, allowSelf:false, localOnly:true);
            }
            
            HapticUtils.AddVersionContacts(bakeRoot, paramPrefix, onlySenders, false);

            if (lightType != VRCFuryHapticSocket.AddLight.None) {
                var lights = GameObjects.Create("Lights", bakeRoot);

                ForEachPossibleLight(transform, false, light => {
                    AvatarCleaner.RemoveComponent(light);
                });

                if (EditorUserBuildSettings.activeBuildTarget != BuildTarget.Android) {
                    var main = GameObjects.Create("Root", lights);
                    var mainLight = main.AddComponent<Light>();
                    mainLight.type = LightType.Point;
                    mainLight.color = Color.black;
                    mainLight.range = lightType == VRCFuryHapticSocket.AddLight.Ring ? 0.42f : 0.41f;
                    mainLight.shadows = LightShadows.None;
                    mainLight.renderMode = LightRenderMode.ForceVertex;

                    var front = GameObjects.Create("Front", lights);
                    front.localPosition = Vector3.forward * 0.01f / lights.worldScale.x;
                    var frontLight = front.AddComponent<Light>();
                    frontLight.type = LightType.Point;
                    frontLight.color = Color.black;
                    frontLight.range = 0.45f;
                    frontLight.shadows = LightShadows.None;
                    frontLight.renderMode = LightRenderMode.ForceVertex;
                }
            }
            
            if (EditorApplication.isPlaying) {
                var gizmo = socket.owner().AddComponent<VRCFurySocketGizmo>();
                gizmo.pos = localPosition;
                gizmo.rot = localRotation;
                gizmo.type = lightType;
                gizmo.hideFlags = HideFlags.DontSave;
                foreach (var light in bakeRoot.GetComponentsInSelfAndChildren<Light>()) {
                    light.hideFlags |= HideFlags.HideInHierarchy;
                }
                foreach (var contact in bakeRoot.GetComponentsInSelfAndChildren<ContactBase>()) {
                    contact.hideFlags |= HideFlags.HideInHierarchy;
                }
            }

            return Tuple.Create(name, bakeRoot);
        }

        private static Tuple<float, float> GetHandTouchZoneSize(VRCFuryHapticSocket socket) {
            bool enableHandTouchZone = false;
            if (socket.enableHandTouchZone2 == VRCFuryHapticSocket.EnableTouchZone.On) {
                enableHandTouchZone = true;
            } else if (socket.enableHandTouchZone2 == VRCFuryHapticSocket.EnableTouchZone.Auto) {
                enableHandTouchZone = ShouldProbablyHaveTouchZone(socket);
            }
            if (!enableHandTouchZone) {
                return null;
            }
            var length = socket.length;
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

        public static Tuple<VRCFuryHapticSocket.AddLight, Vector3, Quaternion> GetInfoFromLightsOrComponent(VRCFuryHapticSocket socket) {
            if (socket.addLight != VRCFuryHapticSocket.AddLight.None) {
                var type = socket.addLight;
                if (type == VRCFuryHapticSocket.AddLight.Auto) type = ShouldProbablyBeHole(socket) ? VRCFuryHapticSocket.AddLight.Hole : VRCFuryHapticSocket.AddLight.Ring;
                var position = socket.position;
                var rotation = Quaternion.Euler(socket.rotation);
                return Tuple.Create(type, position, rotation);
            }
            
            var lightInfo = GetInfoFromLights(socket.transform);
            if (lightInfo != null) {
                return lightInfo;
            }

            return Tuple.Create(VRCFuryHapticSocket.AddLight.None, Vector3.zero, Quaternion.identity);
        }

        /**
         * Visit every light that could possibly be used for this socket. This includes all children,
         * and single-depth children of all parents.
         */
        public static void ForEachPossibleLight(VFGameObject obj, bool directOnly, Action<Light> act) {
            var visited = new HashSet<Light>();
            void Visit(Light light) {
                if (visited.Contains(light)) return;
                visited.Add(light);
                if (!IsHole(light) && !IsRing(light) && !IsNormal(light)) return;
                act(light);
            }
            foreach (var child in obj.Children()) {
                foreach (var light in child.gameObject.GetComponents<Light>()) {
                    Visit(light);
                }
            }
            if (!directOnly) {
                foreach (var light in obj.GetComponentsInSelfAndChildren<Light>()) {
                    Visit(light);
                }
            }
        }
        public static Tuple<VRCFuryHapticSocket.AddLight, Vector3, Quaternion> GetInfoFromLights(Transform obj, bool directOnly = false) {
            var isRing = false;
            Light main = null;
            Light normal = null;
            ForEachPossibleLight(obj, directOnly, light => {
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
            });

            if (main == null || normal == null) return null;

            var position = obj.InverseTransformPoint(main.transform.position);
            var normalPosition = obj.InverseTransformPoint(normal.transform.position);
            var forward = (normalPosition - position).normalized;
            var rotation = Quaternion.LookRotation(forward);

            return Tuple.Create(isRing ? VRCFuryHapticSocket.AddLight.Ring : VRCFuryHapticSocket.AddLight.Hole, position, rotation);
        }

        private static bool IsDirectChildOfHips(VRCFuryHapticSocket socket) {
            return IsChildOfBone(socket, HumanBodyBones.Hips)
                && !IsChildOfBone(socket, HumanBodyBones.Chest)
                && !IsChildOfBone(socket, HumanBodyBones.Spine)
                && !IsChildOfBone(socket, HumanBodyBones.LeftUpperArm)
                && !IsChildOfBone(socket, HumanBodyBones.LeftUpperLeg)
                && !IsChildOfBone(socket, HumanBodyBones.RightUpperArm)
                && !IsChildOfBone(socket, HumanBodyBones.RightUpperLeg);
        }

        public static bool ShouldProbablyHaveTouchZone(VRCFuryHapticSocket socket) {
            if (IsDirectChildOfHips(socket)) {
                var name = GetName(socket).ToLower();
                if (name.Contains("rubbing") || name.Contains("job")) {
                    return false;
                }
                return true;
            }
            return false;
        }
        
        public static bool ShouldProbablyBeHole(VRCFuryHapticSocket socket) {
            if (IsChildOfBone(socket, HumanBodyBones.Head)) return true;
            return ShouldProbablyHaveTouchZone(socket);
        }

        private static bool IsChildOfBone(VRCFuryHapticSocket socket, HumanBodyBones bone) {
            try {
                VFGameObject obj = socket.owner();
                VFGameObject avatarObject = obj.GetComponentInSelfOrParent<VRCAvatarDescriptor>()?.owner();
                if (!avatarObject) return false;
                var boneObj = VRCFArmatureUtils.FindBoneOnArmatureOrNull(avatarObject, bone);
                return boneObj && IsChildOf(boneObj.transform, socket.transform);
            } catch (Exception) {
                return false;
            }
        }

        private static string GetName(VRCFuryHapticSocket socket) {
            var name = socket.name;
            if (!string.IsNullOrWhiteSpace(name)) return name;
            return HapticUtils.GetName(socket.owner());
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
