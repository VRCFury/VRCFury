using System;
using System.Collections;
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
    internal class BlendShapeLinkBuilder : FeatureBuilder<BlendShapeLink> {
        public override string GetEditorTitle() {
            return "BlendShape Link";
        }

        [CustomPropertyDrawer(typeof(BlendShapeLink.Exclude))]
        public class ExcludeDrawer : PropertyDrawer {
            public override VisualElement CreatePropertyGUI(SerializedProperty exclude) {
                return VRCFuryEditorUtils.Prop(exclude.FindPropertyRelative("name"));
            }
        }
        
        [CustomPropertyDrawer(typeof(BlendShapeLink.Include))]
        public class IncludeDrawer : PropertyDrawer {
            public override VisualElement CreatePropertyGUI(SerializedProperty include) {
                var row = new VisualElement().Row();
                row.Add(VRCFuryEditorUtils.Prop(include.FindPropertyRelative("nameOnBase")).FlexBasis(1).FlexGrow(1));
                row.Add(VRCFuryEditorUtils.Prop(include.FindPropertyRelative("nameOnLinked")).FlexBasis(1).FlexGrow(1));
                return row;
            }
        }
        
        [CustomPropertyDrawer(typeof(BlendShapeLink.LinkSkin))]
        public class LinkSkinDrawer : PropertyDrawer {
            public override VisualElement CreatePropertyGUI(SerializedProperty prop) {
                return VRCFuryEditorUtils.Prop(prop.FindPropertyRelative("renderer"));
            }
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
            content.Add(VRCFuryEditorUtils.List(prop.FindPropertyRelative("linkSkins")));
            
            var adv = new Foldout {
                text = "Advanced Options",
                value = false
            };
            content.Add(adv);

            var includeAll = prop.FindPropertyRelative("includeAll");
            var excludes = prop.FindPropertyRelative("excludes");
            var exactMatch = prop.FindPropertyRelative("exactMatch");

            adv.Add(VRCFuryEditorUtils.Prop(includeAll, "Include all blendshapes from base"));
            adv.Add(VRCFuryEditorUtils.Prop(exactMatch, "Link blendshapes with exact names only"));
            adv.Add(VRCFuryEditorUtils.RefreshOnChange(() => {
                var o = new VisualElement();
                if (includeAll.boolValue) {
                    o.Add(VRCFuryEditorUtils.WrappedLabel("Exclude blendshapes:"));
                    o.Add(VRCFuryEditorUtils.List(excludes));
                }
                
                o.Add(VRCFuryEditorUtils.WrappedLabel(includeAll.boolValue ? "Additional linked blendshapes:" : "Linked blendshapes:"));
                var header = new VisualElement().Row();
                header.Add(VRCFuryEditorUtils.WrappedLabel("Name on base").FlexGrow(1));
                header.Add(VRCFuryEditorUtils.WrappedLabel("Name on linked").FlexGrow(1));
                o.Add(header);
                
                o.Add(VRCFuryEditorUtils.List(prop.FindPropertyRelative("includes")));
                
                return o;
            }, includeAll));
            
            adv.Add(new Button(() => {
                var skins = new List<SkinnedMeshRenderer>();
                var linkList = prop.FindPropertyRelative("linkSkins").GetObject() as IList;
                if (linkList != null) {
                    skins.AddRange(linkList.OfType<SkinnedMeshRenderer>()
                        .Append(prop.FindPropertyRelative("baseObj").objectReferenceValue as SkinnedMeshRenderer)
                        .NotNull());
                }
                var baseName = prop.FindPropertyRelative("baseObj").stringValue;
                if (avatarObject != null) {
                    var baseObj = avatarObject.Find(baseName);
                    if (baseObj != null) {
                        var baseSkin = baseObj.GetComponent<SkinnedMeshRenderer>();
                        if (baseSkin != null) skins.Add(baseSkin);
                    }
                }
                VRCFuryActionDrawer.ShowBlendshapeSearchWindow(skins, value => {
                    GUIUtility.systemCopyBuffer = value;
                });
            }) { text = "Copy blendshape to clipboard" });
            
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
                    .SelectMany(s => GetMappings(baseSkin, s, exactMatch.boolValue).Select(m => (s, (m.Key, m.Value))))
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
                        var skinListStr = "";
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
            return model.linkSkins
                .Select(link => link.renderer)
                .NotNull()
                .ToArray();
        }

        private SkinnedMeshRenderer GetBaseSkin() {
            return avatarObject.GetSelfAndAllChildren()
                .Where(t => t.name == model.baseObj)
                .Select(t => t.GetComponent<SkinnedMeshRenderer>())
                .NotNull()
                .OrderBy(skin => skin.owner().GetPath(avatarObject).Length)
                .FirstOrDefault();
        }

        delegate string Normalizer(string input);

        private class FuzzyFinder {
            private readonly Normalizer[] normalizers;
            private readonly IDictionary<string, string>[] preNormalized;

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

        private VFMultimapSet<string, string> GetMappings(SkinnedMeshRenderer baseSkin, SkinnedMeshRenderer linkSkin, bool exact) {
            Normalizer[] normalizers;
            if (exact) {
                normalizers = new Normalizer[] { s => s };
            } else {
                normalizers = new Normalizer[] {
                    s => s,
                    s => Regex.Replace(s.ToLower(), @"\s", ""),
                };
            }

            var baseBlendshapes = baseSkin.GetBlendshapeNames();
            var baseBlendshapesLookup = new FuzzyFinder(baseBlendshapes, normalizers);
            var linkBlendshapes = linkSkin.GetBlendshapeNames();
            var linkBlendshapesLookup = new FuzzyFinder(linkBlendshapes, normalizers);
            var outputMap = new VFMultimapSet<string, string>();

            void Attempt(string from, string to, bool allowDuplicates) {
                from = baseBlendshapesLookup.Lookup(from);
                if (from == null) return;
                if (outputMap.ContainsKey(from) && !allowDuplicates) return;
                to = linkBlendshapesLookup.Lookup(to);
                if (to == null) return;
                outputMap.Put(from, to);
            }
            
            foreach (var include in model.includes) {
                if (string.IsNullOrWhiteSpace(include.nameOnBase)) {
                    if (string.IsNullOrWhiteSpace(include.nameOnLinked)) continue;
                    Attempt(include.nameOnLinked, include.nameOnLinked, true);
                } else if (string.IsNullOrWhiteSpace(include.nameOnLinked)) {
                    Attempt(include.nameOnBase, include.nameOnBase, true);
                } else {
                    Attempt(include.nameOnBase, include.nameOnLinked, true);
                }
            }
            
            if (model.includeAll) {
                var excludes = model.excludes.Select(ex => ex.name).ToImmutableHashSet();
                foreach (var name in baseBlendshapes) {
                    if (excludes.Contains(name)) continue;
                    Attempt(name, name, false);
                }
            }

            return outputMap;
        }

        [FeatureBuilderAction(FeatureOrder.BlendShapeLinkFixAnimations)]
        public void Apply() {
            var baseSkin = GetBaseSkin();
            if (baseSkin == null) {
                Debug.LogWarning("Failed to find base skin on avatar");
                return;
            }
            var baseSkinPath = baseSkin.owner().GetPath(avatarObject);
            var linkSkins = GetLinkSkins();

            foreach (var linked in linkSkins) {
                var baseToLinkedMapping = GetMappings(baseSkin, linked, model.exactMatch);
                foreach (var (baseName,linkedName) in baseToLinkedMapping.Select(x => (x.Key, x.Value))) {
                    var baseI = baseSkin.GetBlendShapeIndex(baseName);
                    var linkedI = linked.GetBlendShapeIndex(linkedName);
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

                            foreach (var linkedName in baseToLinkedMapping.Get(baseName)) {
                                var linkedI = linked.GetBlendShapeIndex(linkedName);
                                if (linkedI < 0) continue;
                                var newBinding = binding;
                                newBinding.path = linked.owner().GetPath(avatarObject);
                                newBinding.propertyName = "blendShape." + linkedName;

                                clip.SetCurve(newBinding, clip.GetFloatCurve(binding));
                            }
                        }
                    });
                }
            }
        }
    }
}
