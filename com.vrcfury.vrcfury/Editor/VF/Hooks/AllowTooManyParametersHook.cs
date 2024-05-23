#if VRC_NEW_PUBLIC_SDK
using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using UnityEditor;
using UnityEngine;
using VF.Builder;
using VF.Model;
using VF.Model.Feature;
using VRC.SDK3.Avatars.Components;
using VRC.SDK3A.Editor;
using Object = UnityEngine.Object;
#endif

namespace VF.Hooks {
    public static class AllowTooManyParametersHook {
#if VRC_NEW_PUBLIC_SDK
        [InitializeOnLoadMethod]
        private static void Init() {
            VRCSdkControlPanel.OnSdkPanelEnable += (sender, e) => {
                if (VRCSdkControlPanel.TryGetBuilder<IVRCSdkAvatarBuilderApi>(out var _builder)) {
                    builder = _builder;
                }
            };
            EditorApplication.update += Check;
        }

        private static IVRCSdkAvatarBuilderApi builder;
        private static double lastCheck = 0;

        private static void Check() {
            try {
                CheckUnsafe();
            } catch (Exception) { /**/ }
        }
        private static void CheckUnsafe() {
            var now = EditorApplication.timeSinceStartup;
            if (now - lastCheck < 1) return;
            lastCheck = now;
 
            if (builder == null) return;
            var panel = builder.GetType().GetField("_builder",
                    BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic)?
                .GetValue(builder);
            if (panel == null) return;
            var errors = panel.GetType().GetField("GUIErrors",
                BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic)?
                .GetValue(panel) as IEnumerable;
            if (errors == null) return;
            foreach (var pair in errors) {
                var key = pair.GetType().GetProperty("Key")?.GetValue(pair) as Object;
                var value = pair.GetType().GetProperty("Value")?.GetValue(pair) as IList;
                if (key == null || value == null) continue;
                var avatar = key as VRCAvatarDescriptor;
                if (avatar == null) continue;
                var remove = new List<object>();
                foreach (var issue in value) {
                    var issueText = issue.GetType().GetField("issueText").GetValue(issue) as string;
                    if (issueText == null) continue;
                    if (!issueText.Contains("VRCExpressionParameters has too many parameters")) continue;
                    var hasUnlimitedParameters = avatar.owner()
                        .GetComponentsInSelfAndChildren<VRCFury>()
                        .SelectMany(v => v.GetAllFeatures())
                        .Any(f => f is UnlimitedParameters);
                    if (hasUnlimitedParameters) remove.Add(issue);
                }
                foreach (var r in remove) {
                    value.Remove(r);
                }
            }
        }
#endif
    }
}
