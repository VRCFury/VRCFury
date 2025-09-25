using System;
using UnityEngine;
using VF.Service;
using VF.Utils;
using VF.Utils.Controller;

namespace VF.Builder.Haptics {
    internal class SpsDepthContacts {
        public readonly TipRootPair self;
        public readonly TipRootPair others;
        public readonly Lazy<VFAFloat> closestLength;
        public readonly Lazy<VFAFloat> closestRadius;
        public readonly Lazy<VFAFloat> closestDistanceMeters;
        public readonly Lazy<VFAFloat> closestDistancePlugLengths;
        public readonly Lazy<VFAFloat> closestDistanceLocal;
        public readonly Lazy<VFAFloat> velocity;
        public readonly VFBlendTreeDirect directTree;

        public SpsDepthContacts(
            VFGameObject parent,
            string paramPrefix,
            HapticContactsService hapticContactsService,
            VFBlendTreeDirect directTree,
            BlendtreeMath math,
            ControllerManager controller,
            FrameTimeService frameTimeService,
            bool useHipAvoidance,
            VFAFloat scaleFactor,
            float inputPlugLength = -1
        ) {
            this.directTree = directTree;
            self = new TipRootPair(parent, paramPrefix + "/Self", "Self", hapticContactsService, directTree, math, controller, useHipAvoidance, HapticUtils.ReceiverParty.Self, scaleFactor, inputPlugLength);
            others = new TipRootPair(parent, paramPrefix + "/Others", "Others", hapticContactsService, directTree, math, controller, useHipAvoidance, HapticUtils.ReceiverParty.Others, scaleFactor, inputPlugLength);
            var whoIsClosest = new Lazy<(VFAFloat isSelf,VFAFloat isOthers)>(() => {
                var isSelf = controller.MakeAap(paramPrefix + "/Closest/IsSelf", def: 1);
                var isOthers = controller.MakeAap(paramPrefix + "/Closest/IsOthers");
                directTree.Add(BlendtreeMath.GreaterThan(others.distanceMeters.Value, self.distanceMeters.Value).create(
                    isSelf.MakeSetter(1),
                    isOthers.MakeSetter(1)
                ));
                return (isSelf, isOthers);
            });
            VFAFloat MakeClosest(string name, Func<TipRootPair, Lazy<VFAFloat>> getInner) {
                var output = controller.MakeAap($"{paramPrefix}/Closest/{name}", getInner(self).Value.GetDefault());
                math.MultiplyInPlace(output, whoIsClosest.Value.isSelf, getInner(self).Value);
                math.MultiplyInPlace(output, whoIsClosest.Value.isOthers, getInner(others).Value);
                return output;
            }
            closestLength = new Lazy<VFAFloat>(() => MakeClosest("Length", o => o.plugLength));
            closestRadius = new Lazy<VFAFloat>(() => MakeClosest("Radius", o => o.plugRadius));
            closestDistanceMeters = new Lazy<VFAFloat>(() => MakeClosest("Dist/Meters", o => o.distanceMeters));
            closestDistancePlugLengths = new Lazy<VFAFloat>(() => MakeClosest("Dist/PlugLens", o => o.distancePlugLengths));
            closestDistanceLocal = new Lazy<VFAFloat>(() => MakeClosest("Dist/Local", o => o.distanceLocal));

            velocity = new Lazy<VFAFloat>(() => {

                var currentDist = closestDistancePlugLengths.Value;
                var currentTime = frameTimeService.GetTimeSinceLoad();
                var lastDist = math.Buffer(currentDist, minSupported:-100, maxSupported:100);
                var lastTime = math.Buffer(currentTime);
                var diffDistEarly = controller.MakeAap("diff");
                math.CopyInPlace(currentDist, diffDistEarly);
                math.CopyInPlace(lastDist, diffDistEarly, -1);
                var diffDist = math.Buffer(diffDistEarly, minSupported:-100, maxSupported:100);
                var diffTime = math.Subtract(currentTime, lastTime);

                var latchedDiffDist = controller.MakeAap("latchedDiffDist");
                var latchedDiffTime = controller.MakeAap("latchedDiffTime");
                var update = VFBlendTreeDirect.Create("Update");
                update.Add(latchedDiffDist.MakeCopier(diffDist, minSupported:-100, maxSupported:100));
                update.Add(latchedDiffTime.MakeCopier(diffTime));
                var maintain = VFBlendTreeDirect.Create("Maintain");
                maintain.Add(latchedDiffDist.MakeCopier(latchedDiffDist, minSupported:-100, maxSupported:100));
                maintain.Add(latchedDiffTime.MakeCopier(latchedDiffTime));
                math.SetValueWithConditions(
                    (update, BlendtreeMath.Equals(diffDist, 0, epsilon: 0.0001f).Not()),
                    (maintain, null)
                );

                var latchedDiffDistBuffered1 = math.Buffer(latchedDiffDist, minSupported:-100, maxSupported:100);
                var latchedDiffDistBuffered2 = math.Buffer(latchedDiffDistBuffered1, minSupported:-100, maxSupported:100);
                var latchedDiffTimeInverted2 = math.Invert("latchedDiffTimeInverted2", latchedDiffTime);

                var output = controller.MakeAap($"{paramPrefix}/Dist/PlugLens/Vel");
                math.MultiplyInPlace(output, latchedDiffTimeInverted2, latchedDiffDistBuffered2);
                return output;
            });
        }

