using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;
using VF.Utils;

namespace VF.Utils.Controller {
    internal class VFTree : VFMotion {
        private string treeName;
        private BlendTreeType _blendType;
        private bool useAutomaticThresholds;
        private string blendParameter;
        private string blendParameterY;
        private float minThreshold;
        private float maxThreshold;
        private bool normalizedBlendValues;
        private List<VFTreeChild> _children;
        private bool isDirty;

        private VFTree() : base(null) {
        }

        private VFTree(
            BlendTree sourceRaw
        ) : base(sourceRaw) {
        }

        internal static VFTree Load(BlendTree raw, VFLoadContext context) {
            if (raw == null) return null;
            var output = new VFTree(raw);
            output.treeName = raw.name;
            output._blendType = raw.blendType;
            output.useAutomaticThresholds = raw.useAutomaticThresholds;
            output.blendParameter = raw.blendParameter;
            output.blendParameterY = raw.blendParameterY;
            output.minThreshold = raw.minThreshold;
            output.maxThreshold = raw.maxThreshold;
            output.normalizedBlendValues = GetNormalizedBlendValuesRaw(raw);
            output._children = raw.children
                .Select(child => VFTreeChild.Load(child, context))
                .ToList();
            output.isDirty = raw.children
                .Select((child, i) => !ReferenceEquals(output._children[i].motion?.GetSourceAsset(), child.motion))
                .Any(changed => changed);
            return output;
        }

        internal static VFTree Create(
            string name,
            BlendTreeType blendType,
            string blendParameter = null,
            string blendParameterY = null
        ) {
            return new VFTree {
                treeName = name,
                _blendType = blendType,
                useAutomaticThresholds = false,
                blendParameter = blendParameter,
                blendParameterY = blendParameterY,
                normalizedBlendValues = false,
                _children = new List<VFTreeChild>(),
                isDirty = true
            };
        }

        internal string name => treeName;
        internal IReadOnlyList<VFTreeChild> children => _children;

        internal void AddChild(VFTreeChild child) {
            if (child == null) throw new ArgumentNullException(nameof(child));
            if (_children == null) _children = new List<VFTreeChild>();
            _children.Add(child);
            isDirty = true;
        }

        internal void SetNormalizedBlendValues(bool on) {
            if (normalizedBlendValues == on) return;
            normalizedBlendValues = on;
            isDirty = true;
        }

        internal void RewriteChildren(Func<VFTreeChild, OneOrMany<VFTreeChild>> rewrite) {
            var updated = false;
            var rewrittenChildren = new List<VFTreeChild>(_children.Count);
            foreach (var child in _children) {
                var rewritten = rewrite(new VFTreeChild {
                    directBlendParameter = child.directBlendParameter,
                    threshold = child.threshold,
                    position = child.position,
                    timeScale = child.timeScale,
                    cycleOffset = child.cycleOffset,
                    mirror = child.mirror,
                    motion = child.motion
                }).Get().Where(c => c != null).ToList();
                var first = rewritten.FirstOrDefault();
                updated |= rewritten.Count != 1
                    || first == null
                    || child.directBlendParameter != first.directBlendParameter
                    || child.threshold != first.threshold
                    || child.position != first.position
                    || child.timeScale != first.timeScale
                    || child.cycleOffset != first.cycleOffset
                    || child.mirror != first.mirror
                    || !ReferenceEquals(child.motion, first.motion);
                rewrittenChildren.AddRange(rewritten);
            }
            if (updated) {
                _children = rewrittenChildren;
                isDirty = true;
            }
        }

        internal override VFMotion GetLastFrame(bool last = true) {
            var clone = (VFTree)Clone();
            clone.RewriteChildren(child => {
                child.motion = child.motion?.GetLastFrame(last);
                return child;
            });
            return clone;
        }

        internal override Motion Save(VFSaveContext context) {
            if (context.TryGet(this, out var existing)) {
                return existing;
            }
            var canReuseSource = context.ReuseSourceAssets && sourceRaw != null && !isDirty;
            var outputChildren = new ChildMotion[_children.Count];

            for (var i = 0; i < _children.Count; i++) {
                var child = _children[i];
                var savedMotion = child.motion?.Save(context);
                if (canReuseSource && !ReferenceEquals(savedMotion, child.motion?.GetSourceAsset())) {
                    canReuseSource = false;
                }
                outputChildren[i] = new ChildMotion {
                    directBlendParameter = child.directBlendParameter,
                    threshold = child.threshold,
                    position = child.position,
                    timeScale = child.timeScale,
                    cycleOffset = child.cycleOffset,
                    mirror = child.mirror,
                    motion = savedMotion
                };
            }

            if (canReuseSource) {
                context.Add(this, sourceRaw);
                return sourceRaw;
            }
            var output = VrcfObjectFactory.Create<BlendTree>();
            output.name = treeName;
            output.blendType = _blendType;
            output.useAutomaticThresholds = useAutomaticThresholds;
            output.blendParameter = blendParameter;
            output.blendParameterY = blendParameterY;
            output.minThreshold = minThreshold;
            output.maxThreshold = maxThreshold;
            SetNormalizedBlendValuesRaw(output, normalizedBlendValues);
            output.children = outputChildren;
            context.AddNewAsset(output);
            context.Add(this, output);
            return output;
        }

