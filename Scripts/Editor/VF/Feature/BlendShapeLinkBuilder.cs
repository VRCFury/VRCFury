using System.Collections.Generic;
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
            content.Add(VRCFuryEditorUtils.Prop(prop.FindPropertyRelative("baseObj")));

            content.Add(VRCFuryEditorUtils.WrappedLabel("Skinned meshes to link:"));
            content.Add(VRCFuryEditorUtils.List(prop.FindPropertyRelative("objs")));
            
            var adv = new Foldout {
                text = "Advanced Options",
                value = false
            };
            content.Add(adv);

            var includeAll = prop.FindPropertyRelative("includeAll");
            var excludes = prop.FindPropertyRelative("excludes");

            adv.Add(VRCFuryEditorUtils.Prop(includeAll, "Include all blendshapes from base"));
            adv.Add(VRCFuryEditorUtils.RefreshOnChange(() => {
                var o = new VisualElement();
                if (includeAll.boolValue) {
                    o.Add(VRCFuryEditorUtils.WrappedLabel("Exclude blendshapes:"));
                    o.Add(VRCFuryEditorUtils.List(excludes, (i, exclude) =>
                        VRCFuryEditorUtils.Prop(exclude.FindPropertyRelative("name"))
                    ));
                }
                
                o.Add(VRCFuryEditorUtils.WrappedLabel(includeAll.boolValue ? "Additional linked blendshapes:" : "Linked blendshapes:"));
                var header = new VisualElement {
                    style = {
                        flexDirection = FlexDirection.Row
                    }
                };
                header.Add(VRCFuryEditorUtils.WrappedLabel("Name on base", style: s => {
                    s.flexBasis = 0;
                    s.flexGrow = 1;
                }));
                header.Add(VRCFuryEditorUtils.WrappedLabel("Name on linked", style: s => {
                    s.flexBasis = 0;
                    s.flexGrow = 1;
                }));
                o.Add(header);
                
                o.Add(VRCFuryEditorUtils.List(prop.FindPropertyRelative("includes"), (i, include) => {
                    var row = new VisualElement {
                        style = {
                            flexDirection = FlexDirection.Row
                        }
                    };
                    row.Add(VRCFuryEditorUtils.Prop(include.FindPropertyRelative("nameOnBase"), style: s => {
                        s.flexBasis = 0;
                        s.flexGrow = 1;
                    }));
                    row.Add(VRCFuryEditorUtils.Prop(include.FindPropertyRelative("nameOnLinked"), style: s => {
                        s.flexBasis = 0;
                        s.flexGrow = 1;
                    }));
                    return row;
                }));
                
                return o;
            }, includeAll));

            return content;
        }

        [FeatureBuilderAction(FeatureOrder.BlendShapeLinkFixAnimations)]
        public void Apply() {
            var baseSkin = avatarObject.GetComponentsInChildren<Transform>(true)
                .Where(t => t.name == model.baseObj)
                .Select(t => t.GetComponent<SkinnedMeshRenderer>())
                .Where(skin => skin && skin.sharedMesh)
                .OrderBy(skin => AnimationUtility.CalculateTransformPath(skin.transform, avatarObject.transform).Length)
                .FirstOrDefault();

            if (!baseSkin) {
                Debug.LogWarning("Failed to find base skin on avatar");
                return;
            }

            var baseSkinPath = AnimationUtility.CalculateTransformPath(baseSkin.transform, avatarObject.transform);

            var baseToLinkedMapping = new Dictionary<string, string>();
            if (model.includeAll) {
                for (var i = 0; i < baseSkin.sharedMesh.blendShapeCount; i++) {
                    var name = baseSkin.sharedMesh.GetBlendShapeName(i);
                    baseToLinkedMapping[name] = name;
                }
                foreach (var exclude in model.excludes) {
                    baseToLinkedMapping.Remove(exclude.name);
                }
            }
            foreach (var include in model.includes) {
                if (string.IsNullOrWhiteSpace(include.nameOnBase)) {
                    if (string.IsNullOrWhiteSpace(include.nameOnLinked)) continue;
                    baseToLinkedMapping[include.nameOnLinked] = include.nameOnLinked;
                } else if (string.IsNullOrWhiteSpace(include.nameOnLinked)) {
                    baseToLinkedMapping[include.nameOnBase] = include.nameOnBase;
                } else {
                    baseToLinkedMapping[include.nameOnBase] = include.nameOnLinked;
                }
            }

            var linkSkins = model.objs
                .Where(obj => obj != null)
                .SelectMany(obj => obj.GetComponents<SkinnedMeshRenderer>())
                .Where(skin => skin.sharedMesh)
                .ToArray();

            foreach (var linked in linkSkins) {
                foreach (var (baseName,linkedName) in baseToLinkedMapping.Select(x => (x.Key, x.Value))) {
                    var baseI = baseSkin.sharedMesh.GetBlendShapeIndex(baseName);
                    var linkedI = linked.sharedMesh.GetBlendShapeIndex(linkedName);
                    if (baseI < 0 || linkedI < 0) continue;
                    var baseWeight = baseSkin.GetBlendShapeWeight(baseI);
                    linked.SetBlendShapeWeight(linkedI, baseWeight);
                }
            }

            foreach (var c in manager.GetAllUsedControllers()) {
                c.ForEachClip(clip => {
                    foreach (var binding in clip.GetFloatBindings()) {
                        if (binding.type != typeof(SkinnedMeshRenderer)) continue;
                        if (binding.path != baseSkinPath) continue;
                        if (!binding.propertyName.StartsWith("blendShape.")) continue;
                        var baseName = binding.propertyName.Substring(11);
                        if (!baseToLinkedMapping.TryGetValue(baseName, out var linkedName)) continue;
                        foreach (var linked in linkSkins) {
                            var linkedI = linked.sharedMesh.GetBlendShapeIndex(linkedName);
                            if (linkedI < 0) continue;
                            var newBinding = binding;
                            newBinding.path =
                                AnimationUtility.CalculateTransformPath(linked.transform, avatarObject.transform);
                            newBinding.propertyName = "blendShape." + linkedName;

                            clip.SetFloatCurve(newBinding, clip.GetFloatCurve(binding));
                        }
                    }
                });
            }
        }
    }
}
