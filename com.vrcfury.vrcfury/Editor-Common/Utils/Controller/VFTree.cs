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
            context.Motions[raw] = output;
            output.treeName = raw.name;
            output._blendType = raw.blendType;
            output.useAutomaticThresholds = raw.useAutomaticThresholds;
            output.blendParameter = raw.blendParameter;
            output.blendParameterY = raw.blendParameterY;
            output.minThreshold = raw.minThreshold;
            output.maxThreshold = raw.maxThreshold;
            output.normalizedBlendValues = GetNormalizedBlendValuesRaw(raw);
            var rawChildren = raw.children;
            output._children = new List<VFTreeChild>(rawChildren.Length);
            foreach (var child in rawChildren) {
                var loaded = VFTreeChild.Load(child, context);
                output._children.Add(loaded);
                output.isDirty |= !ReferenceEquals(loaded.motion?.GetSourceAsset(), child.motion);
            }
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

        internal override Motion Save(VFSaveContext context) {
            if (context.Motions.TryGetValue(this, out var existing)) {
                return existing;
            }
            var canReuseSource = context.ReuseSourceAssets && sourceRaw != null && !isDirty;
            var output = VrcfObjectFactory.Create<BlendTree>();
            context.Motions[this] = output;
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
                context.Motions[this] = sourceRaw;
                return sourceRaw;
            }
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
            context.Motions[this] = output;
            return output;
        }

        internal override VFMotion Clone(VFCloneContext context = null) {
            if (context == null) context = new VFCloneContext();
            return CloneWithChildren(context, child => child.Clone(context));
        }

        private VFMotion CloneWithChildren(VFCloneContext context, Func<VFTreeChild, VFTreeChild> copyChild) {
            if (context.Motions.TryGetValue(this, out var existing)) {
                return existing;
            }
            var output = new VFTree(
                sourceRaw as BlendTree
            );
            context.Motions[this] = output;
            output.treeName = treeName;
            output._blendType = _blendType;
            output.useAutomaticThresholds = useAutomaticThresholds;
            output.blendParameter = blendParameter;
            output.blendParameterY = blendParameterY;
            output.minThreshold = minThreshold;
            output.maxThreshold = maxThreshold;
            output.normalizedBlendValues = normalizedBlendValues;
            output._children = _children.Select(copyChild).ToList();
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
            return GetAllClips().All(clip => clip.IsStatic());
        }

        internal override bool IsTwoState() {
            return GetAllClips().All(clip => clip.IsTwoState());
        }

        internal override bool IsEmptyOrZeroLength() {
            return GetAllClips().All(clip => clip.IsEmptyOrZeroLength());
        }

        internal override VFClip FlattenToClip(VFMotionFlattenMode mode) {
            IEnumerable<VFClip> clips;
            if (mode == VFMotionFlattenMode.AllClips) {
                clips = GetAllClips();
            } else {
                clips = GetActiveClips(new HashSet<string> { VFBlendTreeDirect.AlwaysOneParam });
            }
            var flat = VFClip.Create(name);
            foreach (var clip in clips) {
                flat.CopyFrom(clip.FlattenToClip(mode));
            }
            return flat;
        }

        internal override VFMotion EvaluateMotion(float fraction) {
            var context = new VFCloneContext();
            var percent = Math.Round(fraction * 100);
            VFMotion SampleTree(VFTree tree) {
                if (context.Motions.TryGetValue(tree, out var existing)) return existing;
                var sampled = (VFTree)tree.CloneWithChildren(context, child => {
                    var copy = child.ShallowClone();
                    if (copy.motion is VFTree subtree) copy.motion = SampleTree(subtree);
                    // Each occurrence gets a separate sampled clip.
                    else if (copy.motion is VFClip clip) copy.motion = clip.EvaluateMotion(fraction);
                    return copy;
                });
                sampled.treeName = $"{tree.name} (sampled at {percent}%)";
                sampled.isDirty |= tree._children.Any(child => child.motion is VFClip);
                return sampled;
            }
            return SampleTree(this);
        }

        private IEnumerable<VFClip> GetAllClips() {
            var visited = new HashSet<VFTree>();
            var pending = new Stack<VFMotion>();
            pending.Push(this);
            while (pending.Count > 0) {
                var motion = pending.Pop();
                if (motion is VFClip clip) {
                    yield return clip;
                    continue;
                }
                if (!(motion is VFTree tree) || !visited.Add(tree)) continue;
                foreach (var child in tree.children.Reverse()) {
                    pending.Push(child.motion);
                }
            }
        }

        private IEnumerable<VFClip> GetActiveClips(HashSet<string> onParams) {
            var visited = new HashSet<VFTree>();
            var pending = new Stack<VFMotion>();
            pending.Push(this);
            while (pending.Count > 0) {
                var motion = pending.Pop();
                if (motion is VFClip clip) {
                    yield return clip;
                    continue;
                }
                if (!(motion is VFTree tree) || !visited.Add(tree)) continue;

                if (tree.blendType == BlendTreeType.Direct) {
                    foreach (var child in tree.children
                                 .Where(child => onParams.Contains(child.directBlendParameter))
                                 .Reverse()) {
                        pending.Push(child.motion);
                    }
                } else if (tree.blendType == BlendTreeType.Simple1D) {
                    var orderedChildren = tree.children
                        .Where(child => child.motion != null)
                        .OrderBy(child => child.threshold)
                        .ToArray();
                    if (orderedChildren.Any()) {
                        pending.Push((onParams.Contains(tree.BlendParameter)
                            ? orderedChildren.Last()
                            : orderedChildren.First()
                        ).motion);
                    }
                }
            }
        }
    }
}
