using UnityEditor;
using UnityEngine.UIElements;
using Toggle = UnityEngine.UIElements.Toggle;
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

            VisualElement Mode(string title, string subtitle, string availableName, string defaultName, string savedName) {
                var section = VRCFuryEditorUtils.Section(title, subtitle);
                var available = prop.FindPropertyRelative(availableName);
                c.Add(VRCFuryEditorUtils.Prop(available, $"Include {title} in Avatar"));
                section.Add(VRCFuryEditorUtils.Prop(prop.FindPropertyRelative(defaultName), "Default On"));
                section.Add(VRCFuryEditorUtils.Prop(prop.FindPropertyRelative(savedName), "Save Setting Between Worlds"));
                c.Add(VRCFuryEditorUtils.RefreshOnChange(() =>
                    available.boolValue ? section : new VisualElement(), available));
                return section;
            }
            var legacy = Mode(
                "Legacy Mode",
                null,
                "legacyModeAvailable",
                "legacyModeEnabledOnAvatarLoad",
                "legacyModeSaved"
            );
            legacy.Add(VRCFuryEditorUtils.WrappedLabel("Adds compatibility with:").Bold());
            void CompatibilityCheckbox(string propertyName, string label, string penalty) {
                var checkbox = propertyName == null
                    ? VRCFuryEditorUtils.Prop(null, label, fieldOverride: new Toggle { value = true })
                    : VRCFuryEditorUtils.Prop(prop.FindPropertyRelative(propertyName), label);
                if (propertyName == null) checkbox.SetEnabled(false);
                legacy.Add(checkbox);
                var subtitle = VRCFuryEditorUtils.WrappedLabel(penalty).PaddingLeft(18);
                subtitle.style.fontSize = 11;
                subtitle.style.opacity = 0.75f;
                subtitle.style.marginBottom = 6;
                legacy.Add(subtitle);
            }
            CompatibilityCheckbox(null, "DPS/TPS/SPS1/SPS2 Plugs",
                "Adds 2 lights per socket\nOnly one socket can be active at a time");
            CompatibilityCheckbox("tpsDepthAnimationCompatibility", "TPS Plug Animations (Not Recommended, Rare)",
                "Adds 1 contact sender per socket");
            CompatibilityCheckbox("addDpsTipLight", "DPS Socket Animations (Not Recommended, Rare)",
                "Adds 1 light per plug");
            Mode(
                "Auto Mode",
                "Automatically activates socket that touches a plug.",
                "autoModeAvailable",
                "autoModeDefaultOn",
                "autoModeSaved"
            );
            return c;
        }
    }
}
