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
            
            container.Add(VRCFuryHapticPlugEditor.ConstraintWarning(target.gameObject, true));
            
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
                    new List<string>() { "Auto", "Hole", "Ring", "Ring Bidirectional" },
                    addLightProp.enumValueIndex == 3 ? 3 : addLightProp.enumValueIndex == 2 ? 2 : addLightProp.enumValueIndex == 1 ? 1 : 0
                );
                modeField.RegisterValueChangedCallback(cb => {
                    addLightProp.enumValueIndex = cb.newValue == "Hole" ? 1 : cb.newValue == "Ring" ? 2 : cb.newValue == "Ring Bidirectional" ? 3 : 4;
                    addLightProp.serializedObject.ApplyModifiedProperties();
                });
                section.Add(VRCFuryEditorUtils.BetterProp(addLightProp, "Mode", fieldOverride: modeField, tooltip: "'Auto' will set to Hole if attached to hips or head bone.\n'Bidirectional' will allow entry from either side using SPS. DPS & TPS will treat these as single sided"));
                return section;
            }, addLightProp));

            var addMenuItemProp = serializedObject.FindProperty("addMenuItem");
            container.Add(VRCFuryEditorUtils.BetterProp(addMenuItemProp, "Enable Menu Toggle"));
            container.Add(VRCFuryEditorUtils.RefreshOnChange(() => {
                if (!addMenuItemProp.boolValue) return new VisualElement();
                var toggles = VRCFuryEditorUtils.Section("Menu Toggle", "A menu item will be created for this socket");
                toggles.Add(VRCFuryEditorUtils.BetterProp(serializedObject.FindProperty("enableAuto"), "Include in Auto selection?", tooltip: "If checked, this socket will be eligible to be chosen during 'Auto Mode', which is an option in your menu which will automatically enable the socket nearest to a plug."));
                toggles.Add(VRCFuryEditorUtils.BetterProp(serializedObject.FindProperty("menuIcon"), "Menu Icon", tooltip: "Override the menu icon used for this socket's individual toggle. Looking to move or change the icon of the main SPS menu? Add a VRCFury 'SPS Options' component to the avatar root."));
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

                da.Add(VRCFuryEditorUtils.List(serializedObject.FindProperty("depthActions")));
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
            adv.Add(VRCFuryEditorUtils.BetterProp(serializedObject.FindProperty("useHipAvoidance"), "Use hip avoidance",
                tooltip: "If this socket is placed on the hip bone, this option will prevent triggering or receiving haptics or depth animations from other plugs on the hip bone."));
            adv.Add(VRCFuryEditorUtils.BetterCheckbox(serializedObject.FindProperty("unitsInMeters"), "Units are in world-space"));
            adv.Add(VRCFuryEditorUtils.BetterProp(serializedObject.FindProperty("position"), "Position"));
            adv.Add(VRCFuryEditorUtils.BetterProp(serializedObject.FindProperty("rotation"), "Rotation"));

            return container;
        }
        
        [CustomPropertyDrawer(typeof(VRCFuryHapticSocket.DepthAction))]
        public class DepthActionDrawer : PropertyDrawer {
            public override VisualElement CreatePropertyGUI(SerializedProperty prop) {
                var c = new VisualElement();
                c.Add(VRCFuryEditorUtils.BetterProp(prop.FindPropertyRelative("state")));
                c.Add(VRCFuryEditorUtils.BetterProp(prop.FindPropertyRelative("startDistance"), "Distance when animation begins"));
                c.Add(VRCFuryEditorUtils.BetterProp(prop.FindPropertyRelative("endDistance"), "Distance when animation is maxed"));
                c.Add(VRCFuryEditorUtils.BetterProp(prop.FindPropertyRelative("enableSelf"), "Allow avatar to trigger its own animation?"));
                c.Add(VRCFuryEditorUtils.BetterProp(prop.FindPropertyRelative("smoothingSeconds"), "Smoothing Seconds", tooltip: "It will take approximately this many seconds to smoothly blend to the target depth. Beware that this smoothing is based on framerate, so higher FPS will result in faster smoothing."));
                return c;
            }
        }

        [CustomEditor(typeof(VRCFurySocketGizmo), true)]
        [InitializeOnLoad]
        public class VRCFuryHapticPlaySocketEditor : UnityEditor.Editor {
            static VRCFuryHapticPlaySocketEditor() {
                VRCFurySocketGizmo.EnableSceneLighting = () => {
                    SceneView sv = EditorWindow.GetWindow<SceneView>();
                    if (sv != null) {
                        sv.sceneLighting = true;
                        sv.drawGizmos = true;
                    }
                };
            }
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
            } else if (type == VRCFuryHapticSocket.AddLight.RingBidirectional) {
                text += " (Ring Bidirectional)\nPlug normally follows orange arrow";
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
            if (type == VRCFuryHapticSocket.AddLight.Ring || type == VRCFuryHapticSocket.AddLight.RingBidirectional) {
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

        public static VFGameObject Bake(VRCFuryHapticSocket socket) {
            var transform = socket.transform;
            HapticUtils.RemoveTPSSenders(transform);
            HapticUtils.AssertValidScale(transform, "socket");

            var (lightType, localPosition, localRotation) = GetInfoFromLightsOrComponent(socket);

            var bakeRoot = GameObjects.Create("BakedHapticSocket", transform);
            bakeRoot.localPosition = localPosition;
            bakeRoot.localRotation = localRotation;

            var senders = GameObjects.Create("Senders", bakeRoot);

            // Senders
            {
                var rootTags = new List<string>();
                rootTags.Add(HapticUtils.TagTpsOrfRoot);
                rootTags.Add(HapticUtils.TagSpsSocketRoot);
                if (lightType != VRCFuryHapticSocket.AddLight.None) {
                    switch (lightType) {
                        case VRCFuryHapticSocket.AddLight.Ring:
                            rootTags.Add(HapticUtils.TagSpsSocketIsRing);
                            break;
                        case VRCFuryHapticSocket.AddLight.RingBidirectional:
                            rootTags.Add(HapticUtils.TagSpsSocketIsRing);
                            rootTags.Add(HapticUtils.TagSpsSocketIsBidirectional);
                            break;
                        default:
                            rootTags.Add(HapticUtils.TagSpsSocketIsHole);
                            break;
                    }
                }
                HapticUtils.AddSender(senders, Vector3.zero, "Root", 0.001f, rootTags.ToArray(), useHipAvoidance: socket.useHipAvoidance);
                HapticUtils.AddSender(senders, Vector3.forward * 0.01f, "Front", 0.001f,
                    new[] { HapticUtils.TagTpsOrfFront, HapticUtils.TagSpsSocketFront }, useHipAvoidance: socket.useHipAvoidance);
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
                    mainLight.range = lightType == VRCFuryHapticSocket.AddLight.Ring ? 0.4202f :
                        lightType == VRCFuryHapticSocket.AddLight.RingBidirectional ? 0.4203f : 0.4102f;
                    mainLight.shadows = LightShadows.None;
                    mainLight.renderMode = LightRenderMode.ForceVertex;

                    var front = GameObjects.Create("Front", lights);
                    front.localPosition = Vector3.forward * 0.01f / lights.worldScale.x;
                    var frontLight = front.AddComponent<Light>();
                    frontLight.type = LightType.Point;
                    frontLight.color = Color.black;
                    frontLight.range = 0.4502f;
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

            return bakeRoot;
        }

        public static Tuple<float, float> GetHandTouchZoneSize(VRCFuryHapticSocket socket) {
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
                if (type == VRCFuryHapticSocket.AddLight.Auto)
                    type = ShouldProbablyBeHole(socket) ? VRCFuryHapticSocket.AddLight.Hole :
                        ShouldProbablyBeReversible(socket) ? VRCFuryHapticSocket.AddLight.RingBidirectional : VRCFuryHapticSocket.AddLight.Ring;
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

        public static bool ShouldProbablyBeReversible(VRCFuryHapticSocket socket) {
            if (HapticUtils.IsChildOfBone(socket.owner(), HumanBodyBones.LeftHand)) return true;
            if (HapticUtils.IsChildOfBone(socket.owner(), HumanBodyBones.RightHand)) return true;
            return false;
        }

        public static string GetName(VRCFuryHapticSocket socket) {
            var name = socket.name;
            if (!string.IsNullOrWhiteSpace(name)) return name;
            return HapticUtils.GetName(socket.owner());
        }
    }
}
