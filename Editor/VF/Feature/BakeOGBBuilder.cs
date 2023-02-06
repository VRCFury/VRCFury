using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor.Animations;
using UnityEngine;
using VF.Builder;
using VF.Feature.Base;
using VF.Inspector;
using VF.Model;
using VF.Model.Feature;
using VF.Model.StateAction;
using VRC.Dynamics;
using VRC.SDK3.Dynamics.Contact.Components;

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

                foreach (var r in c.gameObject.GetComponentsInChildren<VRCContactReceiver>()) {
                    objectsToDisableTemporarily.Add(r.gameObject);
                }
            }
            
            foreach (var c in avatarObject.GetComponentsInChildren<OGBOrifice>(true)) {
                fakeHead.MarkEligible(c.gameObject);
                var (name,forward) = OGBOrificeEditor.Bake(c, usedNames);
                
                foreach (var r in c.gameObject.GetComponentsInChildren<VRCContactReceiver>()) {
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
                var mid = layer.NewState("Mid");
                var on = layer.NewState("On");
                off.TransitionsTo(mid).When(fx.Always()).WithTransitionDurationSeconds(1);
                mid.TransitionsTo(on).When(fx.Always());
                
                var clip = manager.GetClipStorage().NewClip("ogbLoad");
                foreach (var obj in objectsToDisableTemporarily) {
                    clipBuilder.Enable(clip, obj, false);
                }
                on.WithAnimation(clip);
            }
        }
    }
}
