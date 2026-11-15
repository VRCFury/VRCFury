using System;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;
using VF.Hooks;
using VF.Utils;
using Object = UnityEngine.Object;

namespace VF.Tests {
    [Category("VRCFury")]
    public class IsActuallyUploadingHookTest {
        [TestCase("Idle", false)]
        [TestCase("Building", true)]
        [TestCase("Success", false)]
        [TestCase("Failure", false)]
        public void DetectsBuilderStateWithoutActiveBuildType(string state, bool expected) {
            var sdkBuilder = ReflectionUtils.GetTypeFromAnyAssembly("VRC.SDKBase.Editor.VRC_SdkBuilder");
            var activeBuildType = sdkBuilder?.GetProperty("ActiveBuildType");
            if (activeBuildType == null) Assert.Ignore("SDK uses legacy build detection.");
            var builderType = ReflectionUtils.GetTypeFromAnyAssembly("VRC.SDK3A.Editor.VRCSdkControlPanelAvatarBuilder");
            if (builderType == null) Assert.Ignore("Requires the avatar SDK builder.");
            var panelType = ReflectionUtils.GetTypeFromAnyAssembly("VRCSdkControlPanel");
            var window = panelType.GetField("window");
            var builders = panelType.GetField("_sdkBuilders", BindingFlags.NonPublic | BindingFlags.Instance);
            var buildState = builderType.GetField("_buildState", BindingFlags.NonPublic | BindingFlags.Instance);
            var previousWindow = window.GetValue(null);
            var previousBuildType = activeBuildType.GetValue(null);
            ScriptableObject panel = null;
            try {
                activeBuildType.SetValue(null, Enum.Parse(activeBuildType.PropertyType, "None"));
                window.SetValue(null, null);
                Assert.That(IsActuallyUploadingHook.Get(), Is.False, "No SDK window or build.");
                activeBuildType.SetValue(null, Enum.Parse(activeBuildType.PropertyType, "Publish"));
                Assert.That(IsActuallyUploadingHook.Get(), Is.True, "External builds need no SDK window.");
                activeBuildType.SetValue(null, Enum.Parse(activeBuildType.PropertyType, "None"));

                panel = ScriptableObject.CreateInstance(panelType);
                var builder = Activator.CreateInstance(builderType);
                buildState.SetValue(builder, Enum.Parse(buildState.FieldType, state));
                var array = Array.CreateInstance(builders.FieldType.GetElementType(), 1);
                array.SetValue(builder, 0);
                builders.SetValue(panel, array);
                window.SetValue(null, panel);
                Assert.That(IsActuallyUploadingHook.Get(), Is.EqualTo(expected));
            } finally {
                if (panel != null) Object.DestroyImmediate(panel);
                window.SetValue(null, previousWindow);
                activeBuildType.SetValue(null, previousBuildType);
            }
        }
    }
}
