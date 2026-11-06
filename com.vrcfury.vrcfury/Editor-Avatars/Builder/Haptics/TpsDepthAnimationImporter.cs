using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;
using VF.Component;
using VF.Injector;
using VF.Model;
using VF.Model.StateAction;
using VF.Service;
using VF.Utils;
using VF.Utils.Controller;
using VRC.SDK3.Avatars.Components;
using VRC.SDK3.Dynamics.Contact.Components;

namespace VF.Builder.Haptics {
    [VFService]
    internal class TpsDepthAnimationImporter {
        [VFAutowired] private readonly VRCFuryInjector injector;
        [VFAutowired] private readonly GlobalsService globals;
        private static bool ENABLED = false;

        public class Result {
            public readonly HashSet<string> layers = new HashSet<string>();
            public readonly HashSet<string> parameters = new HashSet<string>();
            public readonly HashSet<UnityEngine.Component> contacts = new HashSet<UnityEngine.Component>();
            public readonly List<string> descriptions = new List<string>();
        }

        public Result Import(VFGameObject root, IEnumerable<VFGameObject> plugs,
            IEnumerable<VFGameObject> sockets, bool dryRun) {
            var result = new Result();
            if (!ENABLED) return result;
            if (root.GetComponent<VRCAvatarDescriptor>() == null) return result;
            var owners = plugs.Select(p => (owner: p, actions: p.GetComponent<VRCFuryHapticPlug>()?.depthActions2, plug: true))
                .Concat(sockets.Select(s => (owner: s, actions: s.GetComponent<VRCFuryHapticSocket>()?.depthActions2, plug: false))).ToArray();
            var receivers = root.GetComponentsInSelfAndChildren<VRCContactReceiver>();
            VFAvatarController[] controllers = null;
            foreach (var receiver in receivers) {
                var match = Regex.Match(receiver.parameter ?? "", @"TPS_Internal/(Orf|Pen)/(\d+)/(Depth_In|RootRoot)$");
                if (!match.Success || receiver.radius <= 0 || !receiver.allowOthers) continue;
                var plug = match.Groups[1].Value == "Pen";
                if (match.Groups[3].Value != (plug ? "RootRoot" : "Depth_In")) continue;
                if (!receiver.collisionTags.Contains(plug ? SpsTags.LegacySocket : SpsTags.LegacyPlugSphere)) continue;
                var parent = receiver.owner().parent;
                if (parent == null) continue;
                var candidates = owners.Where(o => o.plug == plug
                    && (o.owner.IsSameOrChildOf(parent) || parent.IsSameOrChildOf(o.owner))).ToArray();
                if (candidates.Length != 1) continue;
                var owner = candidates[0];
                var id = match.Groups[2].Value;
                var internalPrefix = receiver.parameter.Substring(0, match.Index) + "TPS_Internal/" + (plug ? "Pen/" : "Orf/") + id + "/";
                var externalPrefix = receiver.parameter.Substring(0, match.Index) + (plug ? "TPS_Pen_" : "TPS_Orf_") + id + "_";
                bool IsDepth(string param) => param == receiver.parameter || (plug && param == externalPrefix + "BufferedDepth");
                bool IsOwnedParam(string param) => param != null && (param.StartsWith(internalPrefix) || param.StartsWith(externalPrefix));
                controllers ??= injector.GetService<ControllersService>().GetAllUsedControllers().ToArray();
                foreach (var controller in controllers) {
                    var imported = false;
                    bool HasUserAnimation(VFLayer layer) {
                        var bindings = new AnimatorIterator.Clips().From(layer).SelectMany(c => c.GetFloatBindings()).ToArray();
                        if (!bindings.Any(KeepBinding)) return false;
                        return bindings.Any(b => b.IsAnimatorBinding() && IsOwnedParam(b.propertyName))
                            || new AnimatorIterator.Trees().From(layer).Any(t => IsOwnedParam(t.BlendParameter) || IsOwnedParam(t.BlendParameterY))
                            || layer.allTransitions.SelectMany(t => t.conditions).Any(c => IsOwnedParam(c.parameter))
                            || layer.allStates.Any(s => IsOwnedParam(s.timeParameter) || IsOwnedParam(s.speedParameter)
                                || IsOwnedParam(s.cycleOffsetParameter) || IsOwnedParam(s.mirrorParameter));
                    }
                    foreach (var layer in controller.GetLayers()) {
                        if (result.layers.Contains(layer.name)) continue;
                        // Cleanup identifies layers by name; do not let a duplicate name delete another controller's layer.
                        if (controllers.Sum(c => c.GetLayers().Count(l => l.name == layer.name)) != 1) continue;
                        var trees = layer.allStates.Select(s => s.motion).OfType<VFTree>()
                            .Where(t => t.blendType == BlendTreeType.Simple1D && IsDepth(t.BlendParameter)).Distinct().ToArray();
                        if (trees.Length != 1) continue;
                        // Do not strand another user animation that still needs this group's old TPS inputs.
                        if (controller.GetLayers().Any(other => other != layer && HasUserAnimation(other))) continue;
                        var tree = trees[0];
                        var frames = tree.children.OrderBy(c => c.threshold)
                            .Select(c => (depth: c.threshold, clip: AtFullWidth(c.motion, externalPrefix + "Width"))).ToArray();
                        if (frames.Length < 2 || frames.Any(f => f.clip == null)
                            || frames.Last().depth <= frames.First().depth) continue;
                        // Extra animated states or unrelated curves are not a wizard depth animation.
                        if (layer.allStates.Any(s => s.motion != null && s.motion != tree
                            && (!(s.motion is VFClip off) || !IsConstant(off) || off.GetFloatCurves()
                                .Any(c => KeepBinding(c.Item1) && c.Item2.Evaluate(0) != 0)))) continue;
                        var bindings = frames.SelectMany(f => f.clip.GetFloatBindings()).Where(KeepBinding).Distinct().ToArray();
                        if (bindings.Length == 0) continue;
                        var clip = VFClip.Create("SPS - Imported TPS Depth");
                        foreach (var binding in bindings) {
                            binding.TryGetCurrentFloat(root, out var defaultValue);
                            var keys = frames.Select(f => new Keyframe(
                                (f.depth - frames.First().depth) / (frames.Last().depth - frames.First().depth),
                                f.clip.GetFloatCurve(binding)?.Evaluate(0) ?? defaultValue)).ToArray();
                            clip.SetCurve(binding, new AnimationCurve(keys));
                        }
                        clip.UseLinearTangents();
                        if (!dryRun) {
                            var action = new AnimationClipAction();
                            if (globals.useOriginalAssets) {
                                // Absolute avatar paths also work when the renderer is outside the socket/plug object.
                                clip.Rewrite(AnimationRewriter.RewriteBinding(b => b.WithPath("/" + b.GetPath(root))));
                                var asset = (AnimationClip)clip.Save(root);
                                var sourcePath = AssetDatabase.GetAssetPath(controller.GetSourceAsset());
                                var path = sourcePath.StartsWith("Assets/")
                                    ? Path.GetDirectoryName(sourcePath) + "/" + Path.GetFileNameWithoutExtension(sourcePath) + " SPS Depth.anim"
                                    : "Assets/SPS Depth.anim";
                                asset.hideFlags = HideFlags.None;
                                AssetDatabase.CreateAsset(asset, AssetDatabase.GenerateUniqueAssetPath(path));
                                action.clip = asset;
                            } else {
                                action.vfClip = clip;
                            }
                            var radius = receiver.radius * receiver.owner().worldScale.x / owner.owner.worldScale.x;
                            owner.actions.Add(new VRCFuryHapticSocket.DepthActionNew {
                                actionSet = new State { actions = { action } },
                                range = new Vector2(-frames.Last().depth * radius, -frames.First().depth * radius),
                                units = VRCFuryHapticSocket.DepthActionUnits.Local,
                                enableSelf = receiver.allowSelf
                            });
                        }
                        result.layers.Add(layer.name);
                        result.descriptions.Add(owner.owner.GetPath(root));
                        imported = true;
                    }
                    var component = plug ? owner.owner.GetComponent<VRCFuryHapticPlug>() : null;
                    var replacesShader = plug && (component != null && component.enableSps
                        || owner.owner.GetComponentsInSelfAndChildren<Renderer>().Any(TpsUtils.HasDpsOrTpsMaterial));
                    if ((imported || replacesShader) && !controller.GetLayers().Any(l => !result.layers.Contains(l.name) && HasUserAnimation(l))) {
                        // SPS deformation also replaces the stock TPS plug buffer, which has no user depth actions.
                        // Keep layers containing user animation instead of deleting them with the obsolete shader logic.
                        foreach (var mathLayer in controller.GetLayers()) {
                            if (!mathLayer.name.Contains("[TPS]")
                                || controllers.Sum(c => c.GetLayers().Count(l => l.name == mathLayer.name)) != 1) continue;
                            var clips = new AnimatorIterator.Clips().From(mathLayer);
                            var bindings = clips.SelectMany(c => c.GetFloatBindings()).ToArray();
                            bool IsLegacyBinding(VFBinding binding) => binding.IsAnimatorBinding()
                                ? IsOwnedParam(binding.propertyName)
                                : binding.target != null && binding.target.IsSameOrChildOf(parent)
                                    && (binding.propertyName.StartsWith("material._TPS_")
                                        || binding.propertyName == "m_UpdateWhenOffscreen" || IsLegacySenderBinding(binding));
                            if (bindings.Any(b => b.IsAnimatorBinding() && IsOwnedParam(b.propertyName))
                                && bindings.All(IsLegacyBinding)
                                && (bindings.All(b => b.IsAnimatorBinding()) || clips.All(IsConstant))) {
                                result.layers.Add(mathLayer.name);
                                imported = true;
                            }
                        }
                    }
                    if (imported) {
                        result.parameters.UnionWith(controller.parameters.Select(p => p.name).Where(IsOwnedParam));
                    }
                }
                result.contacts.UnionWith(receivers.Where(r => result.parameters.Contains(r.parameter)));
            }
            return result;
        }

