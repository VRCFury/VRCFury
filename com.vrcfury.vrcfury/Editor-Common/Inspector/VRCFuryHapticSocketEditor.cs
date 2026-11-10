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
using VF.Model;
using VF.Utils;
using SpsOptions = VF.Model.Feature.SpsOptions;

namespace VF.Inspector {
    [CustomEditor(typeof(VRCFuryHapticSocket), true)]
    internal class VRCFuryHapticSocketEditor : VRCFuryComponentEditor<VRCFuryHapticSocket> {
        internal const float GizmoRadiusOffset = 0.04f;
        internal const float LegacyRadiusOffset = 0.03f;

        private void OnSceneGUI() {
            if (!(target is VRCFuryHapticSocket socket)) return;
            VRCFuryHapticSocketGizmo.DrawEditableStops(socket.guidedPathStops
                .Where(stop => stop != null).Select(stop => stop.transform));
            if (PrefabUtility.IsPartOfPrefabInstance(socket)) return;
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

        private static SerializedProperty AddGuidedPath(SerializedProperty listProp) {
            if (listProp.arraySize >= SpsTags.MaxUserPathStops) return null;
            return VRCFuryEditorUtils.AddToList(listProp);
        }

        private static VisualElement GuidedPathList(SerializedProperty listProp) {
            return VRCFuryEditorUtils.RefreshOnChange(() => {
                var container = new VisualElement();

                string FormatSlot(int slot) {
                    if (slot == -1) return "Root";
                    return $"Stop {slot+1}";
                }

                for (var i = 0; i < Math.Min(listProp.arraySize, SpsTags.MaxUserPathStops); i++) {
                    var index = i;
                    var row = new VisualElement();
                    var item = listProp.GetArrayElementAtIndex(index);
                    row.Add(VRCFuryEditorUtils.Prop(item.FindPropertyRelative("shrink"), $"Collapse plug between {FormatSlot(index-1)} and {FormatSlot(index)}"));

                    row.Add(VRCFuryEditorUtils.Prop(item.FindPropertyRelative("transform"), FormatSlot(index)));

                    row.Add(new Button(() => {
                        listProp.DeleteArrayElementAtIndex(index);
                        listProp.serializedObject.ApplyModifiedProperties();
                    }) {
                        text = "Remove"
                    });
                    container.Add(row);
                }
                if (listProp.arraySize < SpsTags.MaxUserPathStops) {
                    container.Add(new Button(() => AddGuidedPath(listProp)) {
                        text = "Add Stop to Path"
                    });
                }

                return container;
            }, listProp);
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

                var section = VRCFuryEditorUtils.Section("Deformation (Super Plug Shader)", "SPS3 plugs will deform toward this socket\nCheck out vrcfury.com/sps for details");
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

                var legacySupport = VRCFuryEditorUtils.Section(
                    "Legacy Compatibility",
                    "DPS/TPS/SPS1/SPS2 plugs will deform toward this socket\n"
                    + "when you have Legacy enabled in the SPS menu in-game"
                );
                legacySupport.Add(VRCFuryEditorUtils.Debug(refreshElement: () => {
                    if (target == null) return null;
                    var options = target.owner().GetComponentsInUploadRoot<VRCFury>()
                        .SelectMany(component => component.GetAllFeatures())
                        .OfType<SpsOptions>()
                        .FirstOrDefault();
                    return options != null && !options.legacyModeAvailable
                        ? VRCFuryEditorUtils.Warn("Legacy plug compatibility is disabled in SPS Options. These settings will have no effect. You can enable 'DPS/TPS/SPS1/SPS2 Plugs' in the SPS Options component if you want to remain compatible with them.")
                        : VRCFuryEditorUtils.Info("You can disable legacy plug compatibility and remove its lights by adding an SPS Options component on the avatar root.");
                }));
                var overrideLegacySocketType = serializedObject.FindProperty("overrideLegacySocketType");
                legacySupport.Add(VRCFuryEditorUtils.BetterProp(
                    overrideLegacySocketType,
                    "Override Legacy Type"
                ));
                legacySupport.Add(VRCFuryEditorUtils.RefreshOnChange(() => {
                    if (!overrideLegacySocketType.boolValue) return new VisualElement();
                    var legacyType = serializedObject.FindProperty("legacySocketType");
                    // Previously serialized one-way rings behave identically to rings.
                    var field = new PopupField<string>(new List<string> { "Hole", "Ring" }, legacyType.intValue == 0 ? 0 : 1);
                    field.TrackPropertyValue(legacyType, p => field.SetValueWithoutNotify(p.intValue == 0 ? "Hole" : "Ring"));
                    field.RegisterValueChangedCallback(cb => {
                        legacyType.intValue = cb.newValue == "Hole" ? 0 : 1;
                        serializedObject.ApplyModifiedProperties();
                    });
                    return VRCFuryEditorUtils.BetterProp(
                        legacyType,
                        "Legacy Type",
                        fieldOverride: field
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
                        serializedObject.FindProperty("legacyOffsetLocal"),
                        "Legacy Entry Offset"
                    );
                }, overrideLegacyOffset));
                output.Add(legacySupport);


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
                    GetMenuName,
                    isOgbId: false
                ));
                toggles.Add(VRCFuryEditorUtils.BetterProp(serializedObject.FindProperty("enableAuto"), "Include in Auto selection?",
                    tooltip: "If checked, this socket will be eligible to be automatically enabled during Auto Mode."));
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

