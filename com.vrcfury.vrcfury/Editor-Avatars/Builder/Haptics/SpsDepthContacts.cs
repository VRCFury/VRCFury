using System;
using UnityEngine;
using VF.Service;
using VF.Utils;
using VF.Utils.Controller;

namespace VF.Builder.Haptics {
    internal class SpsDepthContacts {
        public readonly TipRootPair self;
        public readonly TipRootPair others;
        public readonly Lazy<VFAParam> closestLength;
        public readonly Lazy<VFAParam> closestRadius;
        public readonly Lazy<VFAParam> closestDistanceMeters;
        public readonly Lazy<VFAParam> closestDistancePlugLengths;
        public readonly Lazy<VFAParam> closestDistanceLocal;
        public readonly Lazy<VFAParam> closestLengthInverted;
        public readonly Lazy<VFAParam> worldScaleInverted;
        public readonly Lazy<VFAParam> velocity;
        public readonly Lazy<VFAParam> velocityPlugLengths;
        public readonly Lazy<VFAParam> velocityLocal;
        public readonly VFBlendTreeDirect directTree;

        public SpsDepthContacts(
            VFGameObject parent,
            string paramPrefix,
            HapticContactsService hapticContactsService,
            VFBlendTreeDirect directTree,
            BlendtreeMath math,
            VFAvatarController controller,
            FrameTimeService frameTimeService,
            bool useHipAvoidance,
            VFAParam worldScale,
            float inputPlugLength = -1
        ) {
            this.directTree = directTree;
            self = new TipRootPair(parent, paramPrefix + "/Self", "Self", hapticContactsService, directTree, math, controller, useHipAvoidance, HapticUtils.ReceiverParty.Self, worldScale, inputPlugLength);
            others = new TipRootPair(parent, paramPrefix + "/Others", "Others", hapticContactsService, directTree, math, controller, useHipAvoidance, HapticUtils.ReceiverParty.Others, worldScale, inputPlugLength);
            var whoIsClosest = new Lazy<(VFAParam isSelf,VFAParam isOthers)>(() => {
                var isSelf = controller.CreateUniqueFloat(paramPrefix + "/Closest/IsSelf", def: 1);
                var isOthers = controller.CreateUniqueFloat(paramPrefix + "/Closest/IsOthers");
                directTree.Add(BlendtreeMath.GreaterThan(others.distanceMeters.Value, self.distanceMeters.Value).create(
                    isSelf.MakeSetter(1),
                    isOthers.MakeSetter(1)
                ));
                return (isSelf, isOthers);
            });
            VFAParam MakeClosest(string name, Func<TipRootPair, Lazy<VFAParam>> getInner) {
                var output = controller.CreateUniqueFloat($"{paramPrefix}/Closest/{name}", getInner(self).Value.GetDefault());
                math.MultiplyInPlace(output, whoIsClosest.Value.isSelf, getInner(self).Value);
                math.MultiplyInPlace(output, whoIsClosest.Value.isOthers, getInner(others).Value);
                return output;
            }
            closestLength = new Lazy<VFAParam>(() => MakeClosest("Length", o => o.plugLength));
            closestRadius = new Lazy<VFAParam>(() => MakeClosest("Radius", o => o.plugRadius));
            closestDistanceMeters = new Lazy<VFAParam>(() => MakeClosest("Dist/Meters", o => o.distanceMeters));
            closestDistancePlugLengths = new Lazy<VFAParam>(() => MakeClosest("Dist/PlugLens", o => o.distancePlugLengths));
            closestDistanceLocal = new Lazy<VFAParam>(() => MakeClosest("Dist/Local", o => o.distanceLocal));
            closestLengthInverted = new Lazy<VFAParam>(() => math.Invert($"{paramPrefix}/Length/Inverted", closestLength.Value));
            worldScaleInverted = new Lazy<VFAParam>(() => math.Invert($"{paramPrefix}/Scale/Inverted", worldScale));
            velocity = new Lazy<VFAParam>(() => {
                var prefix = $"{paramPrefix}/Dist/Meters/Vel";
                var currentTime = frameTimeService.GetTimeSinceLoad();
                var lastDist = math.Buffer(closestDistanceMeters.Value, minSupported:-100, maxSupported:100);
                var lastTime = math.Buffer(currentTime);
                var diffDistEarly = controller.CreateUniqueFloat($"{prefix}/Diff");
                math.CopyInPlace(closestDistanceMeters.Value, diffDistEarly);
                math.CopyInPlace(lastDist, diffDistEarly, -1);
                var diffDist = math.Buffer(diffDistEarly, minSupported:-100, maxSupported:100);
                var diffTime = math.Subtract(currentTime, lastTime);

                var latchedDiffDist = controller.CreateUniqueFloat($"{prefix}/LatchedDiffDist");
                var latchedDiffTime = controller.CreateUniqueFloat($"{prefix}/LatchedDiffTime");
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
                var latchedDiffTimeInverted2 = math.Invert($"{prefix}/LatchedDiffTimeInverted2", latchedDiffTime);

                var output = controller.CreateUniqueFloat(prefix);
                math.MultiplyInPlace(output, latchedDiffTimeInverted2, latchedDiffDistBuffered2);
                return output;
            });
            velocityPlugLengths = new Lazy<VFAParam>(() => {
                var output = controller.CreateUniqueFloat($"{paramPrefix}/Dist/PlugLens/Vel");
                math.MultiplyInPlace(output, closestLengthInverted.Value, velocity.Value);
                return output;
            });
            velocityLocal = new Lazy<VFAParam>(() => {
                var output = controller.CreateUniqueFloat($"{paramPrefix}/Dist/Local/Vel");
                math.MultiplyInPlace(output, worldScaleInverted.Value, velocity.Value);
                return output;
            });
        }

