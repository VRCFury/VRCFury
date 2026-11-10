using System.Collections.Generic;
using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;
using UnityEngine.UIElements;
using VF.Builder;
using VF.Feature.Base;
using VF.Injector;
using VF.Inspector;
using VF.Model;
using VF.Model.Feature;
using VF.Service;
using VF.Utils;
using VRC.SDK3.Avatars.ScriptableObjects;
using Object = UnityEngine.Object;

namespace VF.Feature {

    [FeatureTitle("Zawoo Integration")]
    [FeatureRootOnly]
    [FeatureHideInMenu]
    internal class ZawooIntegrationBuilder : FeatureBuilder<ZawooIntegration> {

        [FeatureEditor]
        public static VisualElement Editor(SerializedProperty prop) {
            var content = new VisualElement();
            content.Add(VRCFuryEditorUtils.Error(
                "This feature is no longer supported."));
            return content;
        }
    }

}