            var radiusActions = serializedObject.FindProperty("plugEntryRadiusActions");
            container.Add(VRCFuryEditorUtils.CheckboxList(
                radiusActions,
                "Enable Plug Entry Radius Animations",
                "Animate based on the plug's radius at the socket entrance",
                "Plug Entry Radius Animations",
                VRCFuryEditorUtils.List(radiusActions, () => VRCFuryEditorUtils.AddToList(radiusActions, action => {
                    action.FindPropertyRelative("range").vector2Value = new Vector2(0, 0.1f);
                }))
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
            }));

            container.Add(VRCFuryHapticPlugEditor.SpsChannelsEditor(target.owner(), false, serializedObject.FindProperty("channelSettings")));
            
            var adv = new Foldout {
                text = "Deprecated Options",
                value = false,
            };
            container.Add(adv);

            var enablePlugLengthParameterProp = serializedObject.FindProperty("enablePlugLengthParameter");
            var enablePlugWidthParameterProp = serializedObject.FindProperty("enablePlugWidthParameter");
            adv.Add(VRCFuryEditorUtils.BetterProp(enablePlugLengthParameterProp, "Plug Length (meters) (Deprecated, use Inject Parameters on Full Controller)"));
            adv.Add(VRCFuryEditorUtils.BetterProp(serializedObject.FindProperty("plugLengthParameterName")));
            adv.Add(VRCFuryEditorUtils.BetterProp(enablePlugWidthParameterProp, "Plug Radius (meters) (Deprecated, use Inject Parameters on Full Controller)"));
            adv.Add(VRCFuryEditorUtils.BetterProp(serializedObject.FindProperty("plugWidthParameterName")));
            adv.Add(VRCFuryEditorUtils.BetterProp(serializedObject.FindProperty("position"), "Position (Deprecated, move the object instead)"));
            adv.Add(VRCFuryEditorUtils.BetterProp(serializedObject.FindProperty("rotation"), "Rotation (Deprecated, move the object instead)"));