        internal override VFMotion Clone(VFMotionCloneContext context = null) {
            if (context == null) context = new VFMotionCloneContext();
            if (context.TryGet(this, out var existing)) {
                return existing;
            }
            var output = new VFTree(
                sourceRaw as BlendTree
            );
            context.Add(this, output);
            output.treeName = treeName;
            output._blendType = _blendType;
            output.useAutomaticThresholds = useAutomaticThresholds;
            output.blendParameter = blendParameter;
            output.blendParameterY = blendParameterY;
            output.minThreshold = minThreshold;
            output.maxThreshold = maxThreshold;
            output.normalizedBlendValues = normalizedBlendValues;
            output._children = _children.Select(child => child.Clone(context)).ToList();
            output.isDirty = isDirty;
            return output;
        }

        internal override void RewriteParameters(Func<string, string> rewriteParamName) {
            if (_blendType != BlendTreeType.Direct) {
                var rewrittenBlendParameter = rewriteParamName(blendParameter);
                if (rewrittenBlendParameter != blendParameter) {
                    blendParameter = rewrittenBlendParameter;
                    isDirty = true;
                }
                if (_blendType != BlendTreeType.Simple1D) {
                    var rewrittenBlendParameterY = rewriteParamName(blendParameterY);
                    if (rewrittenBlendParameterY != blendParameterY) {
                        blendParameterY = rewrittenBlendParameterY;
                        isDirty = true;
                    }
                }
            }
            if (_blendType == BlendTreeType.Direct) {
                foreach (var child in _children) {
                    var rewrittenDirectBlendParameter = rewriteParamName(child.directBlendParameter);
                    if (rewrittenDirectBlendParameter != child.directBlendParameter) {
                        child.directBlendParameter = rewrittenDirectBlendParameter;
                        isDirty = true;
                    }
                }
            }
        }

        internal BlendTreeType blendType => _blendType;
        internal bool NormalizedBlendValues => normalizedBlendValues;
        internal string BlendParameter => blendParameter;
        internal string BlendParameterY => blendParameterY;

        internal static bool GetNormalizedBlendValuesRaw(BlendTree tree) {
            using (var so = new SerializedObject(tree)) {
                return so.FindProperty("m_NormalizedBlendValues").boolValue;
            }
        }

        internal static void SetNormalizedBlendValuesRaw(BlendTree tree, bool on) {
            using (var so = new SerializedObject(tree)) {
                so.FindProperty("m_NormalizedBlendValues").boolValue = on;
                so.ApplyModifiedPropertiesWithoutUndo();
            }
        }

        internal override bool IsStatic() {
            return children.All(child => child.motion == null || child.motion.IsStatic());
        }

        internal override bool IsTwoState() {
            return children.All(child => child.motion != null && child.motion.IsTwoState());
        }

        internal override bool IsEmptyOrZeroLength() {
            return children.All(child => child.motion == null || child.motion.IsEmptyOrZeroLength());
        }

        internal override VFClip FlattenAll() {
            var flat = VFClip.Create(name);
            foreach (var child in children) {
                if (child.motion == null) continue;
                flat.CopyFrom(child.motion.FlattenAll());
            }
            return flat;
        }

        internal override VFClip EvaluateMotion(float fraction) {
            var output = VFClip.Create($"{name} (sampled at {Math.Round(fraction * 100)}%)");
            foreach (var clip in GetActiveClips(new HashSet<string> { VFBlendTreeDirect.AlwaysOneParam })) {
                output.CopyFrom(clip.EvaluateClip(fraction * clip.GetLengthInSeconds()));
            }
            return output;
        }

        private IList<VFClip> GetActiveClips(HashSet<string> onParams) {
            if (!children.Any()) {
                return Array.Empty<VFClip>();
            }
            if (blendType == BlendTreeType.Direct) {
                return children
                    .Where(child => child.motion != null && onParams.Contains(child.directBlendParameter))
                    .SelectMany(child => FlattenActiveClips(child.motion, onParams))
                    .ToArray();
            }
            if (blendType == BlendTreeType.Simple1D) {
                var orderedChildren = children
                    .Where(child => child.motion != null)
                    .OrderBy(child => child.threshold)
                    .ToArray();
                if (!orderedChildren.Any()) {
                    return Array.Empty<VFClip>();
                }
                if (onParams.Contains(BlendParameter)) {
                    return FlattenActiveClips(orderedChildren.Last().motion, onParams);
                }
                return FlattenActiveClips(orderedChildren.First().motion, onParams);
            }
            // This sampler is only used by the DBT/resting-state helpers that currently produce
            // direct and simple-1D trees. Other tree types are intentionally not approximated here.
            return Array.Empty<VFClip>();
        }

        private static IList<VFClip> FlattenActiveClips(VFMotion motion, HashSet<string> onParams) {
            if (motion == null) {
                return Array.Empty<VFClip>();
            }
            if (motion is VFClip clip) {
                return new[] { clip };
            }
            if (motion is VFTree tree) {
                return tree.GetActiveClips(onParams);
            }
            return Array.Empty<VFClip>();
        }
    }
}
