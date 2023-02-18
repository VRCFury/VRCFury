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
                    var shaderOptimizer = ReflectionUtils.GetTypeFromAnyAssembly("Thry.ShaderOptimizer");
                    var unlockMethod = shaderOptimizer.GetMethod("Unlock", BindingFlags.NonPublic | BindingFlags.Static);
                    
                    Vector4 threeToFour(Vector3 a) =>new Vector4(a.x, a.y, a.z);

                    foreach (var skin in c.transform.GetComponentsInChildren<SkinnedMeshRenderer>()) {
                        var mats = skin.sharedMaterials;
                        for (var matI = 0; matI < mats.Length; matI++) {
                            var mat = mats[matI];
                            if (!mat.HasProperty("_TPSPenetratorEnabled")) continue;
                            if (mat.GetFloat("_TPSPenetratorEnabled") <= 0) continue;

                            mat = Object.Instantiate(mat);
                            VRCFuryAssetDatabase.SaveAsset(mat, tmpDir, "ogb_" + mat.name);
                            mats[matI] = mat;
                            
                            ReflectionUtils.CallWithOptionalParams(unlockMethod, null, mat);
                            var scale = skin.transform.lossyScale;
                            var rotation = Quaternion.LookRotation(forward);

                            mat.SetFloat("_TPS_PenetratorLength", length);
                            mat.SetVector("_TPS_PenetratorScale", threeToFour(scale));
                            mat.SetVector("_TPS_PenetratorRight", threeToFour(rotation * Vector3.right));
                            mat.SetVector("_TPS_PenetratorUp", threeToFour(rotation * Vector3.up));
                            mat.SetVector("_TPS_PenetratorForward", threeToFour(rotation * Vector3.forward));
                            mat.SetFloat("_TPS_IsSkinnedMeshRenderer", 1);
                            mat.EnableKeyword("TPS_IsSkinnedMesh");
                            
                            var bakeUtil = ReflectionUtils.GetTypeFromAnyAssembly("Thry.TPS.BakeToVertexColors");
                            var bakeMethod = bakeUtil.GetMethod("BakePositionsToTexture", new[] { typeof(Renderer), typeof(Texture2D) });
                            Texture2D tex = null;
                            VRCFuryAssetDatabase.WithoutAssetEditing(() => {
                                tex = (Texture2D)ReflectionUtils.CallWithOptionalParams(bakeMethod, null, skin, null);
                            });
                            if (string.IsNullOrWhiteSpace(AssetDatabase.GetAssetPath(tex))) {
                                throw new VRCFBuilderException("Failed to bake TPS texture");
                            }
                            mat.SetTexture("_TPS_BakedMesh", tex);
                        }
                        skin.sharedMaterials = mats;
                    }
                    foreach (var skin in c.transform.GetComponentsInChildren<MeshRenderer>()) {
                        var mats = skin.sharedMaterials;
                        for (var matI = 0; matI < mats.Length; matI++) {
                            var mat = skin.sharedMaterials[matI];
                            if (!mat.HasProperty("_TPSPenetratorEnabled")) continue;
                            if (mat.GetFloat("_TPSPenetratorEnabled") <= 0) continue;

                            mat = Object.Instantiate(mat);
                            VRCFuryAssetDatabase.SaveAsset(mat, tmpDir, "ogb_" + mat.name);
                            mats[matI] = mat;
                            
                            ReflectionUtils.CallWithOptionalParams(unlockMethod, null, mat);
                            var scale = skin.transform.lossyScale;
                            var rotation = Quaternion.LookRotation(forward);
                            
                            mat.SetFloat("_TPS_PenetratorLength", length);
                            mat.SetVector("_TPS_PenetratorScale", threeToFour(scale));
                            mat.SetVector("_TPS_PenetratorRight", threeToFour(rotation * Vector3.right));
                            mat.SetVector("_TPS_PenetratorUp", threeToFour(rotation * Vector3.up));
                            mat.SetVector("_TPS_PenetratorForward", threeToFour(rotation * Vector3.forward));
                            mat.SetFloat("_TPS_IsSkinnedMeshRenderer", 0);
                            mat.DisableKeyword("TPS_IsSkinnedMesh");
                        }
                        skin.sharedMaterials = mats;
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
