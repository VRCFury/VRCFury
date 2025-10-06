using UnityEditor;
using UnityEngine.UIElements;
using VF.Component;

namespace VF.Inspector {
    [CustomEditor(typeof(VRCFuryKeepAnchorOverride), true)]
    internal class VRCFKeepAnchorOverrideEditor : VRCFuryComponentEditor<VRCFuryKeepAnchorOverride> {
        protected override VisualElement CreateEditor(SerializedObject serializedObject, VRCFuryKeepAnchorOverride target) {
            var container = new VisualElement();
						container.Add(VRCFuryEditorUtils.Info(
							"Any renderers under this component will be unaffected by the Anchor Override Fix component. " +
							"This is intended for assets that are 'separate' from the avatar like world drops or followers."
						));
            return container;
        }
    }
}
