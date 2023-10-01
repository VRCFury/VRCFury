using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;
using VF.Builder;
using VF.Feature.Base;
using VF.Injector;
using VF.Inspector;
using VF.Model.Feature;

namespace VF.Feature {
    [VFService]
    public class EditorOnlyBuilder : FeatureBuilder {
        
        [FeatureBuilderAction(FeatureOrder.RemoveEditorOnly)]
        public void Apply() {
            RemoveChildrenWithEditorOnly(avatarObject);
        }

        private void RemoveChildrenWithEditorOnly(VFGameObject gameObject) {
            foreach (var child in gameObject.Children()) {
                if (child.gameObject.CompareTag("EditorOnly")) {
                    child.Destroy();
                } else if (child.childCount > 0) {
                    RemoveChildrenWithEditorOnly(child);
                }
            }
        }
    }
}