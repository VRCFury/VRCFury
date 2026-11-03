using UnityEditor;
using UnityEngine.UIElements;
using VF.Feature.Base;
using VF.Inspector;
using VF.Model.Feature;
using VF.Utils;

namespace VF.Feature {
    [FeatureTitle("SPS Options")]
    [FeatureOnlyOneAllowed]
    [FeatureRootOnly]
    internal class SpsOptionsBuilder : FeatureBuilder<SpsOptions> {

        [FeatureEditor]
        public static VisualElement Editor(SerializedProperty prop, VFGameObject avatarObject) {
            var c = new VisualElement();
            var menu = VRCFuryEditorUtils.Section("SPS Menu");
            menu.Add(MoveMenuItemBuilder.SelectButton(
                avatarObject,
                true,
                prop.FindPropertyRelative("menuPath"),
                append: () => "SPS",
                label: "Path Override",
                selectLabel: "Select",
                placeholder: "SPS"
            ));
            menu.Add(VRCFuryEditorUtils.Prop(prop.FindPropertyRelative("menuIcon"), "Icon Override"));
            menu.Add(VRCFuryEditorUtils.Prop(prop.FindPropertyRelative("saveSockets"), "Save Active Sockets Between Worlds"));
            c.Add(menu);

            void Mode(string title, string subtitle, string availableName, string defaultName, string savedName) {
                var section = VRCFuryEditorUtils.Section(title, subtitle);
                var available = prop.FindPropertyRelative(availableName);
                section.Add(VRCFuryEditorUtils.Prop(available, "Include in Avatar"));
                section.Add(VRCFuryEditorUtils.RefreshOnChange(() => {
                    var settings = new VisualElement();
                    settings.Add(VRCFuryEditorUtils.Prop(prop.FindPropertyRelative(defaultName), "Default On"));
                    settings.Add(VRCFuryEditorUtils.Prop(prop.FindPropertyRelative(savedName), "Save Setting Between Worlds"));
                    settings.SetEnabled(available.boolValue);
                    return settings;
                }, available));
                c.Add(section);
            }
            Mode(
                "Legacy Mode",
                "Enables compatibility with DPS/TPS/SPS1/SPS2 plugs on other avatars.\nAdds 2 lights per socket.\nLimit 1 socket at a time when enabled.",
                "legacyModeAvailable",
                "legacyModeEnabledOnAvatarLoad",
                "legacyModeSaved"
            );
            Mode(
                "Auto Mode",
                "Automatically activates socket that touches a plug.",
                "autoModeAvailable",
                "autoModeDefaultOn",
                "autoModeSaved"
            );
            var tipLight = VRCFuryEditorUtils.Section("DPS Socket Animation Compatibility",
                "These are very rare, you should usually not use this.\nAdds 1 light per plug.");
            var legacyAvailable = prop.FindPropertyRelative("legacyModeAvailable");
            VisualElement CompatibilityCheckbox(string propertyName) => VRCFuryEditorUtils.RefreshOnChange(() => {
                var checkbox = VRCFuryEditorUtils.Prop(prop.FindPropertyRelative(propertyName), "Include in Avatar (Not Recommended)");
                checkbox.SetEnabled(legacyAvailable.boolValue);
                return checkbox;
            }, legacyAvailable);
            tipLight.Add(CompatibilityCheckbox("addDpsTipLight"));
            c.Add(tipLight);
            var tpsDepth = VRCFuryEditorUtils.Section("TPS Plug Animation Compatibility",
                "These are very rare, you should usually not use this.\nAdds 1 contact sender per socket.");
            tpsDepth.Add(CompatibilityCheckbox("tpsDepthAnimationCompatibility"));
            c.Add(tpsDepth);
            return c;
        }
    }
}
