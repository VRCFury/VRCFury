using System;
using System.Collections.Generic;
using UnityEditor.Animations;
using UnityEngine;
using VF.Builder;
using VF.Feature.Base;
using VF.Inspector;
using VF.Model;
using VF.Model.Feature;
using VF.Model.StateAction;
using VRC.Dynamics;

namespace VF.Feature {
    public class BakeOGBBuilder : FeatureBuilder {
        [FeatureBuilderAction(FeatureOrder.BakeOgbComponents)]
        public void Apply() {
            var usedNames = new List<string>();
            foreach (var c in avatarObject.GetComponentsInChildren<OGBPenetrator>(true)) {
                OGBPenetratorEditor.Bake(c, usedNames);
            }
            foreach (var c in avatarObject.GetComponentsInChildren<OGBOrifice>(true)) {
                var (name,forward) = OGBOrificeEditor.Bake(c, usedNames);

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

                if (c.enableDepthAction && c.depthAction != null && !c.depthAction.IsEmpty()) {
                    var maxDepth = c.depthActionLength;
                    if (maxDepth <= 0) maxDepth = 0.25f;

                    var fx = GetFx();
                    var depthParam = fx.NewFloat(name + "/AnimDepth");
                    var contactingRootParam = fx.NewBool(name + "/AnimContacting");

                    OGBUtils.AddReceiver(c.gameObject, forward * -maxDepth, depthParam.Name(), "AnimInside", maxDepth, new []{OGBUtils.CONTACT_PEN_MAIN}, allowSelf:c.depthActionSelf, localOnly:true);
                    OGBUtils.AddReceiver(c.gameObject, Vector3.zero, contactingRootParam.Name(), "AnimRoot", 0.01f, new []{OGBUtils.CONTACT_PEN_MAIN}, allowSelf:c.depthActionSelf, localOnly:true, type: ContactReceiver.ReceiverType.Constant);

                    var layer = fx.NewLayer("Depth Animation for " + name);
                    var off = layer.NewState("Off");
                    var on = layer.NewState("On");

                    var clip = LoadState(name, c.depthAction);
                    var frames = ClipBuilder.GetLengthInFrames(clip);
                    if (frames <= 1) {
                        var tree = manager.GetClipStorage().NewBlendTree(name + " tree");
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
        }
    }
}
