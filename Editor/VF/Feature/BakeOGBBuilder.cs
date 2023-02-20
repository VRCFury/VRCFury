using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;
using VF.Builder;
using VF.Builder.Exceptions;
using VF.Feature.Base;
using VF.Inspector;
using VF.Model;
using VF.Model.Feature;
using VF.Model.StateAction;
using VRC.Dynamics;
using VRC.SDK3.Dynamics.Contact.Components;
using Object = UnityEngine.Object;

namespace VF.Feature {
    public class BakeOGBBuilder : FeatureBuilder {
        private static readonly int TpsPenetratorEnabled = Shader.PropertyToID("_TPSPenetratorEnabled");
        private static readonly int TpsPenetratorLength = Shader.PropertyToID("_TPS_PenetratorLength");
        private static readonly int TpsPenetratorScale = Shader.PropertyToID("_TPS_PenetratorScale");
        private static readonly int TpsPenetratorRight = Shader.PropertyToID("_TPS_PenetratorRight");
        private static readonly int TpsPenetratorUp = Shader.PropertyToID("_TPS_PenetratorUp");
        private static readonly int TpsPenetratorForward = Shader.PropertyToID("_TPS_PenetratorForward");
        private static readonly int TpsIsSkinnedMeshRenderer = Shader.PropertyToID("_TPS_IsSkinnedMeshRenderer");
        private static readonly string TpsIsSkinnedMeshKeyword = "TPS_IsSkinnedMesh";
        private static readonly int TpsBakedMesh = Shader.PropertyToID("_TPS_BakedMesh");

