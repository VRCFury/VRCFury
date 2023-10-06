using System;
using System.Collections.Generic;
using System.Globalization;
using System.Runtime.CompilerServices;
using System.Text.RegularExpressions;
using UnityEditor;
using UnityEditor.UIElements;
using UnityEngine;
using UnityEngine.Animations;
using UnityEngine.UIElements;
using VF.Builder;
using VF.Builder.Haptics;
using VF.Component;
using VF.Menu;
using VF.Model;
using VRC.Dynamics;
using VRC.SDK3.Avatars.Components;
using VRC.SDK3.Dynamics.Contact.Components;

namespace VF.Inspector {
    [CustomEditor(typeof(VRCFuryHapticSocket), true)]
    public class VRCFuryHapticSocketEditor : VRCFuryComponentEditor<VRCFuryHapticSocket> {
        public override VisualElement CreateEditor(SerializedObject serializedObject, VRCFuryHapticSocket target) {
            var container = new VisualElement();
            
            container.Add(VRCFuryEditorUtils.BetterProp(serializedObject.FindProperty("name"), "Name in menu / connected apps"));
            
            var addLightProp = serializedObject.FindProperty("addLight");
            var spsEnabledCheckbox = new Toggle();
            var noneIndex = (int)VRCFuryHapticSocket.AddLight.None;
            var autoIndex = (int)VRCFuryHapticSocket.AddLight.Auto;
            spsEnabledCheckbox.SetValueWithoutNotify(addLightProp.enumValueIndex != noneIndex);
            spsEnabledCheckbox.RegisterValueChangedCallback(cb => {
                if (cb.newValue) addLightProp.enumValueIndex = autoIndex;
                else addLightProp.enumValueIndex = noneIndex;
                addLightProp.serializedObject.ApplyModifiedProperties();
            });
            container.Add(VRCFuryEditorUtils.BetterProp(addLightProp, "Enable SPS (Super Plug Shader)", fieldOverride: spsEnabledCheckbox));
            container.Add(VRCFuryEditorUtils.RefreshOnChange(() => {
                if (addLightProp.enumValueIndex == noneIndex) return new VisualElement();
                var section = VRCFuryEditorUtils.Section("SPS (Super Plug Shader)", "SPS/TPS/DPS plugs will deform toward this socket\nCheck out vrcfury.com/sps for details");
                var modeField = new PopupField<string>(
                    new List<string>() { "Auto", "Hole", "Ring" },
                    addLightProp.enumValueIndex == 2 ? 2 : addLightProp.enumValueIndex == 1 ? 1 : 0
                );
                modeField.RegisterValueChangedCallback(cb => {
                    addLightProp.enumValueIndex = cb.newValue == "Hole" ? 1 : cb.newValue == "Ring" ? 2 : 3;
                    addLightProp.serializedObject.ApplyModifiedProperties();
                });
                section.Add(VRCFuryEditorUtils.BetterProp(addLightProp, "Mode", fieldOverride: modeField, tooltip: "'Auto' will set to Hole if attached to hips or head bone."));
                return section;
            }, addLightProp));

            var addMenuItemProp = serializedObject.FindProperty("addMenuItem");
            container.Add(VRCFuryEditorUtils.BetterProp(addMenuItemProp, "Enable Menu Toggle"));
            container.Add(VRCFuryEditorUtils.RefreshOnChange(() => {
                if (!addMenuItemProp.boolValue) return new VisualElement();
                var toggles = VRCFuryEditorUtils.Section("Menu Toggle", "A menu item will be created for this socket");
                toggles.Add(VRCFuryEditorUtils.BetterProp(serializedObject.FindProperty("enableAuto"), "Include in Auto selection?", tooltip: "If checked, this socket will be eligible to be chosen during 'Auto Mode', which is an option in your menu which will automatically enable the socket nearest to a plug."));
                return toggles;
            }, addMenuItemProp));

            var enableDepthAnimationsProp = serializedObject.FindProperty("enableDepthAnimations");
            container.Add(VRCFuryEditorUtils.BetterProp(
                enableDepthAnimationsProp,
                "Enable Depth Animations",
                tooltip: "Allows you to animate anything based on the proximity of a plug near this socket"
            ));
            container.Add(VRCFuryEditorUtils.RefreshOnChange(() => {
                if (!enableDepthAnimationsProp.boolValue) return new VisualElement();
                var da = VRCFuryEditorUtils.Section("Depth Animations");
                
                da.Add(VRCFuryEditorUtils.Info(
                    "If you provide a non-static (moving) animation clip, the clip will run from start " +
                    "to end depending on penetration depth. Otherwise, it will animate from 'off' to 'on' depending on depth."));
                
                var unscaledUnitsProp = serializedObject.FindProperty("unitsInMeters");
                da.Add(VRCFuryEditorUtils.RefreshOnChange(() => VRCFuryEditorUtils.Info(
                    "Distance = 0 : Tip of plug is touching socket\n" +
                    "Distance > 0 : Tip of plug is outside socket\n" +
                    "Distance < 0 = Tip of plug is inside socket\n" +
                    (unscaledUnitsProp.boolValue ? "1 Unit is 1 Meter (~3.28 feet)" : $"1 Unit is {target.transform.lossyScale.z} Meter(s) (~{Math.Round(target.transform.lossyScale.z * 3.28, 2)} feet)")
                ), unscaledUnitsProp));

                da.Add(VRCFuryEditorUtils.List(serializedObject.FindProperty("depthActions"), (i, prop) => {
                    var c = new VisualElement();

                    c.Add(VRCFuryEditorUtils.BetterProp(prop.FindPropertyRelative("state")));
                    c.Add(VRCFuryEditorUtils.BetterProp(prop.FindPropertyRelative("startDistance"), "Distance when animation begins"));
                    c.Add(VRCFuryEditorUtils.BetterProp(prop.FindPropertyRelative("endDistance"), "Distance when animation is maxed"));
                    c.Add(VRCFuryEditorUtils.BetterProp(prop.FindPropertyRelative("enableSelf"), "Allow avatar to trigger its own animation?"));
                    c.Add(VRCFuryEditorUtils.BetterProp(prop.FindPropertyRelative("smoothingSeconds"), "Smoothing Seconds", tooltip: "It will take approximately this many seconds to smoothly blend to the target depth. Beware that this smoothing is based on framerate, so higher FPS will result in faster smoothing."));
                    return c;
                }));
                return da;
            }, enableDepthAnimationsProp));
            
            var enableActiveAnimationProp = serializedObject.FindProperty("enableActiveAnimation");
            container.Add(VRCFuryEditorUtils.BetterProp(
                enableActiveAnimationProp,
                "Enable Active Animation",
                tooltip: "This animation will be active whenever the socket is enabled in the menu"
            ));
            container.Add(VRCFuryEditorUtils.RefreshOnChange(() => {
                if (!enableActiveAnimationProp.boolValue) return new VisualElement();
                var activeBox = VRCFuryEditorUtils.Section("Active Animation",
                    "This animation will be active whenever the socket is enabled in the menu");
                activeBox.Add(VRCFuryEditorUtils.BetterProp(
                    serializedObject.FindProperty("activeActions")
                ));
                return activeBox;
            }, enableActiveAnimationProp));

            var haptics = VRCFuryEditorUtils.Section("Haptics", "OGB haptic support is enabled on this socket by default");
            container.Add(haptics);
            haptics.Add(VRCFuryEditorUtils.BetterProp(
                serializedObject.FindProperty("enableHandTouchZone2"),
                "Enable hand touch zone? (Auto will add only if child of Hips)"
            ));
            haptics.Add(VRCFuryEditorUtils.BetterProp(
                serializedObject.FindProperty("length"),
                "Hand touch zone depth override in meters:\nNote, this zone is only used for hand touches, not plug interaction."
            ));
            
            var adv = new Foldout {
                text = "Advanced",
                value = false,
            };
            container.Add(adv);
            adv.Add(VRCFuryEditorUtils.BetterCheckbox(serializedObject.FindProperty("unitsInMeters"), "Units are in world-space"));
            adv.Add(VRCFuryEditorUtils.BetterProp(serializedObject.FindProperty("position"), "Position"));
            adv.Add(VRCFuryEditorUtils.BetterProp(serializedObject.FindProperty("rotation"), "Rotation"));

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
            var orange = new Color(1f, 0.5f, 0);
            var isAndroid = EditorUserBuildSettings.activeBuildTarget == BuildTarget.Android;

            var discColor = orange;
            
            var text = "Socket";
            if (!string.IsNullOrWhiteSpace(name)) text += $" '{name}'";
            if (isAndroid) {
                text += " (SPS Disabled)\nThis is an android project!";
                discColor = Color.red;
            } else if (type == VRCFuryHapticSocket.AddLight.Hole) {
                text += " (Hole)\nPlug follows orange arrow";
            } else if (type == VRCFuryHapticSocket.AddLight.Ring) {
                text += " (Ring)\nPlug follows orange arrow";
            } else {
                text += " (SPS disabled)";
                discColor = Color.red;
            }

            var worldForward = worldRot * Vector3.forward;
            VRCFuryGizmoUtils.WithHandles(() => {
                Handles.color = discColor;
                Handles.DrawWireDisc(worldPos, worldForward, 0.02f);
            });
            VRCFuryGizmoUtils.WithHandles(() => {
                Handles.color = discColor;
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
            HapticUtils.AddSender(senders, Vector3.zero, "Root", 0.001f, new [] { HapticUtils.CONTACT_ORF_MAIN });
            HapticUtils.AddSender(senders, Vector3.forward * 0.01f, "Front", 0.001f, new [] { HapticUtils.CONTACT_ORF_NORM });
            if (lightType != VRCFuryHapticSocket.AddLight.None) {
                HapticUtils.AddSender(
                    senders,
                    Vector3.zero,
                    "Type",
                    0.001f,
                    new [] { lightType == VRCFuryHapticSocket.AddLight.Ring ? HapticUtils.CONTACT_ORF_IsRing : HapticUtils.CONTACT_ORF_IsHole }
                );
            }
            
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
                    HapticUtils.AddReceiver(receivers, Vector3.forward * -oscDepth, paramPrefix + "/TouchSelf", "TouchSelf", oscDepth, HapticUtils.SelfContacts, HapticUtils.ReceiverParty.Self, localOnly:true);
                    HapticUtils.AddReceiver(receivers, Vector3.forward * -(oscDepth/2), paramPrefix + "/TouchSelfClose", "TouchSelfClose", closeRadius, HapticUtils.SelfContacts, HapticUtils.ReceiverParty.Self, localOnly:true, height: oscDepth, rotation: capsuleRotation, type: ContactReceiver.ReceiverType.Constant);
                    HapticUtils.AddReceiver(receivers, Vector3.forward * -oscDepth, paramPrefix + "/TouchOthers", "TouchOthers", oscDepth, HapticUtils.BodyContacts, HapticUtils.ReceiverParty.Others, localOnly:true);
                    HapticUtils.AddReceiver(receivers, Vector3.forward * -(oscDepth/2), paramPrefix + "/TouchOthersClose", "TouchOthersClose", closeRadius, HapticUtils.BodyContacts, HapticUtils.ReceiverParty.Others, localOnly:true, height: oscDepth, rotation: capsuleRotation, type: ContactReceiver.ReceiverType.Constant);
                    // Legacy non-upgraded TPS detection
                    HapticUtils.AddReceiver(receivers, Vector3.forward * -oscDepth, paramPrefix + "/PenOthers", "PenOthers", oscDepth, new []{HapticUtils.CONTACT_PEN_MAIN}, HapticUtils.ReceiverParty.Others, localOnly:true);
                    HapticUtils.AddReceiver(receivers, Vector3.forward * -(oscDepth/2), paramPrefix + "/PenOthersClose", "PenOthersClose", closeRadius, new []{HapticUtils.CONTACT_PEN_MAIN}, HapticUtils.ReceiverParty.Others, localOnly:true, height: oscDepth, rotation: capsuleRotation, type: ContactReceiver.ReceiverType.Constant);
                    
                    var frotRadius = 0.1f;
                    var frotPos = 0.05f;
                    HapticUtils.AddReceiver(receivers, Vector3.forward * frotPos, paramPrefix + "/FrotOthers", "FrotOthers", frotRadius, new []{HapticUtils.CONTACT_ORF_MAIN}, HapticUtils.ReceiverParty.Others, localOnly:true);
                }
                
                HapticUtils.AddReceiver(receivers, Vector3.zero, paramPrefix + "/PenSelfNewRoot", "PenSelfNewRoot", 1f, new []{HapticUtils.CONTACT_PEN_ROOT}, HapticUtils.ReceiverParty.Self, localOnly:true);
                HapticUtils.AddReceiver(receivers, Vector3.zero, paramPrefix + "/PenSelfNewTip", "PenSelfNewTip", 1f, new []{HapticUtils.CONTACT_PEN_MAIN}, HapticUtils.ReceiverParty.Self, localOnly:true);
                HapticUtils.AddReceiver(receivers, Vector3.zero, paramPrefix + "/PenOthersNewRoot", "PenOthersNewRoot", 1f, new []{HapticUtils.CONTACT_PEN_ROOT}, HapticUtils.ReceiverParty.Others, localOnly:true);
                HapticUtils.AddReceiver(receivers, Vector3.zero, paramPrefix + "/PenOthersNewTip", "PenOthersNewTip", 1f, new []{HapticUtils.CONTACT_PEN_MAIN}, HapticUtils.ReceiverParty.Others, localOnly:true);
            }

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
            var length = socket.length * (socket.unitsInMeters ? 1f : socket.transform.lossyScale.z); ;
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

        public static bool ShouldProbablyHaveTouchZone(VRCFuryHapticSocket socket) {
            if (HapticUtils.IsDirectChildOfHips(socket.owner())) {
                var name = GetName(socket).ToLower();
                if (name.Contains("rubbing") || name.Contains("job")) {
                    return false;
                }
                return true;
            }
            return false;
        }

        public static bool ShouldProbablyBeHole(VRCFuryHapticSocket socket) {
            if (HapticUtils.IsChildOfBone(socket.owner(), HumanBodyBones.Head)) return true;
            return ShouldProbablyHaveTouchZone(socket);
        }

        private static string GetName(VRCFuryHapticSocket socket) {
            var name = socket.name;
            if (!string.IsNullOrWhiteSpace(name)) return name;
            return HapticUtils.GetName(socket.owner());
        }
    }
}
