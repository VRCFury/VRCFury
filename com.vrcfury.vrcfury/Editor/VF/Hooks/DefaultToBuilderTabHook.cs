using System;
using System.Reflection;
using UnityEditor;
using UnityEngine.UIElements;
using VF.Utils;

namespace VF.Hooks {
    internal static class DefaultToBuilderTabHook {
        private static double whenSdkPanelLoaded;
        private static EditorWindow sdkPanel = null;
 
        private static PropertyInfo APIUser_IsLoggedIn = ReflectionUtils.GetTypeFromAnyAssembly("VRC.Core.APIUser")?
            .GetProperty("IsLoggedIn", BindingFlags.Static | BindingFlags.Public);

        [InitializeOnLoadMethod]
        private static void Init() {
            if (APIUser_IsLoggedIn == null) return;
#if VRC_NEW_PUBLIC_SDK && UNITY_2022_1_OR_NEWER
            VRCSdkControlPanel.OnSdkPanelEnable += (panel, e) => {
                sdkPanel = panel as EditorWindow;
                whenSdkPanelLoaded = EditorApplication.timeSinceStartup;
            };
            Scheduler.Schedule(() => {
                if (sdkPanel == null) return;
                var timeSincePanelLoaded = EditorApplication.timeSinceStartup - whenSdkPanelLoaded;
                if (timeSincePanelLoaded > 5) return;
                if (APIUser_IsLoggedIn.GetValue(null) as bool? != true) return;
                var builderTab = sdkPanel.rootVisualElement.Q<Button>("tab-builder");
                if (builderTab == null) return;
                if (!builderTab.enabledInHierarchy) return;
                sdkPanel = null;
                using (var ev = new NavigationSubmitEvent { target = builderTab }) {
                    builderTab.SendEvent(ev);
                }
            }, 200);
#endif 
        }
    }
}