            return container;
        }
        
        [CustomPropertyDrawer(typeof(VRCFuryHapticSocket.DepthActionNew))]
        public class DepthActionDrawer : PropertyDrawer {
            protected virtual bool IsRadius => false;
            public override VisualElement CreatePropertyGUI(SerializedProperty prop) {
                var c = new VisualElement();
                c.Add(VRCFuryEditorUtils.BetterProp(prop.FindPropertyRelative("actionSet")));
                var units = prop.FindPropertyRelative("units");
                c.Add(VRCFuryEditorUtils.RefreshOnChange(() =>
                    VRCFuryEditorUtils.BetterProp(
                        null,
                        IsRadius ? "Plug entry radius" : "Activation distance",
                        tooltip: IsRadius ? "The animation is off at the minimum radius and fully on at the maximum radius."
                            : "Animation will begin at the far distance, and 'max' at the near distance. If you provide a static action or clip," +
                                 " the animation will be fully 'off' at the far distance, and fully 'on' at the near distance.",
                        fieldOverride: new DepthActionSlider(prop.FindPropertyRelative("range"), (VRCFuryHapticSocket.DepthActionUnits)units.enumValueIndex, radius: IsRadius)
                    )
                , units));
                var unitNames = units.enumDisplayNames;
                var unitField = new PopupField<string>(IsRadius
                    ? new List<string> { "Local", "Meters" } : unitNames.ToList(), 0);
                void UpdateUnits(SerializedProperty value) => unitField.SetValueWithoutNotify(unitNames[value.enumValueIndex]);
                UpdateUnits(units);
                unitField.TrackPropertyValue(units, UpdateUnits);
                unitField.RegisterValueChangedCallback(change => {
                    var oldUnits = (VRCFuryHapticSocket.DepthActionUnits)units.enumValueIndex;
                    var newUnits = (VRCFuryHapticSocket.DepthActionUnits)Array.IndexOf(unitNames, change.newValue);
                    if (prop.serializedObject.targetObject is UnityEngine.Component owner) {
                        var scale = owner.transform.lossyScale.x;
                        var range = prop.FindPropertyRelative("range");
                        if (oldUnits == VRCFuryHapticSocket.DepthActionUnits.Local && newUnits == VRCFuryHapticSocket.DepthActionUnits.Meters) {
                            range.vector2Value *= scale;
                        } else if (oldUnits == VRCFuryHapticSocket.DepthActionUnits.Meters && newUnits == VRCFuryHapticSocket.DepthActionUnits.Local && scale != 0) {
                            range.vector2Value /= scale;
                        }
                    }
                    units.enumValueIndex = (int)newUnits;
                    units.serializedObject.ApplyModifiedProperties();
                });
                c.Add(VRCFuryEditorUtils.BetterProp(null, "Range Units", fieldOverride: unitField));
                c.Add(VRCFuryEditorUtils.RefreshOnChange(() =>
                    (VRCFuryHapticSocket.DepthActionUnits)units.enumValueIndex == VRCFuryHapticSocket.DepthActionUnits.Meters
                        ? SpsEditorUtils.MetersWarning()
                        : new VisualElement(), units));
                c.Add(VRCFuryEditorUtils.CheckboxPair(prop.FindPropertyRelative("enableSelf"), "Allow Self",
                    prop.FindPropertyRelative("enableOthers"), "Allow Others"));
                if (prop.serializedObject.targetObject is VRCFuryHapticPlug) {
                    c.Add(VRCFuryEditorUtils.CheckboxPair(prop.FindPropertyRelative("allowHole"), "Allow Hole",
                        prop.FindPropertyRelative("allowRing"), "Allow Ring"));
                }
                c.Add(VRCFuryEditorUtils.BetterProp(prop.FindPropertyRelative("smoothingSeconds"), "Smoothing Seconds", tooltip: "It will take approximately this many seconds to smoothly blend to the target value. Beware that this smoothing is based on framerate, so higher FPS will result in faster smoothing."));
                if (!IsRadius) c.Add(VRCFuryEditorUtils.BetterProp(prop.FindPropertyRelative("reverseClip"), "Reverse clip (Deprecated, DO NOT USE)"));
                if (!IsRadius && prop.serializedObject.targetObject is VRCFuryHapticSocket socket) {
                    var radius = prop.FindPropertyRelative("plugRadiusForFullWeight");
                    float WorldScale() => Mathf.Max(Mathf.Abs(socket.transform.lossyScale.x), 0.000001f);
                    var toggle = new Toggle { name = "multiplyPlugRadius" };
                    var field = new FloatField { name = "plugRadiusForFullWeight", isDelayed = true };
                    field.style.width = 80;
                    field.style.flexShrink = 0;
                    var radiusControls = new VisualElement { name = "plugRadiusControls" };
                    var row = new VisualElement().Row();
                    row.Add(field);
                    var meters = VRCFuryEditorUtils.WrappedLabel("");
                    meters.name = "plugRadiusMeters";
                    meters.style.marginLeft = 5;
                    row.Add(meters);
                    radiusControls.Add(VRCFuryEditorUtils.BetterProp(null, "Local plug radius for max (1.0) weight:", fieldOverride: row));
                    void UpdateMeters() => meters.text = $"{radius.floatValue * WorldScale():G4} m";
                    void UpdateRadius() {
                        field.SetValueWithoutNotify(radius.floatValue);
                        toggle.SetValueWithoutNotify(radius.floatValue > 0);
                        radiusControls.style.display = radius.floatValue > 0 ? DisplayStyle.Flex : DisplayStyle.None;
                        UpdateMeters();
                    }
                    field.RegisterValueChangedCallback(evt => {
                        radius.floatValue = Mathf.Max(0, evt.newValue);
                        radius.serializedObject.ApplyModifiedProperties();
                        UpdateRadius();
                    });
                    toggle.RegisterValueChangedCallback(evt => {
                        radius.floatValue = evt.newValue ? 0.25f / WorldScale() : 0;
                        radius.serializedObject.ApplyModifiedProperties();
                        UpdateRadius();
                    });
                    c.Add(VRCFuryEditorUtils.BetterProp(null, "Weight by local plug radius", fieldOverride: toggle,
                        tooltip: "Scales the animation weight using the plug's average radius in socket-local units. Full weight is reached at the configured radius."));
                    c.Add(radiusControls);
                    toggle.TrackProperty(radius, _ => UpdateRadius());
                    meters.schedule.Execute(UpdateMeters).Every(100);
                    UpdateRadius();
                }
                return c;
            }
        }

        [CustomPropertyDrawer(typeof(VRCFuryHapticSocket.PlugEntryRadiusAction))]
        public class PlugEntryRadiusActionDrawer : DepthActionDrawer {
            protected override bool IsRadius => true;
        }



        internal static VRCFuryHapticSocket.AddLight GetGuidedPathTerminalType(VRCFuryHapticSocket.AddLight lightType) {
            return lightType == VRCFuryHapticSocket.AddLight.Hole
                ? VRCFuryHapticSocket.AddLight.Hole
                : VRCFuryHapticSocket.AddLight.RingOneWay;
        }

        internal static VRCFuryHapticSocket.AddLight GetLegacyLightType(VRCFuryHapticSocket socket, VRCFuryHapticSocket.AddLight lightType) {
            if (socket.overrideLegacySocketType) {
                return socket.legacySocketType switch {
                    VRCFuryHapticSocket.LegacySocketType.Hole => VRCFuryHapticSocket.AddLight.Hole,
                    _ => VRCFuryHapticSocket.AddLight.Ring
                };
            }
            return socket.guidedPathStops.Any(stop => stop != null && stop.transform != null)
                ? VRCFuryHapticSocket.AddLight.Hole
                : lightType;
        }

        public static Tuple<VRCFuryHapticSocket.AddLight, TransformData> GetSocketTransformLocal(
            VRCFuryHapticSocket socket
        ) => Tuple.Create(socket.addLight, new TransformData(socket.position, Quaternion.Euler(socket.rotation)));

        public class BakeResult {
            public SpsChannelContacts channelContacts;
            public SelfAndOthersReceiverService.Receivers plugReceivers;
            public VFGameObject bakeRoot;
            public VFGameObject[] pathRoots;
            public VFGameObject legacyFrontSender;
            public VFGameObject worldSpace;
            public VFGameObject legacyLights;
        }

    }
}
