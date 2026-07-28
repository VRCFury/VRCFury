using System;
using System.Collections.Generic;
using System.Linq;
using JetBrains.Annotations;
using UnityEditor;
using UnityEditor.UIElements;
using UnityEngine;
using UnityEngine.UIElements;
using VF.Builder.Haptics;
using VF.Component;
using VF.Injector;
using VF.Utils;

namespace VF.Inspector {
    [CustomEditor(typeof(VRCFuryHapticSocket), true)]
    internal class VRCFuryHapticSocketEditor : VRCFuryComponentEditor<VRCFuryHapticSocket> {
        internal const int SpsTagCount = 2;
        private const int GuidedPathCount = 3;

        private void OnSceneGUI() {
            if (!(target is VRCFuryHapticSocket socket)) return;
            VRCFuryHapticSocketGizmo.DrawEditableTangents(socket);
            VRCFuryHapticSocketGizmo.DrawEditableLegacyOffset(socket);
        }

        internal static string GetMenuName(VRCFuryHapticSocket socket) {
            return HapticUtils.GetPreferredId(
                socket,
                s => s.name,
                s => HapticUtils.GetFallbackId(s.owner())
            );
        }

        private static string GetOscId(VRCFuryHapticSocket socket) {
            return HapticUtils.GetPreferredId(
                socket,
                s => s.oscId,
                _ => GetMenuName(socket)
            );
        }

        private static SerializedProperty AddSpsTag(SerializedProperty listProp) {
            if (listProp.arraySize >= SpsTagCount) return null;
            var index = listProp.arraySize;
            listProp.InsertArrayElementAtIndex(index);
            var item = listProp.GetArrayElementAtIndex(index);
            item.stringValue = "";
            listProp.serializedObject.ApplyModifiedProperties();
            return item;
        }

        private static VisualElement SpsTagList(SerializedProperty listProp) {
            return VRCFuryEditorUtils.RefreshOnChange(() => {
                var container = new VisualElement();
                for (var i = 0; i < Math.Min(listProp.arraySize, SpsTagCount); i++) {
                    var index = i;
                    var row = new VisualElement();
                    row.Add(VRCFuryHapticPlugEditor.SpsTagProp(listProp.GetArrayElementAtIndex(index), $"Tag #{index + 1}"));
                    row.Add(new Button(() => {
                        listProp.DeleteArrayElementAtIndex(index);
                        listProp.serializedObject.ApplyModifiedProperties();
                    }) {
                        text = "Remove"
                    });
                    container.Add(row);
                }
                if (listProp.arraySize < SpsTagCount) {
                    container.Add(new Button(() => AddSpsTag(listProp)) {
                        text = "Add Tag"
                    });
                }
                return container;
            }, listProp);
        }

        private static SerializedProperty AddGuidedPath(SerializedProperty listProp) {
            if (listProp.arraySize >= GuidedPathCount) return null;
            return VRCFuryEditorUtils.AddToList(listProp);
        }

        private static VisualElement GuidedPathList(SerializedProperty listProp) {
            var refreshProps = new List<SerializedProperty> { listProp };
            for (var i = 0; i < Math.Min(listProp.arraySize, GuidedPathCount); i++) {
                var item = listProp.GetArrayElementAtIndex(i);
                refreshProps.Add(item.FindPropertyRelative("customizeTangentOut"));
                refreshProps.Add(item.FindPropertyRelative("customizeTangentIn"));
            }
            return VRCFuryEditorUtils.RefreshOnChange(() => {
                var container = new VisualElement();

                string FormatSlot(int slot) {
                    if (slot == -1) return "Root";
                    return $"Stop {slot+1}";
                }

                for (var i = 0; i < Math.Min(listProp.arraySize, GuidedPathCount); i++) {
                    var index = i;
                    var row = new VisualElement();
                    var item = listProp.GetArrayElementAtIndex(index);
                    var customizeTangentOut = item.FindPropertyRelative("customizeTangentOut");
                    var customizeTangentIn = item.FindPropertyRelative("customizeTangentIn");

                    row.Add(VRCFuryEditorUtils.Prop(customizeTangentOut, $"Customize Tangent exiting {FormatSlot(index-1)}"));
                    if (customizeTangentOut.boolValue) {
                        row.Add(VRCFuryEditorUtils.Prop(item.FindPropertyRelative("tangentOut")));
                    }
                    row.Add(VRCFuryEditorUtils.Prop(item.FindPropertyRelative("shrink"), $"Collapse plug between {FormatSlot(index-1)} and {FormatSlot(index)}"));
                    row.Add(VRCFuryEditorUtils.Prop(customizeTangentIn, $"Customize Tangent entering {FormatSlot(index)}"));
                    if (customizeTangentIn.boolValue) {
                        row.Add(VRCFuryEditorUtils.Prop(item.FindPropertyRelative("tangentIn")));
                    }

                    row.Add(VRCFuryEditorUtils.Prop(item.FindPropertyRelative("transform"), FormatSlot(index)));

                    row.Add(new Button(() => {
                        listProp.DeleteArrayElementAtIndex(index);
                        listProp.serializedObject.ApplyModifiedProperties();
                    }) {
                        text = "Remove"
                    });
                    container.Add(row);
                }
                if (listProp.arraySize < GuidedPathCount) {
                    container.Add(new Button(() => AddGuidedPath(listProp)) {
                        text = "Add Stop to Path"
                    });
                }

                return container;
            }, refreshProps.ToArray());
        }