        [FeatureBuilderAction(FeatureOrder.BakeOgbComponents)]
        public void Apply() {
            var usedNames = new List<string>();
            var fakeHead = allBuildersInRun.OfType<FakeHeadBuilder>().First();

            // When you first load into a world, contact receivers already touching a sender register as 0 proximity
            // until they are removed and then reintroduced to each other.
            var objectsToDisableTemporarily = new HashSet<GameObject>();
            
            foreach (var c in avatarObject.GetComponentsInChildren<OGBPenetrator>(true)) {
                OGBPenetratorEditor.Bake(c, usedNames);
                
                if (c.configureTps) {
                    var size = OGBPenetratorEditor.GetSize(c);
                    if (size == null) {
                        throw new VRCFBuilderException("Failed to get size of penetrator to configure TPS");
                    }
                    var (length, radius, forward) = size;

                    Vector4 ThreeToFour(Vector3 a) => new Vector4(a.x, a.y, a.z);
                    
                    var root = new GameObject("OGB_TPSShaderBase");
                    root.transform.SetParent(c.transform, false);
                    root.transform.localRotation = Quaternion.LookRotation(forward);

                    var configuredOne = false;

                    foreach (var renderer in c.transform.GetComponentsInChildren<Renderer>()) {
                        var skin = renderer as SkinnedMeshRenderer;
                        var shaderRotation = skin ? Quaternion.identity : Quaternion.LookRotation(forward);
                        var mats = renderer.sharedMaterials;
                        var replacedAMat = false;
                        for (var matI = 0; matI < mats.Length; matI++) {
                            var mat = mats[matI];
                            if (!mat.HasProperty(TpsPenetratorEnabled)) continue;
                            if (mat.GetFloat(TpsPenetratorEnabled) <= 0) continue;

                            mat = Object.Instantiate(mat);
                            VRCFuryAssetDatabase.SaveAsset(mat, tmpDir, "ogb_" + mat.name);
                            mats[matI] = mat;

                            var shaderOptimizer = ReflectionUtils.GetTypeFromAnyAssembly("Thry.ShaderOptimizer");
                            if (shaderOptimizer == null) {
                                throw new VRCFBuilderException(
                                    "OGB Penetrator has 'auto-configure TPS' checked, but Poiyomi Pro TPS does not seem to be imported in project.");
                            }
                            var unlockMethod = shaderOptimizer.GetMethod("Unlock", BindingFlags.NonPublic | BindingFlags.Static);
                            VRCFuryAssetDatabase.WithoutAssetEditing(() => {
                                ReflectionUtils.CallWithOptionalParams(unlockMethod, null, mat);
                            });
                            var scale = renderer.transform.lossyScale;

                            mat.SetFloat(TpsPenetratorLength, length);
                            mat.SetVector(TpsPenetratorScale, ThreeToFour(scale));
                            mat.SetVector(TpsPenetratorRight, ThreeToFour(shaderRotation * Vector3.right));
                            mat.SetVector(TpsPenetratorUp, ThreeToFour(shaderRotation * Vector3.up));
                            mat.SetVector(TpsPenetratorForward, ThreeToFour(shaderRotation * Vector3.forward));

                            if (skin) {
                                mat.SetFloat(TpsIsSkinnedMeshRenderer, 1);
                                mat.EnableKeyword(TpsIsSkinnedMeshKeyword);
                            
                                var bakeUtil = ReflectionUtils.GetTypeFromAnyAssembly("Thry.TPS.BakeToVertexColors");
                                var bakeMethod = bakeUtil.GetMethod("BakePositionsToTexture", new[] { typeof(Renderer), typeof(Texture2D) });
                                Texture2D tex = null;
                                VRCFuryAssetDatabase.WithoutAssetEditing(() => {
                                    tex = (Texture2D)ReflectionUtils.CallWithOptionalParams(bakeMethod, null, renderer, null);
                                });
                                if (string.IsNullOrWhiteSpace(AssetDatabase.GetAssetPath(tex))) {
                                    throw new VRCFBuilderException("Failed to bake TPS texture");
                                }
                                mat.SetTexture(TpsBakedMesh, tex);
                            } else {
                                mat.SetFloat(TpsIsSkinnedMeshRenderer, 0);
                                mat.DisableKeyword(TpsIsSkinnedMeshKeyword);
                            }

                            EditorUtility.SetDirty(mat);
                            replacedAMat = true;
                            configuredOne = true;
                        }

                        if (replacedAMat) {
                            renderer.sharedMaterials = mats;
                            if (skin) skin.rootBone = root.transform;
                            EditorUtility.SetDirty(renderer);
                        }
                    }

                    if (!configuredOne) {
                        throw new VRCFBuilderException(
                            "OGB Penetrator has 'auto-configure TPS' enabled, but there were no meshes found inside " +
                            "using Poiyomi Pro 8.1+ with the 'Penetrator' feature enabled.");
                    }
                }

                foreach (var r in c.gameObject.GetComponentsInChildren<VRCContactReceiver>(true)) {
                    objectsToDisableTemporarily.Add(r.gameObject);
                }
            }
            
            foreach (var c in avatarObject.GetComponentsInChildren<OGBOrifice>(true)) {
                fakeHead.MarkEligible(c.gameObject);
                var (name,forward) = OGBOrificeEditor.Bake(c, usedNames);
                
                foreach (var r in c.gameObject.GetComponentsInChildren<VRCContactReceiver>(true)) {
                    objectsToDisableTemporarily.Add(r.gameObject);
                }

                if (c.addMenuItem) {
                    c.gameObject.SetActive(false);
                    addOtherFeature(new Toggle() {
                        name = "Holes/" + name,
                        state = new State() {
                            actions = {
                                new ObjectToggleAction() {
                                    obj = c.gameObject
                                }
                            }
                        },
                        enableExclusiveTag = true,
                        exclusiveTag = "OGBOrificeToggles"
                    });
                }

                var actionNum = 0;
                foreach (var depthAction in c.depthActions) {
                    actionNum++;
                    var prefix = name + actionNum;
                    if (depthAction.state == null || depthAction.state.IsEmpty()) continue;

                    var minDepth = depthAction.minDepth;

                    var maxDepth = depthAction.maxDepth;
                    if (maxDepth <= minDepth) maxDepth = 0.25f;
                    if (maxDepth <= minDepth) continue;

                    var length = maxDepth - minDepth;

                    var fx = GetFx();

                    var contactingRootParam = fx.NewBool(prefix + "/AnimContacting");
                    OGBUtils.AddReceiver(c.gameObject, forward * -minDepth, contactingRootParam.Name(), "AnimRoot" + actionNum, 0.01f, new []{OGBUtils.CONTACT_PEN_MAIN}, allowSelf:depthAction.enableSelf, type: ContactReceiver.ReceiverType.Constant);
                    
                    var depthParam = fx.NewFloat(prefix + "/AnimDepth");
                    OGBUtils.AddReceiver(c.gameObject, forward * -(minDepth + length), depthParam.Name(), "AnimInside" + actionNum, length, new []{OGBUtils.CONTACT_PEN_MAIN}, allowSelf:depthAction.enableSelf);

                    var layer = fx.NewLayer("Depth Animation " + actionNum + " for " + name);
                    var off = layer.NewState("Off");
                    var on = layer.NewState("On");

                    var clip = LoadState(prefix, depthAction.state);
                    var frames = ClipBuilder.GetLengthInFrames(clip);
                    if (frames <= 1) {
                        var tree = manager.GetClipStorage().NewBlendTree(prefix + " tree");
                        tree.blendType = BlendTreeType.Simple1D;
                        tree.useAutomaticThresholds = false;
                        tree.blendParameter = depthParam.Name();
                        tree.AddChild(manager.GetClipStorage().GetNoopClip(), 0);
                        tree.AddChild(clip, 1);
                        on.WithAnimation(tree);
                    } else {
                        on.WithAnimation(clip).MotionTime(depthParam);
                    }

                    var onWhen = depthParam.IsGreaterThan(0).And(contactingRootParam.IsTrue());
                    off.TransitionsTo(on).When(onWhen);
                    on.TransitionsTo(off).When(onWhen.Not());
                }
            }

            if (objectsToDisableTemporarily.Count > 0) {
                var fx = GetFx();
                var layer = fx.NewLayer("OGB Off Temporarily Upon Load");
                var off = layer.NewState("Off");
                var on = layer.NewState("On");
                off.TransitionsTo(on).When().WithTransitionExitTime(1);
                
                var clip = manager.GetClipStorage().NewClip("ogbLoad");
                foreach (var obj in objectsToDisableTemporarily) {
                    clipBuilder.Enable(clip, obj, false);
                }
                off.WithAnimation(clip);
            }
        }
    }
}
