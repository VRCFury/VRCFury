using System;
using UnityEngine;
using VF.Service;
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

        public SpsDepthContacts(
            VFGameObject parent,
            string paramPrefix,
            HapticContactsService hapticContactsService,
            DirectBlendTreeService directTree,
            MathService math,
            bool useHipAvoidance,
            VFAFloat scaleFactor,
            float inputPlugLength = -1
        ) {
            self = new TipRootPair(parent, paramPrefix + "/Self", "Self", hapticContactsService, directTree, math, useHipAvoidance, HapticUtils.ReceiverParty.Self, scaleFactor, inputPlugLength);
            others = new TipRootPair(parent, paramPrefix + "/Others", "Others", hapticContactsService, directTree, math, useHipAvoidance, HapticUtils.ReceiverParty.Others, scaleFactor, inputPlugLength);
            var whoIsClosest = new Lazy<(VFAFloat isSelf,VFAFloat isOthers)>(() => {
                var isSelf = math.MakeAap(paramPrefix + "/Closest/IsSelf");
                var isOthers = math.MakeAap(paramPrefix + "/Closest/IsOthers");
                directTree.Add(math.GreaterThan(others.distanceMeters.Value, self.distanceMeters.Value).create(
                    math.MakeSetter(isSelf, 1),
                    math.MakeSetter(isOthers, 1)
                ));
                return (isSelf, isOthers);
            });
            VFAFloat MakeClosest(string name, Func<TipRootPair, Lazy<VFAFloat>> getInner) {
                var output = math.MakeAap($"{paramPrefix}/Closest/{name}", getInner(self).Value.GetDefault());
                math.MultiplyInPlace(output, whoIsClosest.Value.isSelf, getInner(self).Value);
                math.MultiplyInPlace(output, whoIsClosest.Value.isOthers, getInner(others).Value);
                return output;
            }
            closestLength = new Lazy<VFAFloat>(() => MakeClosest("Length", o => o.plugLength));
            closestRadius = new Lazy<VFAFloat>(() => MakeClosest("Radius", o => o.plugRadius));
            closestDistanceMeters = new Lazy<VFAFloat>(() => MakeClosest("Dist/Meters", o => o.distanceMeters));
            closestDistancePlugLengths = new Lazy<VFAFloat>(() => MakeClosest("Dist/PlugLens", o => o.distancePlugLengths));
            closestDistanceLocal = new Lazy<VFAFloat>(() => MakeClosest("Dist/Local", o => o.distanceLocal));
        }
    }

    internal class TipRootPair {
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
            DirectBlendTreeService directTree,
            MathService math,
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
                    return math.Multiply($"{paramPrefix}/LengthMeters", scaleFactor, localPlugLength);
                });
                distanceMeters = new Lazy<VFAFloat>(() => {
                    var output = math.MakeAap($"{paramPrefix}/Dist/Meters", def: 100);
                    directTree.Add(math.Add(
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
                    var output = math.MakeAap($"{paramPrefix}/{name}");
                    var whenDetectable = useWidthMath
                        ? math.Add(
                            $"{paramPrefix}/{name}/Detectable",
                            output,
                            (tipContact.Value, 0.5f * contactRadius),
                            (widthContact.Value, -0.5f * contactRadius)
                        )
                        : math.Add(
                            $"{paramPrefix}/{name}/Detectable",
                            output,
                            (tipContact.Value, contactRadius),
                            (basisContact, -contactRadius),
                            (0.01f, 1)
                        );
                    var whenTipOnly = math.Add($"{paramPrefix}/{name}/TipOnly", output, (defaultSize, 1));
                    Motion whenGone = null;
                    var whenInside = math.Add($"{paramPrefix}/{name}/Inside", output, (output, 1));

                    directTree.Add(
                        math.GreaterThan(tipContact.Value, 0).create(
                            math.GreaterThan(basisContact, 0).create(
                                math.LessThan(tipContact.Value, 1).create(
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
                    var output = math.MakeAap($"{paramPrefix}/Dist/Meters", def: 100);
                    var whenInside = math.Add(
                        $"{paramPrefix}/Dist/Inside",
                        output,
                        (plugLength.Value, -1),
                        (rootContact.Value, -contactRadius),
                        (1, contactRadius)
                    );

                    var whenOutside = math.Add(
                        $"{paramPrefix}/Dist/Outside",
                        output,
                        (tipContact.Value, -contactRadius),
                        (1, contactRadius)
                    );

                    var whenInsideNoRoot = math.Add($"{paramPrefix}/Dist/InsideNoRoot", output);
                    var whenInsideNoLength = whenInsideNoRoot;
                    var whenGone = math.Add($"{paramPrefix}/Dist/Gone", output, (100, 1));

                    var tree = math.GreaterThan(tipContact.Value, 1, true).create(
                        math.GreaterThan(rootContact.Value, 0).create(
                            math.GreaterThan(plugLength.Value, 0).create(
                                whenInside,
                                whenInsideNoLength
                            ),
                            whenInsideNoRoot
                        ),
                        math.GreaterThan(tipContact.Value, 0).create(
                            whenOutside,
                            whenGone
                        )
                    );
                    directTree.Add(tree);
                    return output;
                });
            }

            distancePlugLengths = new Lazy<VFAFloat>(() => {
                var output = math.MakeAap($"{paramPrefix}/Dist/PlugLens", def: 100);
                var invertedPlugLength = math.Invert($"{paramPrefix}/Length/Inverted", plugLength.Value);
                math.MultiplyInPlace(output, invertedPlugLength, distanceMeters.Value);
                return output;
            });
            distanceLocal = new Lazy<VFAFloat>(() => {
                var output = math.MakeAap($"{paramPrefix}/Dist/Local", def: 100);
                var invertedScaleFactor = math.Invert($"{paramPrefix}/Scale/Inverted", scaleFactor);
                math.MultiplyInPlace(output, invertedScaleFactor, distanceMeters.Value);
                return output;
            });
        }
    }
}