        protected override VisualElement CreateEditor(SerializedObject serializedObject, VRCFuryHapticSocket target) {
            var container = new VisualElement();
            
            container.Add(VRCFuryHapticPlugEditor.ConstraintWarning(target, true));
            
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
                var output = new VisualElement();

                var section = VRCFuryEditorUtils.Section("Deformation (Super Plug Shader)", "SPS2 plugs will deform toward this socket\nCheck out vrcfury.com/sps for details");
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
                section.Add(VRCFuryEditorUtils.BetterProp(
                    serializedObject.FindProperty("useRadiusOffset"),
                    "Radius Offset",
                    tooltip: "Offsets SPS targeting in the socket up direction by the resolver radius. Legacy lights are also moved upward slightly for TPS/DPS compatibility."
                ));
                section.Add(VRCFuryEditorUtils.BetterProp(
                    serializedObject.FindProperty("guidedPathStops"),
                    "Guided Path",
                    fieldOverride: GuidedPathList(serializedObject.FindProperty("guidedPathStops")),
                    tooltip: "If provided, the plug will be guided through these transforms after passing through the socket. If the socket is a hole, the collapse will occur at the last transform in the path."
                ));
                output.Add(section);

                var enableBackwardCompatibility = serializedObject.FindProperty("useLights");
                output.Add(VRCFuryEditorUtils.BetterProp(enableBackwardCompatibility, "Enable Legacy Compatibility"));
                output.Add(VRCFuryEditorUtils.RefreshOnChange(() => {
                    if (!enableBackwardCompatibility.boolValue) return new VisualElement();
                    var legacySupport = VRCFuryEditorUtils.Section("Legacy Compatibility",
                        "SPS1/DPS/TPS plugs will deform toward this socket\nUses Lights");
                    var overrideLegacySocketType = serializedObject.FindProperty("overrideLegacySocketType");
                    legacySupport.Add(VRCFuryEditorUtils.BetterProp(
                        overrideLegacySocketType,
                        "Override Legacy Type"
                    ));
                    legacySupport.Add(VRCFuryEditorUtils.RefreshOnChange(() => {
                        if (!overrideLegacySocketType.boolValue) return new VisualElement();
                        return VRCFuryEditorUtils.BetterProp(
                            serializedObject.FindProperty("legacySocketType"),
                            "Legacy Type"
                        );
                    }, overrideLegacySocketType));
                    var overrideLegacyOffset = serializedObject.FindProperty("overrideLegacyOffset");
                    legacySupport.Add(VRCFuryEditorUtils.BetterProp(
                        overrideLegacyOffset,
                        "Override Legacy Offset"
                    ));
                    legacySupport.Add(VRCFuryEditorUtils.RefreshOnChange(() => {
                        if (!overrideLegacyOffset.boolValue) return new VisualElement();
                        return VRCFuryEditorUtils.BetterProp(
                            serializedObject.FindProperty("legacyOffset"),
                            "Legacy Entry Offset"
                        );
                    }, overrideLegacyOffset));
                    return legacySupport;
                }, enableBackwardCompatibility));

                return output;
            }, addLightProp));

            var addMenuItemProp = serializedObject.FindProperty("addMenuItem");
            container.Add(VRCFuryEditorUtils.BetterProp(addMenuItemProp, "Enable Menu Toggle"));
            container.Add(VRCFuryEditorUtils.RefreshOnChange(() => {
                if (!addMenuItemProp.boolValue) return new VisualElement();
                var toggles = VRCFuryEditorUtils.Section("Menu Toggle", "A menu item will be created for this socket");
                toggles.Add(SpsEditorUtils.AutoHapticIdProp(
                    serializedObject.FindProperty("name"),
                    "Name in menu",
                    target,
                    target.owner(),
                    avatar => avatar.GetComponentsInSelfAndChildren<VRCFuryHapticSocket>(),
                    GetMenuName
                ));
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

            container.Add(VRCFuryHapticPlugEditor.GetOgbHapticsSection(haptics => {
                haptics.Add(SpsEditorUtils.AutoHapticIdProp(
                    serializedObject.FindProperty("oscId"),
                    "ID sent to OGB",
                    target,
                    target.owner(),
                    avatar => avatar.GetComponentsInSelfAndChildren<VRCFuryHapticSocket>(),
                    GetOscId
                ));
                haptics.Add(VRCFuryEditorUtils.BetterProp(
                    serializedObject.FindProperty("enableHandTouchZone2"),
                    "Enable hand touch zone? (Auto will add only if child of Hips)"
                ));
                haptics.Add(VRCFuryEditorUtils.BetterProp(
                    serializedObject.FindProperty("length"),
                    "Hand touch zone depth override in meters:\nNote, this zone is only used for hand touches, not plug interaction."
                ));
            }));

            var tags = VRCFuryEditorUtils.Section("Tags", "Filter which plugs can target this socket");
            tags.Add(SpsTagList(serializedObject.FindProperty("tags")));
            var useSharedTag = serializedObject.FindProperty("useSharedTag");
            tags.Add(VRCFuryEditorUtils.BetterProp(useSharedTag, "'Global' SPS2 Tag",
                tooltip: "Allows all SPS2 plugs (which are configured using the defaults) to target this socket."));
            tags.Add(VRCFuryEditorUtils.RefreshOnChange(() => {
                if (!useSharedTag.boolValue) {
                    return VRCFuryEditorUtils.Warn("This socket does not have the global SPS2 tag, so most plugs will not target it.");
                }
                var autoTags = VRCFuryPerFrameInjector.GetPerFrameInjector(target.owner())
                    .GetService<SpsConfigurer>()
                    .GetAutoSocketTagNames(target);
                if (autoTags.Count > 0) {
                    return VRCFuryEditorUtils.Info("Additional automatic tags included: " + autoTags.Join(", "));
                }
                return new VisualElement();
            }, useSharedTag));
            container.Add(tags);
            
            var adv = new Foldout {
                text = "Advanced",
                value = false,
            };
            container.Add(adv);
            
            var plugParams = VRCFuryEditorUtils.Section("Global Plug Parameters");
            adv.Add(plugParams);
            var enablePlugLengthParameterProp = serializedObject.FindProperty("enablePlugLengthParameter");
            var enablePlugWidthParameterProp = serializedObject.FindProperty("enablePlugWidthParameter");
            plugParams.Add(VRCFuryEditorUtils.BetterProp(enablePlugLengthParameterProp, "Plug Length (meters) (Deprecated, use Inject Parameters on Full Controller)"));
            plugParams.Add(VRCFuryEditorUtils.BetterProp(serializedObject.FindProperty("plugLengthParameterName")));
            plugParams.Add(VRCFuryEditorUtils.BetterProp(enablePlugWidthParameterProp, "Plug Radius (meters) (Deprecated, use Inject Parameters on Full Controller)"));
            plugParams.Add(VRCFuryEditorUtils.BetterProp(serializedObject.FindProperty("plugWidthParameterName")));
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

        internal static VRCFuryHapticSocket.AddLight GetGuidedPathTerminalType(VRCFuryHapticSocket.AddLight lightType) {
            return lightType == VRCFuryHapticSocket.AddLight.Hole
                ? VRCFuryHapticSocket.AddLight.Hole
                : VRCFuryHapticSocket.AddLight.RingOneWay;
        }

        internal static VRCFuryHapticSocket.AddLight GetLegacyLightType(VRCFuryHapticSocket socket, VRCFuryHapticSocket.AddLight lightType) {
            if (socket.overrideLegacySocketType) {
                return socket.legacySocketType == VRCFuryHapticSocket.LegacySocketType.Hole
                    ? VRCFuryHapticSocket.AddLight.Hole
                    : VRCFuryHapticSocket.AddLight.Ring;
            }
            return socket.guidedPathStops.Any(stop => stop != null && stop.transform != null)
                ? VRCFuryHapticSocket.AddLight.Hole
                : lightType;
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

        public class BakeResult {
            public VFGameObject bakeRoot;
            public VFGameObject oneSpace;
            public VFGameObject worldSpace;
            public List<VFGameObject> screenMarkers;
            public List<ScreenMarkerResult> screenMarkerResults;
            public VFGameObject lights;
            public VFGameObject senders;
        }

        public class ScreenMarkerResult {
            public VFGameObject obj;
            public MeshRenderer renderer;
            public List<SpsConfigurer.MaterialProperty> materialProperties;
        }
    }
}
