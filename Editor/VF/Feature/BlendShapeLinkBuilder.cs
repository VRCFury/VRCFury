using System.Linq;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;
using VF.Builder;
using VF.Feature.Base;
using VF.Inspector;
using VF.Model.Feature;

namespace VF.Feature {
    public class BlendShapeLinkBuilder : FeatureBuilder<BlendShapeLink> {
        public override string GetEditorTitle() {
            return "BlendShape Link";
        }

        public override VisualElement CreateEditor(SerializedProperty prop) {
            var content = new VisualElement();
            
            content.Add(VRCFuryEditorUtils.Info(
                "This feature will link the blendshapes in the specified skinned meshes to a skinned mesh" +
                " on the avatar base. The default value and any menu items controlling the blendshape" +
                " will be updated to control these linked objects as well."));
            
            content.Add(VRCFuryEditorUtils.WrappedLabel("Name of skinned mesh object on avatar:"));
            content.Add(VRCFuryEditorUtils.PropWithoutLabel(prop.FindPropertyRelative("baseObj")));

            content.Add(VRCFuryEditorUtils.WrappedLabel("Skinned meshes to link:"));
            content.Add(VRCFuryEditorUtils.List(prop.FindPropertyRelative("objs"), (i,el) => 
                VRCFuryEditorUtils.PropWithoutLabel(el)));

            return content;
        }

        [FeatureBuilderAction(FeatureOrder.BlendShapeLinkFixAnimations)]
        public void Apply() {
            var baseSkin = avatarObject.GetComponentsInChildren<Transform>(true)
                .Where(t => t.name == model.baseObj)
                .Select(t => t.GetComponent<SkinnedMeshRenderer>())
                .Where(skin => skin != null)
                .OrderBy(skin => AnimationUtility.CalculateTransformPath(skin.transform, avatarObject.transform).Length)
                .FirstOrDefault();

            if (baseSkin == null) {
                Debug.LogWarning("Failed to find base skin on avatar");
                return;
            }

            var baseSkinPath = AnimationUtility.CalculateTransformPath(baseSkin.transform, avatarObject.transform);

            var linkSkins = model.objs
                .Where(obj => obj != null)
                .SelectMany(obj => obj.GetComponents<SkinnedMeshRenderer>())
                .ToArray();

            foreach (var skin in linkSkins) {
                for (var i = 0; i < skin.sharedMesh.blendShapeCount; i++) {
                    var bsName = skin.sharedMesh.GetBlendShapeName(i);
                    var baseI = baseSkin.sharedMesh.GetBlendShapeIndex(bsName);
                    if (baseI < 0) continue;
                    var baseWeight = baseSkin.GetBlendShapeWeight(baseI);
                    skin.SetBlendShapeWeight(i, baseWeight);
                }
            }

            foreach (var controller in manager.GetAllUsedControllers()) {
                for (var layerId = 0; layerId < controller.GetRaw().layers.Length; layerId++) {
                    var layer = controller.GetRaw().layers[layerId];
                    AnimatorIterator.ForEachClip(layer, (clip, setClip) => {
                        void ensureMutable() {
                            if (!VRCFuryAssetDatabase.IsVrcfAsset(clip)) {
                                var newClip = manager.GetClipStorage().NewClip(clip.name);
                                clipBuilder.CopyWithAdjustedPrefixes(clip, newClip);
                                clip = newClip;
                                setClip(clip);
                            }
                        }
                        foreach (var binding in AnimationUtility.GetCurveBindings(clip)) {
                            if (binding.type != typeof(SkinnedMeshRenderer)) continue;
                            if (binding.path != baseSkinPath) continue;
                            if (!binding.propertyName.StartsWith("blendShape.")) continue;
                            var blendShapeName = binding.propertyName.Substring(11);
                            foreach (var skin in linkSkins) {
                                var blendShapeIndex = skin.sharedMesh.GetBlendShapeIndex(blendShapeName);
                                if (blendShapeIndex < 0) continue;
                                var newBinding = binding;
                                newBinding.path =
                                    AnimationUtility.CalculateTransformPath(skin.transform, avatarObject.transform);
                                ensureMutable();
                                AnimationUtility.SetEditorCurve(clip, newBinding, AnimationUtility.GetEditorCurve(clip, binding));
                            }
                        }
                    });
                }
            }
        }
    }
}
