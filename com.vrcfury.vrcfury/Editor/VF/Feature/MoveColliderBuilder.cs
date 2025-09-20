using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;
using VF.Feature.Base;
using VF.Inspector;
using VF.Model.Feature;

namespace VF.Feature
{
	[FeatureTitle("Move Collider")]
	[FeatureRootOnly]
	internal class MoveColliderBuilder : FeatureBuilder<MoveCollider> {
		[FeatureEditor]
		public static VisualElement Editor(SerializedProperty prop) {
			var content = new VisualElement();
			content.Add(VRCFuryEditorUtils.Info(
				"This moves a default collider (like head, torso, etc) to a different object than normal." +
				" There can only be one of each type on the avatar.\n\nMoving a hand collider will also" +
				" move where you grab physbones from."
			));
			content.Add(VRCFuryEditorUtils.Prop(prop.FindPropertyRelative("avatarCollider"), "Collider"));
			content.Add(VRCFuryEditorUtils.Prop(prop.FindPropertyRelative("rootTransform"), "Root Transform"));
			content.Add(VRCFuryEditorUtils.Prop(prop.FindPropertyRelative("radius"), "Radius"));
			content.Add(VRCFuryEditorUtils.Prop(prop.FindPropertyRelative("height"), "Height"));
			return content;
		}

		public Transform GetTransform() {
			return model.rootTransform != null ? model.rootTransform : featureBaseObject;
		}
	}
}
