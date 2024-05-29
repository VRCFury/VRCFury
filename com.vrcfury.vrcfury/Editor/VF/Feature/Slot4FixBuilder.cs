using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.UIElements;
using VF.Builder;
using VF.Feature.Base;
using VF.Inspector;
using VF.Model.Feature;
using VF.Utils;

namespace VF.Feature {
    internal class Slot4FixBuilder : FeatureBuilder<Slot4Fix> {
        public override string GetEditorTitle() {
            return "Slot 4 Fix";
        }

        public override VisualElement CreateEditor(SerializedProperty prop) {
            var content = new VisualElement();
            content.Add(VRCFuryEditorUtils.Error("This feature is deprecated and now does nothing. The slot 4 bug has been fixed in unity 2022."));
            return content;
        }

        public override bool ShowInMenu() {
            return false;
        }
    }
}
