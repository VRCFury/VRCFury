using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using System.Text.RegularExpressions;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;
using VF.Builder;
using VF.Feature.Base;
using VF.Inspector;
using VF.Model.Feature;
using VF.Utils;

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
            
            content.Add(VRCFuryEditorUtils.Debug(refreshMessage: () => {
                if (avatarObject == null) {
                    return "Avatar descriptor is missing";
                }
                var baseSkin = GetBaseSkin();
                if (!baseSkin) {
                    return "Base skin could not be found";
                }

                var linkSkins = GetLinkSkins();
                var mappingsWithSkin = linkSkins
                    .SelectMany(s => GetMappings(baseSkin, s).Select(m => (s, (m.Key, m.Value))))
                    .ToImmutableHashSet();
                var allMappings = mappingsWithSkin
                    .Select(tuple => tuple.Item2)
                    .Distinct()
                    .OrderBy(mapping => mapping)
                    .ToArray();

                var text = new List<string>();
                text.Add("Base Skin: " + baseSkin.owner().name);
                if (linkSkins.Count > 0) {
                    text.Add("Linked Skins: " + string.Join(", ", linkSkins.Select(l => l.owner().name)));
                } else {
                    text.Add("No valid linked skins found");
                }

                if (allMappings.Length > 0) {
                    string FormatMapping((string,string) pair) {
                        var (from, to) = pair;
                        string skinListStr = "";
                        if (linkSkins.Count > 1) {
                            var skinList = linkSkins
                                .Where(s => mappingsWithSkin.Contains((s, pair)))
                                .Select(s => s.name);
                            skinListStr = " (" + string.Join(",", skinList) + ")";
                        }

                        if (from == to) return from + skinListStr;
                        return from + " > " + to + skinListStr;
                    }
                    text.Add("Linked Blendshapes:\n" + string.Join("\n", allMappings.Select(FormatMapping)));
                } else {
                    text.Add("No valid mappings found");
                }
                return string.Join("\n", text);
            }));

            return content;
        }

        private IList<SkinnedMeshRenderer> GetLinkSkins() {
            return model.objs
                .Where(obj => obj != null)
                .SelectMany(obj => obj.GetComponents<SkinnedMeshRenderer>())
                .Where(skin => skin.sharedMesh)
                .ToArray();
        }

        private SkinnedMeshRenderer GetBaseSkin() {
            return avatarObject.GetSelfAndAllChildren()
                .Where(t => t.name == model.baseObj)
                .Select(t => t.GetComponent<SkinnedMeshRenderer>())
                .Where(skin => skin && skin.sharedMesh)
                .OrderBy(skin => AnimationUtility.CalculateTransformPath(skin.transform, avatarObject.transform).Length)
                .FirstOrDefault();
        }

        private ISet<String> GetBlendshapesInSkin(SkinnedMeshRenderer skin) {
            return Enumerable.Range(0, skin.sharedMesh.blendShapeCount)
                .Select(i => skin.sharedMesh.GetBlendShapeName(i))
                .ToImmutableHashSet();
        }
        
        delegate string Normalizer(string input);

        private class FuzzyFinder {
            private Normalizer[] normalizers;
            private IDictionary<string, string>[] preNormalized;

            public FuzzyFinder(ICollection<string> names, params Normalizer[] normalizers) {
                this.normalizers = normalizers;
                preNormalized = normalizers.Select(n => PreNormalize(names, n)).ToArray();
            }

            private static IDictionary<string, string> PreNormalize(ICollection<string> names, Normalizer normalizer) {
                return names
                    .Select(name => (normalizer(name), name))
                    .GroupBy(tuple => tuple.Item1)
                    .Select(group => (group.Key, group.Select(i => i.Item2).ToArray()))
                    .Where(pair => pair.Item2.Length == 1)
                    .ToDictionary(pair => pair.Item1, pair => pair.Item2[0]);
            }

            public string Lookup(string name) {
                for (var i = 0; i < normalizers.Length; i++) {
                    var normalizer = normalizers[i];
                    var preNorm = preNormalized[i];
                    var normalized = normalizer(name);
                    if (preNorm.TryGetValue(normalized, out var result)) {
                        return result;
                    }
                }
                return null;
            }
        }

        private Dictionary<string, string> GetMappings(SkinnedMeshRenderer baseSkin, SkinnedMeshRenderer linkSkin) {
            var normalizers = new Normalizer[] {
                s => s,
                s => Regex.Replace(s.ToLower(), @"\s", ""),
                s => Regex.Replace(s.ToLower(), @"[^a-z0-9+-]", "")
            };

            var baseBlendshapes = GetBlendshapesInSkin(baseSkin);
            var baseBlendshapesLookup = new FuzzyFinder(baseBlendshapes, normalizers);
            var linkBlendshapes = GetBlendshapesInSkin(linkSkin);
            var linkBlendshapesLookup = new FuzzyFinder(linkBlendshapes, normalizers);
            var outputMap = new Dictionary<string, string>();

            void Attempt(string from, string to) {
                from = baseBlendshapesLookup.Lookup(from);
                if (from == null) return;
                if (outputMap.ContainsKey(from)) return;
                to = linkBlendshapesLookup.Lookup(to);
                if (to == null) return;
                outputMap[from] = to;
            }
            
            foreach (var include in model.includes) {
                if (string.IsNullOrWhiteSpace(include.nameOnBase)) {
                    if (string.IsNullOrWhiteSpace(include.nameOnLinked)) continue;
                    Attempt(include.nameOnLinked, include.nameOnLinked);
                } else if (string.IsNullOrWhiteSpace(include.nameOnLinked)) {
                    Attempt(include.nameOnBase, include.nameOnBase);
                } else {
                    Attempt(include.nameOnBase, include.nameOnLinked);
                }
            }
            
            if (model.includeAll) {
                var excludes = model.excludes.Select(ex => ex.name).ToImmutableHashSet();
                foreach (var name in baseBlendshapes) {
                    if (excludes.Contains(name)) continue;
                    Attempt(name, name);
                }
            }

            return outputMap;
        }

        [FeatureBuilderAction(FeatureOrder.BlendShapeLinkFixAnimations)]
        public void Apply() {
            var baseSkin = GetBaseSkin();
            if (!baseSkin) {
                Debug.LogWarning("Failed to find base skin on avatar");
                return;
            }
            var baseSkinPath = AnimationUtility.CalculateTransformPath(baseSkin.transform, avatarObject.transform);
            var linkSkins = GetLinkSkins();

            foreach (var linked in linkSkins) {
                var baseToLinkedMapping = GetMappings(baseSkin, linked);
                foreach (var (baseName,linkedName) in baseToLinkedMapping.Select(x => (x.Key, x.Value))) {
                    var baseI = baseSkin.sharedMesh.GetBlendShapeIndex(baseName);
                    var linkedI = linked.sharedMesh.GetBlendShapeIndex(linkedName);
                    if (baseI < 0 || linkedI < 0) continue;
                    var baseWeight = baseSkin.GetBlendShapeWeight(baseI);
                    linked.SetBlendShapeWeight(linkedI, baseWeight);
                }

                foreach (var c in manager.GetAllUsedControllers()) {
                    c.ForEachClip(clip => {
                        foreach (var binding in clip.GetFloatBindings()) {
                            if (binding.type != typeof(SkinnedMeshRenderer)) continue;
                            if (binding.path != baseSkinPath) continue;
                            if (!binding.propertyName.StartsWith("blendShape.")) continue;
                            var baseName = binding.propertyName.Substring(11);
                            if (!baseToLinkedMapping.TryGetValue(baseName, out var linkedName)) continue;

                            var linkedI = linked.sharedMesh.GetBlendShapeIndex(linkedName);
                            if (linkedI < 0) continue;
                            var newBinding = binding;
                            newBinding.path =
                                AnimationUtility.CalculateTransformPath(linked.transform, avatarObject.transform);
                            newBinding.propertyName = "blendShape." + linkedName;

                            clip.SetFloatCurve(newBinding, clip.GetFloatCurve(binding));
                        }
                    });
                }
            }
        }
    }
}