        private static bool KeepBinding(VFBinding binding) => !binding.IsAnimatorBinding() && binding.target != null
            && !binding.propertyName.StartsWith("material._TPS_") && binding.propertyName != "m_UpdateWhenOffscreen"
            && !IsLegacySenderBinding(binding);

        private static bool IsLegacySenderBinding(VFBinding binding) => binding.type == typeof(VRCContactSender)
            && binding.propertyName == "m_Enabled" && binding.target != null
            && binding.target.GetComponents<VRCContactSender>().Any(sender => sender.collisionTags
                .Any(tag => tag == SpsTags.LegacyPlugSphere || tag == SpsTags.LegacyPlugWidth));

        // TPS writes constant poses with a single key at time one. VFClip.IsStatic also requires a time-zero key.
        private static bool IsConstant(VFClip clip) => clip.GetObjectBindings().Length == 0
            && clip.GetFloatCurves().All(c => c.Item2.keys.Select(k => k.value).Distinct().Count() <= 1);

        private static VFClip AtFullWidth(VFMotion motion, string widthParameter) {
            var visited = new HashSet<VFMotion>();
            while (motion is VFTree tree && visited.Add(motion)) {
                if (tree.blendType != BlendTreeType.Simple1D || tree.BlendParameter != widthParameter) return null;
                motion = tree.children.OrderBy(c => c.threshold).LastOrDefault()?.motion;
            }
            return motion is VFClip clip && IsConstant(clip) ? clip : null;
        }
    }
}
