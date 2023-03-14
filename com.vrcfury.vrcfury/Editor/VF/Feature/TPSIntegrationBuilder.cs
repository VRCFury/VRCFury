using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;
using UnityEngine.UIElements;
using VF.Builder;
using VF.Builder.Exceptions;
using VF.Feature.Base;
using VF.Inspector;
using VF.Menu;
using VF.Model.Feature;

namespace VF.Feature {
    public class TPSIntegrationBuilder : FeatureBuilder<TPSIntegration2> {
        [FeatureBuilderAction]
        public void Apply() {

        }

        public override bool ShowInMenu() {
            return false;
        }

        public override string GetEditorTitle() {
            return "TPS Integration";
        }
        
        public override VisualElement CreateEditor(SerializedProperty prop) {
            var content = new VisualElement();
            content.Add(VRCFuryEditorUtils.Error("This feature is deprecated and now does nothing. Use VRCFury Haptic Socket and VRCFury Haptic Plug (with TPS autoconfiguration) instead!"));
            return content;
        }

        public override bool AvailableOnProps() {
            return false;
        }
    }
}