        public class TipRootPair {
            public readonly Lazy<VFAParam> plugLength;
                public readonly Lazy<VFAParam> plugRadius;
                public readonly Lazy<VFAParam> distanceMeters;
                public readonly Lazy<VFAParam> distancePlugLengths;
                public readonly Lazy<VFAParam> distanceLocal;
                public readonly Lazy<VFAParam> plugLengthInverted;
                public readonly Lazy<VFAParam> worldScaleInverted;

            public TipRootPair(
                VFGameObject parent,
                string paramPrefix,
                string objPrefix,
                HapticContactsService hapticContactsService,
                VFBlendTreeDirect directTree,
                BlendtreeMath blendtreeMath,
                VFAvatarController controller,
                bool useHipAvoidance,
                HapticUtils.ReceiverParty party,
                VFAParam worldScale,
                float localPlugLength
            ) {
                var contactRadius = 3f;
                var suffix = party == HapticUtils.ReceiverParty.Self && useHipAvoidance
                    ? hapticContactsService.GetSelfTagSuffix(parent) : "";
                if (localPlugLength >= 0) {
                    // These are contacts for a plug, find the nearest socket
                    var contact = new Lazy<VFAParam>(() => {
                        return hapticContactsService.AddReceiver(new HapticContactsService.ReceiverRequest() {
                            obj = parent,
                            paramName = $"{paramPrefix}/Contact",
                            objName = $"{objPrefix}",
                            radius = contactRadius,
                            tags = new[] { HapticUtils.TagTpsOrfRoot + suffix },
                            party = party
                        });
                    });
                    plugLength = new Lazy<VFAParam>(() => {
                        return blendtreeMath.Multiply($"{paramPrefix}/LengthMeters", worldScale, localPlugLength);
                    });
                    distanceMeters = new Lazy<VFAParam>(() => {
                        var output = controller.CreateUniqueFloat($"{paramPrefix}/Dist/Meters", def: 100);
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
                    var tipContact = new Lazy<VFAParam>(() => {
                        return hapticContactsService.AddReceiver(new HapticContactsService.ReceiverRequest() {
                            obj = parent,
                            paramName = $"{paramPrefix}/Contact/Tip",
                            objName = $"{objPrefix}Tip",
                            radius = contactRadius,
                            tags = new[] { HapticUtils.CONTACT_PEN_MAIN + suffix },
                            party = party
                        });
                    });
                    var rootContact = new Lazy<VFAParam>(() => {
                        return hapticContactsService.AddReceiver(new HapticContactsService.ReceiverRequest() {
                            obj = parent,
                            paramName = $"{paramPrefix}/Contact/Root",
                            objName = $"{objPrefix}Root",
                            radius = contactRadius,
                            tags = new[] { HapticUtils.CONTACT_PEN_ROOT + suffix },
                            party = party
                        });
                    });
                    var widthContact = new Lazy<VFAParam>(() => {
                        return hapticContactsService.AddReceiver(new HapticContactsService.ReceiverRequest() {
                            obj = parent,
                            paramName = $"{paramPrefix}/Contact/Width",
                            objName = $"{objPrefix}Width",
                            radius = contactRadius,
                            tags = new[] { HapticUtils.CONTACT_PEN_WIDTH + suffix },
                            party = party
                        });
                    });

                    VFAParam MakeOffsetDetector(string name, VFAParam basisContact, bool useWidthMath, float defaultSize) {
                        var output = controller.CreateUniqueFloat($"{paramPrefix}/{name}", def: defaultSize);
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
                        VFMotion whenGone = whenTipOnly;
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

                    plugLength = new Lazy<VFAParam>(() => {
                        var defaultLength = 0.3f;
                        return MakeOffsetDetector("Length", rootContact.Value, false, defaultLength);
                    });
                    plugRadius = new Lazy<VFAParam>(() => {
                        var defaultRadius = 0.03f;
                        return MakeOffsetDetector("Radius", widthContact.Value, true, defaultRadius);
                    });

                    distanceMeters = new Lazy<VFAParam>(() => {
                        var output = controller.CreateUniqueFloat($"{paramPrefix}/Dist/Meters", def: 100);
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

                plugLengthInverted = new Lazy<VFAParam>(() => blendtreeMath.Invert($"{paramPrefix}/Length/Inverted", plugLength.Value));
                worldScaleInverted = new Lazy<VFAParam>(() => blendtreeMath.Invert($"{paramPrefix}/Scale/Inverted", worldScale));

                distancePlugLengths = new Lazy<VFAParam>(() => {
                    var output = controller.CreateUniqueFloat($"{paramPrefix}/Dist/PlugLens", def: 100);
                    blendtreeMath.MultiplyInPlace(output, plugLengthInverted.Value, distanceMeters.Value);
                    return output;
                });
                distanceLocal = new Lazy<VFAParam>(() => {
                    var output = controller.CreateUniqueFloat($"{paramPrefix}/Dist/Local", def: 100);
                    blendtreeMath.MultiplyInPlace(output, worldScaleInverted.Value, distanceMeters.Value);
                    return output;
                });
            }
        }
    }
}
