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

            var compatibility = VRCFuryEditorUtils.Section("Legacy Compatibility");
            var plugs = prop.FindPropertyRelative("legacyModeAvailable");
            var sockets = prop.FindPropertyRelative("legacySocketCompatibility");
            void CompatibilityCheckbox(string propertyName, string label, string penalty, SerializedProperty enabledWhen = null) {
                VisualElement Checkbox() {
                    if (enabledWhen == null || enabledWhen.boolValue) {
                        return VRCFuryEditorUtils.Prop(prop.FindPropertyRelative(propertyName), label);
                    }
                    var disabled = VRCFuryEditorUtils.Prop(null, label, fieldOverride: new Toggle());
                    disabled.SetEnabled(false);
                    return disabled;
                }
                compatibility.Add(enabledWhen == null ? Checkbox() : VRCFuryEditorUtils.RefreshOnChange(Checkbox, enabledWhen));
                var subtitle = VRCFuryEditorUtils.WrappedLabel(penalty).PaddingLeft(18);
                subtitle.style.fontSize = 11;
                subtitle.style.opacity = 0.75f;
                subtitle.style.marginBottom = 6;
                compatibility.Add(subtitle);
            }
            CompatibilityCheckbox("legacyModeAvailable", "DPS/TPS/SPS1/SPS2 Plugs",
                "Adds 2 lights per socket\nRequires Legacy Mode On");
            CompatibilityCheckbox("legacySocketCompatibility", "DPS/TPS/SPS1/SPS2 Sockets",
                "Adds 2 contact senders per plug");
            CompatibilityCheckbox("tpsDepthAnimationCompatibility", "TPS Plug Animations (Not Recommended, Rare)",
                "Adds 1 contact sender per socket\nRequires Legacy Mode On", enabledWhen: plugs);
            CompatibilityCheckbox("addDpsTipLight", "DPS Socket Animations (Not Recommended, Rare)",
                "Adds 1 light per plug\nRequires Legacy Mode On", enabledWhen: sockets);
            var dps = prop.FindPropertyRelative("addDpsTipLight");
            compatibility.Add(VRCFuryEditorUtils.RefreshOnChange(() => {
                var controls = new VisualElement();
                if (!plugs.boolValue && !(sockets.boolValue && dps.boolValue)) return controls;
                controls.Add(VRCFuryEditorUtils.WrappedLabel("Legacy Mode Menu").Bold());
                controls.Add(VRCFuryEditorUtils.WrappedLabel("Only one socket can be active at a time"));
                controls.Add(VRCFuryEditorUtils.Prop(prop.FindPropertyRelative("legacyModeEnabledOnAvatarLoad"), "Default On"));
                controls.Add(VRCFuryEditorUtils.Prop(prop.FindPropertyRelative("legacyModeSaved"), "Save Setting Between Worlds"));
                return controls;
            }, plugs, sockets, dps));
            c.Add(compatibility);
            var autoAvailable = prop.FindPropertyRelative("autoModeAvailable");
            c.Add(VRCFuryEditorUtils.Prop(autoAvailable, "Include Auto Mode in Avatar"));
            c.Add(VRCFuryEditorUtils.RefreshOnChange(() => {
                if (!autoAvailable.boolValue) return new VisualElement();
                var auto = VRCFuryEditorUtils.Section("Auto Mode", "Automatically activates socket that touches a plug.");
                auto.Add(VRCFuryEditorUtils.Prop(prop.FindPropertyRelative("autoModeDefaultOn"), "Default On"));
                auto.Add(VRCFuryEditorUtils.Prop(prop.FindPropertyRelative("autoModeSaved"), "Save Setting Between Worlds"));
                return auto;
            }, autoAvailable));
            return c;
        }
    }
}
