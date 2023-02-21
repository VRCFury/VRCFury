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
                var bakeInfo = OGBPenetratorEditor.Bake(c, usedNames);

                if (c.configureTps) {
                    Vector4 ThreeToFour(Vector3 a) => new Vector4(a.x, a.y, a.z);
                    
                    if (bakeInfo == null) {
                        throw new VRCFBuilderException("Failed to get size of penetrator to configure TPS");
                    }
                    var (name, bakeRoot, worldLength, worldRadius) = bakeInfo;

                    var configuredOne = false;

                    foreach (var renderer in c.transform.GetComponentsInChildren<Renderer>()) {
                        var root = new GameObject("OGBTPSShaderRoot");
                        root.transform.SetParent(bakeRoot.transform, false);
                        root.transform.SetParent(renderer.transform, true);
                        // TODO: Convert MeshRenderers into SkinnedMeshRenderers so we can take advantage of the base offset

                        var skin = renderer as SkinnedMeshRenderer;
                        var shaderRotation = skin ? Quaternion.identity : root.transform.localRotation;
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
                            var localScale = skin ? root.transform.lossyScale : renderer.transform.lossyScale;

                            mat.SetFloat(TpsPenetratorLength, worldLength);
                            mat.SetVector(TpsPenetratorScale, ThreeToFour(localScale));
                            mat.SetVector(TpsPenetratorRight, ThreeToFour(shaderRotation * Vector3.right));
                            mat.SetVector(TpsPenetratorUp, ThreeToFour(shaderRotation * Vector3.up));
                            mat.SetVector(TpsPenetratorForward, ThreeToFour(shaderRotation * Vector3.forward));

                            if (skin) {
                                mat.SetFloat(TpsIsSkinnedMeshRenderer, 1);
                                mat.EnableKeyword(TpsIsSkinnedMeshKeyword);

                                var bakeUtil = ReflectionUtils.GetTypeFromAnyAssembly("Thry.TPS.BakeToVertexColors");
                                var meshInfoType = bakeUtil.GetNestedType("MeshInfo");
                                var meshInfo = Activator.CreateInstance(meshInfoType);
                                var bakedMesh = MeshBaker.BakeMesh(skin, root.transform);
                                if (bakedMesh == null)
                                    throw new VRCFBuilderException("Failed to bake mesh for TPS configuration"); 
                                meshInfoType.GetField("bakedVertices").SetValue(meshInfo, bakedMesh.vertices);
                                meshInfoType.GetField("bakedNormals").SetValue(meshInfo, bakedMesh.normals);
                                meshInfoType.GetField("ownerRenderer").SetValue(meshInfo, skin);
                                meshInfoType.GetField("sharedMesh").SetValue(meshInfo, skin.sharedMesh);
                                var bakeMethod = bakeUtil.GetMethod(
                                    "BakePositionsToTexture", 
                                    BindingFlags.Static | BindingFlags.NonPublic | BindingFlags.Public,
                                    null,
                                    new[] { meshInfoType, typeof(Texture2D) },
                                    null
                                );
                                Texture2D tex = null;
                                VRCFuryAssetDatabase.WithoutAssetEditing(() => {
                                    tex = (Texture2D)ReflectionUtils.CallWithOptionalParams(bakeMethod, null, meshInfo, null);
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
                            if (skin) {
                                skin.rootBone = root.transform;
                                addOtherFeature(new BoundingBoxFix2() { singleRenderer = skin });
                            }
                            EditorUtility.SetDirty(renderer);
                        }
                    }

                    if (!configuredOne) {
                        throw new VRCFBuilderException(
                            "OGB Penetrator has 'auto-configure TPS' enabled, but there were no meshes found inside " +
                            "using Poiyomi Pro 8.1+ with the 'Penetrator' feature enabled.");
                    }
                }

                if (bakeInfo != null) {
                    var (name, bakeRoot, worldLength, worldRadius) = bakeInfo;
                    foreach (var r in bakeRoot.GetComponentsInChildren<VRCContactReceiver>(true)) {
                        objectsToDisableTemporarily.Add(r.gameObject);
                    }
                }
            }

            var enableAuto = avatarObject.GetComponentsInChildren<OGBOrifice>(true)
                .Where(o => o.addMenuItem && o.enableAuto)
                .ToArray()
                .Length >= 2;
            VFABool autoOn = null;
            AnimationClip autoOnClip = null;
            if (enableAuto) {
                autoOn = GetFx().NewBool("autoMode");
                manager.GetMenu().NewMenuToggle("Holes/Auto", autoOn);
                autoOnClip = manager.GetClipStorage().NewClip("EnableAutoReceivers");
                var autoReceiverLayer = GetFx().NewLayer("Auto - Enable Receivers");
                var off = autoReceiverLayer.NewState("Off");
                var on = autoReceiverLayer.NewState("On").WithAnimation(autoOnClip);
                var whenOn = autoOn.IsTrue().And(GetFx().IsLocal().IsTrue());
                off.TransitionsTo(on).When(whenOn);
                on.TransitionsTo(off).When(whenOn.Not());
            }
            
            var enableStealth = avatarObject.GetComponentsInChildren<OGBOrifice>(true)
                .Where(o => o.addMenuItem)
                .ToArray()
                .Length >= 1;
            VFABool stealthOn = null;
            if (enableStealth) {
                stealthOn = GetFx().NewBool("stealth");
                manager.GetMenu().NewMenuToggle("Holes/Stealth", stealthOn);
            }

            var autoOrifices = new List<Tuple<string, VFABool, VFANumber>>();
            var exclusiveTriggers = new List<Tuple<VFABool, VFAState>>();
            foreach (var c in avatarObject.GetComponentsInChildren<OGBOrifice>(true)) {
                fakeHead.MarkEligible(c.gameObject);
                var (name,bakeRoot) = OGBOrificeEditor.Bake(c, usedNames);
                
                foreach (var r in bakeRoot.GetComponentsInChildren<VRCContactReceiver>(true)) {
                    objectsToDisableTemporarily.Add(r.gameObject);
                }

                if (c.addMenuItem) {
                    c.gameObject.SetActive(true);

                    ICollection<GameObject> FindChildren(params string[] names) {
                        return names.Select(n => bakeRoot.transform.Find(n))
                            .Where(t => t != null)
                            .Select(t => t.gameObject)
                            .ToArray();
                    }

                    foreach (var obj in FindChildren("Senders", "Receivers", "Lights", "VersionLocal", "VersionBeacon")) {
                        obj.SetActive(false);
                    }
                    var onLocalClip = manager.GetClipStorage().NewClip($"{name}_onLocal");
                    foreach (var obj in FindChildren("Senders", "Receivers", "Lights", "VersionLocal")) {
                        clipBuilder.Enable(onLocalClip, obj);
                    }
                    var onRemoteClip = manager.GetClipStorage().NewClip($"{name}_onRemote");
                    foreach (var obj in FindChildren("Senders", "Lights", "VersionBeacon")) {
                        clipBuilder.Enable(onRemoteClip, obj);
                    }
                    var onStealthClip = manager.GetClipStorage().NewClip($"{name}_stealth");
                    foreach (var obj in FindChildren("Receivers", "VersionLocal")) {
                        clipBuilder.Enable(onStealthClip, obj);
                    }

                    var holeOn = GetFx().NewBool(name, synced: true);
                    manager.GetMenu().NewMenuToggle($"Holes/{name}", holeOn);

                    var layer = GetFx().NewLayer(name);
                    var offState = layer.NewState("Off");
                    var onLocalState = layer.NewState("On Local").WithAnimation(onLocalClip).Move(offState, 1, 0);
                    var onRemoteState = layer.NewState("On Remote").WithAnimation(onRemoteClip);
                    var stealthState = layer.NewState("Stealth").WithAnimation(onStealthClip);

                    var whenOn = holeOn.IsTrue();
                    var whenOnAndLocal = whenOn.And(GetFx().IsLocal().IsTrue());
                    var whenStealthEnabled = stealthOn?.IsTrue() ?? GetFx().Never();

                    var whenStealth = whenOnAndLocal.And(whenStealthEnabled);
                    var whenOnLocal = whenOnAndLocal.And(whenStealth.Not());
                    var whenOnRemote = whenOn.And(whenStealthEnabled.Not()).And(whenStealth.Not()).And(whenOnLocal.Not());
                    var whenOff = whenStealth.Not().And(whenOnLocal.Not()).And(whenOnRemote.Not());

                    foreach (var state in new[] { offState, onLocalState, onRemoteState, stealthState }) {
                        if (state != offState) state.TransitionsTo(offState).When(whenOff);
                        if (state != onLocalState) state.TransitionsTo(onLocalState).When(whenOnLocal);
                        if (state != onRemoteState) state.TransitionsTo(onRemoteState).When(whenOnRemote);
                        if (state != stealthState) state.TransitionsTo(stealthState).When(whenStealth);
                    }

                    exclusiveTriggers.Add(Tuple.Create(holeOn, onLocalState));

                    if (c.enableAuto) {
                        var distParam = GetFx().NewFloat(name + "/AutoDistance");
                        var distReceiver = OGBUtils.AddReceiver(bakeRoot, Vector3.zero, distParam.Name(), "AutoDistance", 0.3f,
                            new[] { OGBUtils.CONTACT_PEN_MAIN });
                        distReceiver.SetActive(false);
                        clipBuilder.Enable(autoOnClip, distReceiver);
                        autoOrifices.Add(Tuple.Create(name, holeOn, distParam));
                    }
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
                    OGBUtils.AddReceiver(bakeRoot, Vector3.forward * -minDepth, contactingRootParam.Name(), "AnimRoot" + actionNum, 0.01f, new []{OGBUtils.CONTACT_PEN_MAIN}, allowSelf:depthAction.enableSelf, type: ContactReceiver.ReceiverType.Constant);
                    
                    var depthParam = fx.NewFloat(prefix + "/AnimDepth");
                    OGBUtils.AddReceiver(bakeRoot, Vector3.forward * -(minDepth + length), depthParam.Name(), "AnimInside" + actionNum, length, new []{OGBUtils.CONTACT_PEN_MAIN}, allowSelf:depthAction.enableSelf);

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

            foreach (var i in Enumerable.Range(0, exclusiveTriggers.Count)) {
                var (_, state) = exclusiveTriggers[i];
                foreach (var j in Enumerable.Range(0, exclusiveTriggers.Count)) {
                    if (i == j) continue;
                    var (param, _) = exclusiveTriggers[j];
                    state.Drives(param, false);
                }
            }

            if (autoOn != null) {
                var fx = GetFx();
                var layer = fx.NewLayer("Auto OGB Mode");
                var remoteTrap = layer.NewState("Remote trap");
                var stopped = layer.NewState("Stopped");
                remoteTrap.TransitionsTo(stopped).When(fx.IsLocal().IsTrue());
                var start = layer.NewState("Start").Move(stopped, 1, 0);
                stopped.TransitionsTo(start).When(autoOn.IsTrue());
                var stop = layer.NewState("Stop").Move(start, 1, 0);
                start.TransitionsTo(stop).When(autoOn.IsFalse());
                foreach (var auto in autoOrifices) {
                    var (name, enabled, dist) = auto;
                    stop.Drives(enabled, false);
                }
                stop.TransitionsTo(stopped).When(fx.Always());

                var vsParam = fx.NewFloat("comparison");
                var vs1 = manager.GetClipStorage().NewClip("vs1");
                vs1.SetCurve("", typeof(Animator), vsParam.Name(), AnimationCurve.Constant(0, 0, 1f));
                var vs0 = manager.GetClipStorage().NewClip("vs0");
                vs0.SetCurve("", typeof(Animator), vsParam.Name(), AnimationCurve.Constant(0, 0, 0f));

                var states = new Dictionary<Tuple<int, int>, VFAState>();
                for (var i = 0; i < autoOrifices.Count; i++) {
                    var (aName, aEnabled, aDist) = autoOrifices[i];
                    var triggerOn = layer.NewState($"Start {aName}").Move(start, i, 2);
                    triggerOn.Drives(aEnabled, true);
                    states[Tuple.Create(i,-1)] = triggerOn;
                    var triggerOff = layer.NewState($"Stop {aName}");
                    triggerOff.Drives(aEnabled, false);
                    triggerOff.TransitionsTo(start).When(fx.Always());
                    states[Tuple.Create(i,-2)] = triggerOff;
                    for (var j = 0; j < autoOrifices.Count; j++) {
                        if (i == j) continue;
                        var (bName, bEnabled, bDist) = autoOrifices[j];
                        var vs = layer.NewState($"{aName} vs {bName}").Move(triggerOff, 0, j+1);
                        var tree = manager.GetClipStorage().NewBlendTree($"{aName} vs {bName}");
                        tree.useAutomaticThresholds = false;
                        tree.blendType = BlendTreeType.FreeformCartesian2D;
                        tree.AddChild(vs0, new Vector2(1f, 0));
                        tree.AddChild(vs1, new Vector2(0, 1f));
                        tree.blendParameter = aDist.Name();
                        tree.blendParameterY = bDist.Name();
                        vs.WithAnimation(tree);
                        states[Tuple.Create(i,j)] = vs;
                    }
                }
                
                for (var i = 0; i < autoOrifices.Count; i++) {
                    var (name, enabled, dist) = autoOrifices[i];
                    var triggerOn = states[Tuple.Create(i, -1)];
                    var triggerOff = states[Tuple.Create(i, -2)];
                    var firstComparison = states[Tuple.Create(i, i == 0 ? 1 : 0)];
                    start.TransitionsTo(firstComparison).When(enabled.IsTrue());
                    triggerOn.TransitionsTo(firstComparison).When(fx.Always());
                    
                    for (var j = 0; j < autoOrifices.Count; j++) {
                        if (i == j) continue;
                        var current = states[Tuple.Create(i, j)];
                        var otherActivate = states[Tuple.Create(j, -1)];

                        current.TransitionsTo(otherActivate).When(vsParam.IsGreaterThan(0.51f));
                        
                        var nextI = j + 1;
                        if (nextI == i) nextI++;
                        if (nextI == autoOrifices.Count) {
                            current.TransitionsTo(triggerOff).When(dist.IsGreaterThan(0).Not());
                            current.TransitionsTo(start).When(fx.Always());
                        } else {
                            var next = states[Tuple.Create(i, nextI)];
                            current.TransitionsTo(next).When(fx.Always());
                        }
                    }
                }

                start.TransitionsTo(states[Tuple.Create(0, 1)]).When(fx.Always());
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
