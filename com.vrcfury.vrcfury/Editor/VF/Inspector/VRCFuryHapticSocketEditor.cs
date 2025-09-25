using System;
using System.Collections.Generic;
using System.Linq;
using JetBrains.Annotations;
using UnityEditor;
using UnityEditor.UIElements;
using UnityEngine;
using UnityEngine.UIElements;
using VF.Builder;
using VF.Builder.Haptics;
using VF.Component;
using VF.Menu;
using VF.Service;
using VF.Utils;
using VRC.Dynamics;
using VRC.SDK3.Avatars.Components;

namespace VF.Inspector {
    [CustomEditor(typeof(VRCFuryHapticSocket), true)]
    internal class VRCFuryHapticSocketEditor : VRCFuryComponentEditor<VRCFuryHapticSocket> {
        protected override VisualElement CreateEditor(SerializedObject serializedObject, VRCFuryHapticSocket target) {
            var container = new VisualElement();
            
            container.Add(VRCFuryHapticPlugEditor.ConstraintWarning(target, true));
            
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
            container.Add(VRCFuryEditorUtils.BetterProp(addLightProp, "Enable Deformation", fieldOverride: spsEnabledCheckbox));
            container.Add(VRCFuryEditorUtils.RefreshOnChange(() => {
                if (addLightProp.enumValueIndex == noneIndex) return new VisualElement();
                var section = VRCFuryEditorUtils.Section("Deformation (Super Plug Shader)", "SPS/TPS/DPS plugs will deform toward this socket\nCheck out vrcfury.com/sps for details");
                var modeField = new PopupField<string>(
                    new List<string>() { "Auto", "Hole", "Ring", "One-Way Ring (Uncommon)" },
                    addLightProp.enumValueIndex == 4 ? 3 : addLightProp.enumValueIndex == 2 ? 2 : addLightProp.enumValueIndex == 1 ? 1 : 0
                );
                modeField.RegisterValueChangedCallback(cb => {
                    addLightProp.enumValueIndex = cb.newValue == "Hole" ? 1 : cb.newValue == "Ring" ? 2 : cb.newValue == "One-Way Ring (Uncommon)" ? 4 : 3;
                    addLightProp.serializedObject.ApplyModifiedProperties();
                });
                section.Add(VRCFuryEditorUtils.BetterProp(addLightProp, "Mode", fieldOverride: modeField,
                    tooltip: "'Auto' will set to Hole if attached to hips or head bone.\n" +
                             "'Rings' can be entered from either side using SPS, but TPS/DPS will only enter one side.\n" +
                             "'One-Way Rings' can only be entered from one side."));
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

            // Depth Animations
            container.Add(VRCFuryEditorUtils.CheckboxList(
                serializedObject.FindProperty("depthActions2"),
                "Enable Depth Animations",
                "Allows you to animate anything based on the proximity of a plug near this socket",
                "Depth Animations"
            ));

            // Active Animations
            container.Add(VRCFuryEditorUtils.CheckboxList(
                serializedObject.FindProperty("activeActions.actions"),
                "Enable Active Animation",
                "This animation will be active whenever the socket is enabled in the menu",
                "Active Animation",
                VRCFuryEditorUtils.BetterProp(serializedObject.FindProperty("activeActions"))
            ));

            var haptics = VRCFuryHapticPlugEditor.GetHapticsSection();
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
            
            var plugParams = VRCFuryEditorUtils.Section("Global Plug Parameters");
            adv.Add(plugParams);
            var enablePlugLengthParameterProp = serializedObject.FindProperty("enablePlugLengthParameter");
            var enablePlugWidthParameterProp = serializedObject.FindProperty("enablePlugWidthParameter");
            plugParams.Add(VRCFuryEditorUtils.BetterProp(enablePlugLengthParameterProp, "Plug Length (meters)"));
            plugParams.Add(VRCFuryEditorUtils.BetterProp(serializedObject.FindProperty("plugLengthParameterName")));
            plugParams.Add(VRCFuryEditorUtils.BetterProp(enablePlugWidthParameterProp, "Plug Radius (meters)"));
            plugParams.Add(VRCFuryEditorUtils.BetterProp(serializedObject.FindProperty("plugWidthParameterName")));
            
            adv.Add(VRCFuryEditorUtils.BetterProp(serializedObject.FindProperty("useHipAvoidance"), "Use hip avoidance",
                tooltip: "If this socket is placed on the hip bone, this option will prevent triggering or receiving haptics or depth animations from other plugs on the hip bone."));
            adv.Add(VRCFuryEditorUtils.BetterProp(serializedObject.FindProperty("unitsInMeters"), "Units are in world-space"));
            adv.Add(VRCFuryEditorUtils.BetterProp(serializedObject.FindProperty("position"), "Position"));
            adv.Add(VRCFuryEditorUtils.BetterProp(serializedObject.FindProperty("rotation"), "Rotation"));

            return container;
        }
        
        [CustomPropertyDrawer(typeof(VRCFuryHapticSocket.DepthActionNew))]
        public class DepthActionDrawer : PropertyDrawer {
            public override VisualElement CreatePropertyGUI(SerializedProperty prop) {
                var c = new VisualElement();
                c.Add(VRCFuryEditorUtils.BetterProp(prop.FindPropertyRelative("actionSet")));
                var units = prop.FindPropertyRelative("units");
                c.Add(VRCFuryEditorUtils.RefreshOnChange(() =>
                    VRCFuryEditorUtils.BetterProp(
                        null,
                        "Activation distance",
                        tooltip: "Animation will begin at the far distance, and 'max' at the near distance. If you provide a static action or clip," +
                                 " the animation will be fully 'off' at the far distance, and fully 'on' at the near distance.",
                        fieldOverride: new DepthActionSlider(prop.FindPropertyRelative("range"), (VRCFuryHapticSocket.DepthActionUnits)units.enumValueIndex)
                    )
                , units));
                c.Add(VRCFuryEditorUtils.BetterProp(
                    units,
                    "Range Units"
                ));
                c.Add(VRCFuryEditorUtils.BetterProp(prop.FindPropertyRelative("enableSelf"), "Allow avatar to trigger its own animation?"));
                c.Add(VRCFuryEditorUtils.BetterProp(prop.FindPropertyRelative("smoothingSeconds"), "Smoothing Seconds", tooltip: "It will take approximately this many seconds to smoothly blend to the target depth. Beware that this smoothing is based on framerate, so higher FPS will result in faster smoothing."));
                c.Add(VRCFuryEditorUtils.BetterProp(prop.FindPropertyRelative("reverseClip"), "Reverse clip (unusual)"));
                return c;
            }
        }

        [CustomEditor(typeof(VRCFurySocketGizmo), true)]
        public class VRCFuryHapticPlaySocketEditor : UnityEditor.Editor {
            [InitializeOnLoadMethod]
            private static void Init() {
                VRCFurySocketGizmo.EnableSceneLighting = () => {
                    var sv = EditorWindowFinder.GetWindows<SceneView>().FirstOrDefault();
                    if (sv != null) {
                        sv.sceneLighting = true;
                        sv.drawGizmos = true;
                    }
                };
            }
            [DrawGizmo(GizmoType.Selected | GizmoType.NonSelected | GizmoType.Pickable)]
            static void DrawGizmo2(VRCFurySocketGizmo gizmo, GizmoType gizmoType) {
                if (!gizmo.show) return;
                DrawGizmo(gizmo.owner().TransformPoint(gizmo.pos), gizmo.owner().worldRotation * gizmo.rot, gizmo.type, "", Selection.activeGameObject == gizmo.owner());
            }
        }

        static void DrawGizmo(Vector3 worldPos, Quaternion worldRot, VRCFuryHapticSocket.AddLight type, string name, bool selected) {
            var orange = new Color(1f, 0.5f, 0);

            var discColor = orange;
            
            var text = "SPS Socket";
            if (!string.IsNullOrWhiteSpace(name)) text += $" '{name}'";
            if (!BuildTargetUtils.IsDesktop()) {
                text += " (Deformation Disabled)\nThis is an Android/iOS project!";
                discColor = Color.red;
            } else if (type == VRCFuryHapticSocket.AddLight.Hole) {
                text += " (Hole)\nPlug follows orange arrow";
            } else if (type == VRCFuryHapticSocket.AddLight.Ring) {
                text += " (Ring)\nSPS enters either direction\nDPS/TPS only follow orange arrow";
            } else if (type == VRCFuryHapticSocket.AddLight.RingOneWay) {
                text += " (One-Way Ring)\nPlug follows orange arrow";
            } else {
                text += " (Deformation disabled)";
                discColor = Color.red;
            }

            var worldForward = worldRot * Vector3.forward;
            VRCFuryGizmoUtils.DrawDisc(worldPos, worldForward, 0.02f, discColor);
            VRCFuryGizmoUtils.DrawDisc(worldPos, worldForward, 0.04f, discColor);
            if (type == VRCFuryHapticSocket.AddLight.RingOneWay) {
                VRCFuryGizmoUtils.DrawArrow(
                    worldPos + worldForward * 0.05f,
                    worldPos + worldForward * -0.05f,
                    orange
                );
            } else if (type == VRCFuryHapticSocket.AddLight.Ring) {
                VRCFuryGizmoUtils.DrawArrow(
                    worldPos,
                    worldPos + worldForward * -0.05f,
                    orange
                );
                VRCFuryGizmoUtils.DrawArrow(
                    worldPos,
                    worldPos + worldForward * 0.05f,
                    Color.white
                );
            } else {
                VRCFuryGizmoUtils.DrawArrow(
                    worldPos + worldForward * 0.1f,
                    worldPos,
                    orange
                );
            }

            if (selected) {
                VRCFuryGizmoUtils.DrawText(
                    worldPos,
                    "\n" + text,
                    Color.gray,
                    true,
                    true
                );
            }

            // So that it's actually clickable
            Gizmos.color = Color.clear;
            Gizmos.DrawSphere(worldPos, 0.04f);
        }

        [DrawGizmo(GizmoType.Selected | GizmoType.NonSelected | GizmoType.Pickable)]
        static void DrawGizmo(VRCFuryHapticSocket socket, GizmoType gizmoType) {
            var transform = socket.owner();

            var autoInfo = GetInfoFromLightsOrComponent(socket);
            var handTouchZoneSize = GetHandTouchZoneSize(socket, VRCAvatarUtils.GuessAvatarObject(socket)?.GetComponent<VRCAvatarDescriptor>());

            var (lightType, localPosition, localRotation) = autoInfo;
            var localForward = localRotation * Vector3.forward;

            if (handTouchZoneSize != null) {
                var worldStart = transform.TransformPoint(localPosition);
                var worldForward = transform.TransformDirection(localForward);
                var worldLength = handTouchZoneSize.Item1;
                var worldEnd = worldStart - worldForward * worldLength;
                var worldRadius = handTouchZoneSize.Item2;
                VRCFuryGizmoUtils.DrawCapsule(
                    worldStart,
                    worldEnd,
                    worldRadius,
                    Color.gray
                );
            }

            DrawGizmo(transform.TransformPoint(localPosition), transform.worldRotation * localRotation, lightType, GetName(socket), Selection.activeGameObject == socket.owner());
        }

        [CanBeNull]
        public static BakeResult Bake(VRCFuryHapticSocket socket, HapticContactsService hapticContactsService) {
            var transform = socket.owner();
            if (!HapticUtils.AssertValidScale(transform, "socket", shouldThrow: !socket.sendersOnly)) {
                return null;
            }

            var (lightType, localPosition, localRotation) = GetInfoFromLightsOrComponent(socket);

            var bakeRoot = GameObjects.Create("BakedSpsSocket", transform);
            bakeRoot.localPosition = localPosition;
            bakeRoot.localRotation = localRotation;

            var worldSpace = GameObjects.Create("WorldSpace", bakeRoot);
            ConstraintUtils.MakeWorldSpace(worldSpace);

            var senders = GameObjects.Create("Senders", worldSpace);

            // Senders
            {
                var rootTags = new List<string>();
                rootTags.Add(HapticUtils.TagTpsOrfRoot);
                rootTags.Add(HapticUtils.TagSpsSocketRoot);
                if (lightType != VRCFuryHapticSocket.AddLight.None && !socket.sendersOnly) {
                    switch (lightType) {
                        case VRCFuryHapticSocket.AddLight.Ring:
                            rootTags.Add(HapticUtils.TagSpsSocketIsRing);
                            break;
                        case VRCFuryHapticSocket.AddLight.RingOneWay:
                            rootTags.Add(HapticUtils.TagSpsSocketIsRing);
                            rootTags.Add(HapticUtils.TagSpsSocketIsHole);
                            break;
                        default:
                            rootTags.Add(HapticUtils.TagSpsSocketIsHole);
                            break;
                    }
                }
                hapticContactsService.AddSender(new HapticContactsService.SenderRequest() {
                    obj = senders,
                    objName = "Root",
                    radius = 0.001f,
                    tags = rootTags.ToArray(),
                    useHipAvoidance = socket.useHipAvoidance
                });
                hapticContactsService.AddSender(new HapticContactsService.SenderRequest() {
                    obj = senders,
                    pos = Vector3.forward * 0.01f,
                    objName = "Front",
                    radius = 0.001f,
                    tags = new[] { HapticUtils.TagTpsOrfFront, HapticUtils.TagSpsSocketFront },
                    useHipAvoidance = socket.useHipAvoidance
                });
            }

            VFGameObject lights = null;
            if (lightType != VRCFuryHapticSocket.AddLight.None && !socket.sendersOnly) {
                ForEachPossibleLight(transform, false, light => {
                    AvatarCleaner.RemoveComponent(light);
                });

                if (BuildTargetUtils.IsDesktop()) {
                    lights = GameObjects.Create("Lights", worldSpace);
                    var main = GameObjects.Create("Root", lights);
                    var mainLight = main.AddComponent<Light>();
                    mainLight.type = LightType.Point;
                    mainLight.color = Color.black;
                    mainLight.range =
                        (lightType == VRCFuryHapticSocket.AddLight.Ring || lightType == VRCFuryHapticSocket.AddLight.RingOneWay)
                            ? 0.4202f
                            : 0.4102f;
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
            
            if (EditorApplication.isPlaying && !socket.sendersOnly) {
                var gizmo = socket.owner().AddComponent<VRCFurySocketGizmo>();
                gizmo.pos = localPosition;
                gizmo.rot = localRotation;
                gizmo.type = lightType;
            }

            return new BakeResult {
                bakeRoot = bakeRoot,
                worldSpace = worldSpace,
                lights = lights,
                senders = senders
            };
        }

        public static Tuple<float, float> GetHandTouchZoneSize(VRCFuryHapticSocket socket, [CanBeNull] VRCAvatarDescriptor avatar) {
            var enableHandTouchZone = false;
            if (socket.enableHandTouchZone2 == VRCFuryHapticSocket.EnableTouchZone.On) {
                enableHandTouchZone = true;
            } else if (socket.enableHandTouchZone2 == VRCFuryHapticSocket.EnableTouchZone.Auto) {
                enableHandTouchZone = ShouldProbablyHaveTouchZone(socket);
            }
            if (!enableHandTouchZone) {
                return null;
            }
            var length = socket.length * (socket.unitsInMeters ? 1f : socket.owner().worldScale.z);
            if (length <= 0) {
                if (avatar == null) return null;
                length = avatar.ViewPosition.y * 0.05f;
            }
            var radius = length / 2.5f;
            return Tuple.Create(length, radius);
        }

        public enum LegacyDpsLightType {
            None,
            Hole,
            Ring,
            Front,
            Tip
        }
        public static LegacyDpsLightType GetLegacyDpsLightType(Light light) {
            if (light.range >= 0.5) return LegacyDpsLightType.None; // Outside of range
            var secondDecimal = (int)Math.Round((light.range % 0.1) * 100);
            if ((light.color.maxColorComponent > 1 && light.color.a > 0)) return LegacyDpsLightType.None; // For some reason, dps tip lights are (1,1,1,255)
            if (secondDecimal == 9 || secondDecimal == 8) return LegacyDpsLightType.Tip;
            if ((light.color.maxColorComponent > 0 && light.color.a > 0)) return LegacyDpsLightType.None; // Visible light
            if (secondDecimal == 1 || secondDecimal == 3) return LegacyDpsLightType.Hole;
            if (secondDecimal == 2 || secondDecimal == 4) return LegacyDpsLightType.Ring;
            if (secondDecimal == 5 || secondDecimal == 6) return LegacyDpsLightType.Front;
            return LegacyDpsLightType.None;
        }

        public static Tuple<VRCFuryHapticSocket.AddLight, Vector3, Quaternion> GetInfoFromLightsOrComponent(VRCFuryHapticSocket socket) {
            if (socket.addLight != VRCFuryHapticSocket.AddLight.None) {
                var type = socket.addLight;
                if (type == VRCFuryHapticSocket.AddLight.Auto) type = ShouldProbablyBeHole(socket) ? VRCFuryHapticSocket.AddLight.Hole : VRCFuryHapticSocket.AddLight.Ring;
                var position = socket.position;
                var rotation = Quaternion.Euler(socket.rotation);
                return Tuple.Create(type, position, rotation);
            }
            
            var lightInfo = GetInfoFromLights(socket.owner());
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
                var type = GetLegacyDpsLightType(light);
                if (type != LegacyDpsLightType.Hole && type != LegacyDpsLightType.Ring && type != LegacyDpsLightType.Front) return;
                act(light);
            }
            foreach (var child in obj.Children()) {
                foreach (var light in child.GetComponents<Light>()) {
                    Visit(light);
                }
            }
            if (!directOnly) {
                foreach (var light in obj.GetComponentsInSelfAndChildren<Light>()) {
                    Visit(light);
                }
            }
        }
        public static Tuple<VRCFuryHapticSocket.AddLight, Vector3, Quaternion> GetInfoFromLights(VFGameObject obj, bool directOnly = false) {
            var isRing = false;
            Light main = null;
            Light front = null;
            ForEachPossibleLight(obj, directOnly, light => {
                var type = GetLegacyDpsLightType(light);
                if (main == null) {
                    if (type == LegacyDpsLightType.Hole) {
                        main = light;
                    } else if (type == LegacyDpsLightType.Ring) {
                        main = light;
                        isRing = true;
                    }
                }
                if (front == null && type == LegacyDpsLightType.Front) {
                    front = light;
                }
            });

            if (main == null || front == null) return null;

            var position = obj.InverseTransformPoint(main.owner().worldPosition);
            var frontPosition = obj.InverseTransformPoint(front.owner().worldPosition);
            var forward = (frontPosition - position).normalized;
            var rotation = Quaternion.LookRotation(forward);

            return Tuple.Create(isRing ? VRCFuryHapticSocket.AddLight.Ring : VRCFuryHapticSocket.AddLight.Hole, position, rotation);
        }

        public static bool ShouldProbablyHaveTouchZone(VRCFuryHapticSocket socket) {
            if (ClosestBoneUtils.GetClosestHumanoidBone(socket.owner()) != HumanBodyBones.Hips) return false;

            var name = GetName(socket).ToLower();
            if (name.Contains("rubbing") || name.Contains("job")) return false;
            
            return true;
        }

        public static bool ShouldProbablyBeHole(VRCFuryHapticSocket socket) {
            var closestBone = ClosestBoneUtils.GetClosestHumanoidBone(socket.owner());
            if (closestBone == HumanBodyBones.Head || closestBone == HumanBodyBones.Jaw) return true;
            return ShouldProbablyHaveTouchZone(socket);
        }

        public static string GetName(VRCFuryHapticSocket socket) {
            var name = socket.name;
            if (!string.IsNullOrWhiteSpace(name)) return name;
            return HapticUtils.GetName(socket.owner());
        }
        
        public class BakeResult {
            public VFGameObject bakeRoot;
            public VFGameObject worldSpace;
            public VFGameObject lights;
            public VFGameObject senders;
        }
    }
}