        public class TipRootPair {
            public readonly Lazy<VFAFloat> plugLength;
            public readonly Lazy<VFAFloat> plugRadius;
            public readonly Lazy<VFAFloat> distanceMeters;
            public readonly Lazy<VFAFloat> distancePlugLengths;
            public readonly Lazy<VFAFloat> distanceLocal;

            public TipRootPair(
                VFGameObject parent,
                string paramPrefix,
                string objPrefix,
                HapticContactsService hapticContactsService,
                VFBlendTreeDirect directTree,
                BlendtreeMath blendtreeMath,
                ControllerManager controller,
                bool useHipAvoidance,
                HapticUtils.ReceiverParty party,
                VFAFloat scaleFactor,
                float localPlugLength
            ) {
                var contactRadius = 3f;
                if (localPlugLength >= 0) {
                    // These are contacts for a plug, find the nearest socket
                    var contact = new Lazy<VFAFloat>(() => {
                        return hapticContactsService.AddReceiver(new HapticContactsService.ReceiverRequest() {
                            obj = parent,
                            paramName = $"{paramPrefix}/Contact",
                            objName = $"{objPrefix}",
                            radius = contactRadius,
                            tags = new[] { HapticUtils.TagSpsSocketRoot },
                            useHipAvoidance = useHipAvoidance,
                            party = party
                        });
                    });
                    plugLength = new Lazy<VFAFloat>(() => {
                        return blendtreeMath.Multiply($"{paramPrefix}/LengthMeters", scaleFactor, localPlugLength);
                    });
                    distanceMeters = new Lazy<VFAFloat>(() => {
                        var output = controller.MakeAap($"{paramPrefix}/Dist/Meters", def: 100);
                        directTree.Add(BlendtreeMath.Add(
                            $"{paramPrefix}/Dist/Meters",
                            output,
                            (plugLength.Value, -1),
                            (contact.Value, -contactRadius),
                            (1, contactRadius)
                        ));
                        return output;
                    });
                } else {
                    // These are contacts for a socket, find the nearest plug
                    var tipContact = new Lazy<VFAFloat>(() => {
                        return hapticContactsService.AddReceiver(new HapticContactsService.ReceiverRequest() {
                            obj = parent,
                            paramName = $"{paramPrefix}/Contact/Tip",
                            objName = $"{objPrefix}Tip",
                            radius = contactRadius,
                            tags = new[] { HapticUtils.CONTACT_PEN_MAIN },
                            useHipAvoidance = useHipAvoidance,
                            party = party
                        });
                    });
                    var rootContact = new Lazy<VFAFloat>(() => {
                        return hapticContactsService.AddReceiver(new HapticContactsService.ReceiverRequest() {
                            obj = parent,
                            paramName = $"{paramPrefix}/Contact/Root",
                            objName = $"{objPrefix}Root",
                            radius = contactRadius,
                            tags = new[] { HapticUtils.CONTACT_PEN_ROOT },
                            useHipAvoidance = useHipAvoidance,
                            party = party
                        });
                    });
                    var widthContact = new Lazy<VFAFloat>(() => {
                        return hapticContactsService.AddReceiver(new HapticContactsService.ReceiverRequest() {
                            obj = parent,
                            paramName = $"{paramPrefix}/Contact/Width",
                            objName = $"{objPrefix}Width",
                            radius = contactRadius,
                            tags = new[] { HapticUtils.CONTACT_PEN_WIDTH },
                            useHipAvoidance = useHipAvoidance,
                            party = party
                        });
                    });

                    VFAFloat MakeOffsetDetector(string name, VFAFloat basisContact, bool useWidthMath, float defaultSize) {
                        var output = controller.MakeAap($"{paramPrefix}/{name}", def: defaultSize);
                        var whenDetectable = useWidthMath
                            ? BlendtreeMath.Add(
                                $"{paramPrefix}/{name}/Detectable",
                                output,
                                (tipContact.Value, 0.5f * contactRadius),
                                (widthContact.Value, -0.5f * contactRadius)
                            )
                            : BlendtreeMath.Add(
                                $"{paramPrefix}/{name}/Detectable",
                                output,
                                (tipContact.Value, contactRadius),
                                (basisContact, -contactRadius),
                                (0.01f, 1)
                            );
                        var whenTipOnly = BlendtreeMath.Add($"{paramPrefix}/{name}/TipOnly", output, (defaultSize, 1));
                        Motion whenGone = whenTipOnly;
                        var whenInside = BlendtreeMath.Add($"{paramPrefix}/{name}/Inside", output, (output, 1));

                        directTree.Add(
                            BlendtreeMath.GreaterThan(tipContact.Value, 0).create(
                                BlendtreeMath.GreaterThan(basisContact, 0).create(
                                    BlendtreeMath.LessThan(tipContact.Value, 1).create(
                                        whenDetectable,
                                        whenInside
                                    ),
                                    whenTipOnly
                                ),
                                whenGone
                            )
                        );
                        return output;
                    }

                    plugLength = new Lazy<VFAFloat>(() => {
                        var defaultLength = 0.3f;
                        return MakeOffsetDetector("Length", rootContact.Value, false, defaultLength);
                    });
                    plugRadius = new Lazy<VFAFloat>(() => {
                        var defaultRadius = 0.03f;
                        return MakeOffsetDetector("Radius", widthContact.Value, true, defaultRadius);
                    });

                    distanceMeters = new Lazy<VFAFloat>(() => {
                        var output = controller.MakeAap($"{paramPrefix}/Dist/Meters", def: 100);
                        var whenInside = BlendtreeMath.Add(
                            $"{paramPrefix}/Dist/Inside",
                            output,
                            (plugLength.Value, -1),
                            (rootContact.Value, -contactRadius),
                            (1, contactRadius)
                        );

                        var whenOutside = BlendtreeMath.Add(
                            $"{paramPrefix}/Dist/Outside",
                            output,
                            (tipContact.Value, -contactRadius),
                            (1, contactRadius)
                        );

                        var whenInsideNoRoot = BlendtreeMath.Add($"{paramPrefix}/Dist/InsideNoRoot", output);
                        var whenInsideNoLength = whenInsideNoRoot;
                        var whenGone = BlendtreeMath.Add($"{paramPrefix}/Dist/Gone", output, (100, 1));

                        var tree = BlendtreeMath.GreaterThan(tipContact.Value, 1, true).create(
                            BlendtreeMath.GreaterThan(rootContact.Value, 0).create(
                                BlendtreeMath.GreaterThan(plugLength.Value, 0).create(
                                    whenInside,
                                    whenInsideNoLength
                                ),
                                whenInsideNoRoot
                            ),
                            BlendtreeMath.GreaterThan(tipContact.Value, 0).create(
                                whenOutside,
                                whenGone
                            )
                        );
                        directTree.Add(tree);
                        return output;
                    });
                }

                distancePlugLengths = new Lazy<VFAFloat>(() => {
                    var output = controller.MakeAap($"{paramPrefix}/Dist/PlugLens", def: 100);
                    var invertedPlugLength = blendtreeMath.Invert($"{paramPrefix}/Length/Inverted", plugLength.Value);
                    blendtreeMath.MultiplyInPlace(output, invertedPlugLength, distanceMeters.Value);
                    return output;
                });
                distanceLocal = new Lazy<VFAFloat>(() => {
                    var output = controller.MakeAap($"{paramPrefix}/Dist/Local", def: 100);
                    var invertedScaleFactor = blendtreeMath.Invert($"{paramPrefix}/Scale/Inverted", scaleFactor);
                    blendtreeMath.MultiplyInPlace(output, invertedScaleFactor, distanceMeters.Value);
                    return output;
                });
            }
        }
    }
}
