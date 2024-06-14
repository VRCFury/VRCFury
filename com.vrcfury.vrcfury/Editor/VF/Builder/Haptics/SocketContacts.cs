using System;
using System.Collections.Generic;
using UnityEditor.Animations;
using UnityEngine;
using UnityEngine.Animations;
using VF.Inspector;
using VF.Service;
using VF.Utils;
using VF.Utils.Controller;

namespace VF.Builder.Haptics {
    internal class SocketContacts {
        public TipRootPair self;
        public TipRootPair others;
        public Lazy<VFAFloat> closestLength;
        public Lazy<VFAFloat> closestRadius;
        public Lazy<VFAFloat> closestDistanceMeters;
        public Lazy<VFAFloat> closestDistancePlugLengths;

        public SocketContacts(VFGameObject parent, string paramPrefix, HapticContactsService hapticContactsService, ClipFactoryService clipFactory, DirectBlendTreeService directTree, MathService math, bool useHipAvoidance) {
            self = new TipRootPair(parent, paramPrefix + "/Self", "Self", hapticContactsService, clipFactory, directTree, math, useHipAvoidance, HapticUtils.ReceiverParty.Self);
            others = new TipRootPair(parent, paramPrefix + "/Others", "Others", hapticContactsService, clipFactory, directTree, math, useHipAvoidance, HapticUtils.ReceiverParty.Others);
            closestLength = new Lazy<VFAFloat>(() => {
                return math.SetValueWithConditions(
                    paramPrefix + "/Closest/Length",
                    (self.plugLength.Value, math.GreaterThan(others.plugDistanceMeters.Value, self.plugDistanceMeters.Value)),
                    (others.plugLength.Value, null)
                );
            });
            closestRadius = new Lazy<VFAFloat>(() => {
                return math.SetValueWithConditions(
                    paramPrefix + "/Closest/Radius",
                    (self.plugRadius.Value, math.GreaterThan(others.plugDistanceMeters.Value, self.plugDistanceMeters.Value)),
                    (others.plugRadius.Value, null)
                );
            });
            closestDistanceMeters = new Lazy<VFAFloat>(() => {
                return math.SetValueWithConditions(
                    paramPrefix + "/Closest/Radius",
                    (self.plugDistanceMeters.Value, math.GreaterThan(others.plugDistanceMeters.Value, self.plugDistanceMeters.Value)),
                    (others.plugDistanceMeters.Value, null)
                );
            });
            closestDistancePlugLengths = new Lazy<VFAFloat>(() => {
                return math.SetValueWithConditions(
                    paramPrefix + "/Closest/Radius",
                    (self.plugDistancePlugLengths.Value, math.GreaterThan(others.plugDistancePlugLengths.Value, self.plugDistancePlugLengths.Value)),
                    (others.plugDistancePlugLengths.Value, null)
                );
            });
        }
    }
    
    internal class Distance {
        public MathService.VFAap meters;
        public MathService.VFAap local;
        public MathService.VFAap plugLengths;

        private readonly VFAFloat localScaleMultiplier;
        private readonly VFAFloat plugScaleMultiplier;
        private readonly MathService math;

        public Distance() {}

        public Distance(string paramPrefix, VFAFloat localScaleMultiplier, VFAFloat plugScaleMultiplier, MathService math) {
            this.localScaleMultiplier = localScaleMultiplier;
            this.plugScaleMultiplier = plugScaleMultiplier;
            this.math = math;
            meters = math.MakeAap($"{paramPrefix}/Meters");
            meters = math.MakeAap($"{paramPrefix}/Local");
            meters = math.MakeAap($"{paramPrefix}/PlugLengths");
        }

        public Motion MakeSetter(float valueMeters) {
            var output = math.MakeDirect("Distance = " + valueMeters);
            output.Add(math.One(), math.MakeSetter(meters, valueMeters));
            output.Add(localScaleMultiplier, math.MakeSetter(local, valueMeters));
            output.Add(plugScaleMultiplier, math.MakeSetter(plugLengths, valueMeters));
            return output;
        }
    }
    
    internal class TipRootPair {
        public Lazy<VFAFloat> plugLength;
        public Lazy<VFAFloat> plugRadius;
        public Lazy<Distance> plugDistance;

        /**
         * test on android
         */
        public TipRootPair(VFGameObject parent, string paramPrefix, string objPrefix, HapticContactsService hapticContactsService, ClipFactoryService clipFactory, DirectBlendTreeService directTree, MathService math, bool useHipAvoidance, HapticUtils.ReceiverParty party) {

            var contactRadius = 3f;
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
                var whenDetectable = useWidthMath ? 
                    math.Add(
                        $"{paramPrefix}/{name}/Detectable",
                        output,
                        (tipContact.Value,0.5f * contactRadius),
                        (widthContact.Value,-0.5f * contactRadius)
                    ) :
                    math.Add(
                        $"{paramPrefix}/{name}/Detectable",
                        output,
                        (tipContact.Value, contactRadius),
                        (basisContact, -contactRadius),
                        (0.01f, 1)
                    );
                var whenTipOnly = math.Add($"{paramPrefix}/{name}/TipOnly", output, (defaultSize, 1));
                var whenGone = math.Add($"{paramPrefix}/{name}/Gone", output);
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

            Motion MakePlugLengthMeters(MathService.VFAap output) {
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
                var whenGone = math.Add($"{paramPrefix}/Dist/Gone", output, (100, 1));

                return math.GreaterThan(tipContact.Value, 1, true).create(
                    math.GreaterThan(rootContact.Value, 0).create(
                        whenInside,
                        whenInsideNoRoot
                    ),
                    math.GreaterThan(tipContact.Value, 0).create(
                        whenOutside,
                        whenGone
                    )
                );
            }
            plugDistanceMeters = new Lazy<VFAFloat>(() => {
                var output = math.MakeAap($"{paramPrefix}/DistMeters", def: 100);
                directTree.Add(MakePlugLengthMeters(output));
                return output;
            });
            plugDistancePlugLengths = new Lazy<VFAFloat>(() => {
                var output = math.MakeAap($"{paramPrefix}/DistPlugBasis", def: 100);
                var invertedPlugLength = math.Invert($"{paramPrefix}/Length/Inverted", plugLength.Value);
                directTree.Add(invertedPlugLength, MakePlugLengthMeters(output));
                return output;
            });
        }
    }
}
