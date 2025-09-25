using JetBrains.Annotations;
using UnityEditor;
using UnityEngine.UIElements;
using VF.Utils;

namespace VF.Hooks.VrcsdkFixes {
#if VRC_NEW_PUBLIC_SDK && UNITY_2022_1_OR_NEWER
    internal static class DefaultToBuilderTabHook {
        private static double whenSdkPanelLoaded;
        private static EditorWindow sdkPanel = null;

        [InitializeOnLoadMethod]
        private static void Init() {
            VRCSdkControlPanel.OnSdkPanelEnable += (panel, e) => {
                sdkPanel = panel as EditorWindow;
                whenSdkPanelLoaded = 0;
            };
            Scheduler.Schedule(() => {
                var builderTab = GetBuilderButton();
                if (builderTab == null) return;
                var now = EditorApplication.timeSinceStartup;
                if (whenSdkPanelLoaded == 0) whenSdkPanelLoaded = now;
                var timeSincePanelLoaded = now - whenSdkPanelLoaded;
                if (timeSincePanelLoaded > 5) {
                    sdkPanel = null;
                    return;
                }

                if (!builderTab.enabledInHierarchy) return;
                sdkPanel = null;
                using (var ev = new NavigationSubmitEvent { target = builderTab }) {
                    builderTab.SendEvent(ev);
                }
            }, 200);
        }

        [CanBeNull]
        private static Button GetBuilderButton() {
            return sdkPanel.NullSafe()?.rootVisualElement?.Q<Button>("tab-builder");
        }
    }
#endif
}
