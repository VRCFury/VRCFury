namespace VF.Tests {
    using System;
    using System.Linq;
    using NUnit.Framework;
    using UnityEditor;
    using UnityEditor.Rendering;
    using UnityEngine;
    using UnityEngine.Rendering;
    using VF.Builder.Haptics;
    using Object = UnityEngine.Object;

    internal class sps_curveTest : SpsShaderTestBase {
        [Test]
        public void AdvancedHoleKeepsFullRadiusUntilJoinThenUsesRemainingTaper() {
            var hole = (new Vector3(.025f, 0, .005f), Vector3.forward, 1 << 14);
            SetPath(hole);
            material.SetFloat("_Operation", 9);
            var joinZ = Render().b;
            var advance = joinZ - hole.Item1.z;
            const float radius = .1f;
            var cone = SmoothCollapseLength;
            Assert.That(advance, Is.InRange(TaperFullRadiusLength, TaperFullRadiusLength + cone));

            SetPath(deformationShader, hole);
            material.SetFloat("_SPS_BakedRadius", radius);
            material.SetFloat("_SampleDistance", .08f);
            var approach = Render();
            Assert.That(approach.b, Is.LessThan(joinZ));
            Assert.That(approach.a, Is.EqualTo(1).Within(.0001f));

            material.SetFloat("_SampleDistance", .105f);
            var after = Render();
            Assert.That(after.b, Is.GreaterThan(joinZ));
            var end = hole.Item1.z + TaperFullRadiusLength + cone;
            Assert.That(after.a, Is.InRange(.001f, .999f));
            Assert.That(after.a, Is.EqualTo((end - after.b) / cone).Within(.0001f));
        }

        [Test]
        public void HoleTaperIsContinuousWhenSidewaysOffsetStartsAdvancingTheJoin() {
            const float entrance = .05f;
            Color Sample(float offset, bool advanced) {
                var hole = (new Vector3(offset, 0, entrance), Vector3.forward, 1 << 14);
                SetPath(hole);
                material.SetFloat("_Operation", 9);
                var join = Render();
                Assert.That(join.a, Is.EqualTo(1));
                if (advanced) Assert.That(join.b, Is.GreaterThan(entrance + .00001f));
                else Assert.That(join.b, Is.EqualTo(entrance).Within(.000001f));

                SetPath(deformationShader, hole);
                material.SetFloat("_SampleDistance", .106f);
                return Render();
            }

            var before = Sample(.0073f, false);
            var after = Sample(.0074f, true);
            Assert.That(before.a, Is.InRange(.1f, .9f));
            Assert.That(after.a, Is.EqualTo(before.a).Within(.01f));
        }

        [TestCase(.5f)]
        [TestCase(.08f)]
        public void GuidedRingReopensInItsPlaneBeforeTravelResumes(float span) {
            const float hole = .125f;
            var ring = hole + span;
            SetPath(
                deformationShader,
                (Vector3.forward * hole, Vector3.forward, 1 << 14),
                (Vector3.forward * (hole + span / 4), Vector3.forward, 1 << 14),
                (Vector3.forward * ring, Vector3.forward, 0)
            );
            var taperLength = Mathf.Min(StationaryTaperLength, span / 2);
            var samples = new[] {
                (.875f * taperLength, .25f),
                (.75f * taperLength, .5f),
                (.5f * taperLength, 1f),
                (.25f * taperLength, 1f),
                (0f, 1f)
            };
            foreach (var (distanceToRing, radius) in samples) {
                material.SetFloat("_SampleDistance", ring - distanceToRing);
                var sample = Render();
                Assert.That(sample.a, Is.EqualTo(radius).Within(.0001f));
                Assert.That(sample.b, Is.EqualTo(ring).Within(.0001f));
            }
            material.SetFloat("_SampleDistance", ring + .03f);
            var pastRing = Render();
            Assert.That(pastRing.a, Is.EqualTo(1).Within(.0001f));
            Assert.That(pastRing.b, Is.EqualTo(ring + .03f).Within(.0001f));
        }

        [Test]
        public void GuidedCollapseMeasuresExitTaperFromLateJoin() {
            // The entrance is behind the plug. The join is at zero, leaving half a plug length to the ring.
            const float ring = .5f;
            SetPath(
                deformationShader,
                (Vector3.back * .25f, Vector3.forward, 1 << 14),
                (Vector3.forward * ring, Vector3.forward, 0)
            );
            const float taperLength = StationaryTaperLength;
            var samples = new[] {
                (.01f, 0f),
                (.24f, 0f),
                (.26f, 0f),
                (ring - taperLength, 0f),
                (ring - .75f * taperLength, .5f),
                (ring, 1f)
            };
            foreach (var (distance, radius) in samples) {
                material.SetFloat("_SampleDistance", distance);
                var sample = Render();
                Assert.That(sample.b, Is.EqualTo(distance < .25f ? 0 : ring).Within(.0001f));
                Assert.That(sample.a, Is.EqualTo(radius).Within(.0001f));
            }
        }

        [Test]
        public void SpentTaperOnCurvedHoleToRingClosesAtJoinAndReopensAtRing() {
            var ring = new Vector3(0, 0, .5f);
            SetPath(
                deformationShader,
                (Vector3.back * .2f, Vector3.forward, 1 << 14),
                (ring, Vector3.right, 0)
            );
            foreach (var distance in new[] { .03f, .06f, .28f, .35f }) {
                material.SetFloat("_SampleDistance", distance);
                var sample = Render();
                Assert.That(sample.a, Is.Zero.Within(.0001f));
                var center = new Vector3(sample.r, sample.g, sample.b);
                Assert.That(Vector3.Distance(center, distance <= .28f ? Vector3.zero : ring), Is.LessThan(.0001f));
            }
            material.SetFloat("_SampleDistance", .9f);
            Assert.That(Render().a, Is.EqualTo(1).Within(.0001f));
        }

        [Test]
        public void SpentTerminalTaperCollapsesAtTheJoin() {
            var hole = (new Vector3(.025f, 0, -.025f), Vector3.forward, 1 << 14);
            SetPath(hole);
            material.SetFloat("_Operation", 9);
            var join = Render();
            Assert.That(join.b - hole.Item1.z, Is.GreaterThan(TaperFullRadiusLength + SmoothCollapseLength));
            SetPath(deformationShader, hole);
            material.SetFloat("_SampleDistance", .2f);
            var end = Render();
            Assert.That(end.a, Is.Zero.Within(.0001f));
            Assert.That(end.r, Is.EqualTo(join.r).Within(.0001f));
            Assert.That(end.b, Is.EqualTo(join.b).Within(.0001f));
        }

        [Test]
        public void AdvancedTerminalHoleWithTaperBehindRootCollapsesAtRoot() {
            SetPath(deformationShader, (Vector3.forward * -.2f, Vector3.forward, 1 << 14));
            material.SetFloat("_SampleDistance", .2f);
            var result = Render();
            Assert.That(result.a, Is.Zero.Within(.0001f));
            Assert.That(result.b, Is.Zero.Within(.0001f));
        }

        [Test]
        public void SkippingRingConsumesTheRemainingHoleTaper() {
            const float ring = -.12f;
            const float hole = -.10f;
            SetPath(
                deformationShader,
                (Vector3.forward * ring, Vector3.forward, 0),
                (Vector3.forward * hole, Vector3.forward, 1 << 14)
            );
            foreach (var distance in new[] { .005f, .2f }) {
                material.SetFloat("_SampleDistance", distance);
                var sample = Render();
                Assert.That(sample.b, Is.Zero.Within(.0001f), "The centerline turned back to the guide.");
                var radius = Mathf.Clamp01((StationaryTaperLength + hole - distance) / (StationaryTaperLength / 2));
                Assert.That(sample.a, Is.EqualTo(radius).Within(.0001f));
            }
        }

        [TestCase(-.1f, 0f)]
        [TestCase(.3f, .5f)]
        public void HoleAfterRingMeasuresTaperFromItsPlane(float holeZ, float expectedRadius) {
            try {
                SetIndependentSockets(deformationShader, holeZ, true);
                var distance = Mathf.Max(holeZ, 0) + TaperFullRadiusLength + SmoothCollapseLength / 2;
                material.SetFloat("_SampleDistance", distance);
                Assert.That(Render().a, Is.EqualTo(expectedRadius).Within(.0001f));
            } finally {
                if (material != null) material.shader = shader;
            }
        }

        [TestCase(false)]
        [TestCase(true)]
        public void FadedHoleUsesNormalTaperInFirstHalfOfGuidedSpan(bool guided) {
            try {
                if (guided) {
                    SetPath(
                        deformationShader,
                        (Vector3.forward * 1.4f, Vector3.forward, 1 << 14),
                        (Vector3.forward * 4.4f, Vector3.forward, 0)
                    );
                } else {
                    SetPath(deformationShader, (Vector3.forward * 1.4f, Vector3.forward, 1 << 14));
                }
                var coneLength = SmoothCollapseLength;
                var fadeAdvance = .625f * (1 + coneLength);
                material.SetFloat("_SampleDistance", 1.4f + fadeAdvance + TaperFullRadiusLength + coneLength / 2);
                Assert.That(Render().a, Is.EqualTo(.5f).Within(.0001f));
            } finally {
                if (material != null) material.shader = shader;
            }
        }

        [Test]
        public void FadeMovesTheHoleTaperPastTheTipOnAShortenedPath() {
            var entrance = new Vector3(.369f, 0, .02f);
            SetPath(deformationShader, (entrance, Vector3.right, 0));
            material.SetFloat("_SPS_BaseActivationRadiusOverride", .4f);
            var entranceRotation = Quaternion.LookRotation(Vector3.left, Vector3.up);
            var exitRotation = Quaternion.LookRotation(Vector3.right + Vector3.forward, Vector3.up);
            SetContacts(
                "SPS_Self",
                Matrix4x4.identity,
                Matrix4x4.TRS(entrance, entranceRotation, Vector3.one),
                Matrix4x4.TRS(new Vector3(-.65f, 0, -.65f), exitRotation, new Vector3(2 | (1 << 14), 1, 1))
            );
            SetContacts(
                "SPS_Path",
                Matrix4x4.identity,
                Matrix4x4.TRS(new Vector3(-.62f, 0, -.62f), exitRotation, new Vector3(129, 1, 1))
            );

            // Straightening the turn brings the hole within reach of the mesh tip.
            material.SetFloat("_SampleDistance", 1);
            Assert.That(Render().a, Is.EqualTo(1).Within(.0001f));
            // Verify that the hole was selected and eventually closes the mesh.
            material.SetFloat("_SampleDistance", 1.3f);
            Assert.That(Render().a, Is.Zero.Within(.0001f));
        }

        [Test]
        public void ShortGuidedSpanFitsTheEntranceTaperIntoItsFirstHalf() {
            const float hole = .25f;
            const float span = .08f;
            SetPath(
                deformationShader,
                (Vector3.forward * hole, Vector3.forward, 1 << 14),
                (Vector3.forward * (hole + span), Vector3.forward, 0)
            );
            var cone = SmoothCollapseLength;
            var scale = span / 2 / (TaperFullRadiusLength + cone);
            var middleOfCone = hole + scale * (TaperFullRadiusLength + cone / 2);
            material.SetFloat("_SampleDistance", middleOfCone);
            var closing = Render();
            Assert.That(closing.a, Is.EqualTo(.5f).Within(.0001f));
            Assert.That(closing.b, Is.EqualTo(middleOfCone).Within(.0001f));
            material.SetFloat("_SampleDistance", hole + span * .5f);
            Assert.That(Render().a, Is.Zero.Within(.0001f));
        }

        [Test]
        public void TerminalHoleChainPinsToItsEndsAcrossIntermediateNodes() {
            const float hole = .1f;
            var terminal = new Vector3(.2f, 0, .65f);
            SetPath(
                deformationShader,
                (Vector3.forward * hole, Vector3.forward, 1 << 14),
                (Vector3.forward * .35f, Vector3.forward, 1 << 14),
                (terminal, Vector3.right, 1 << 14)
            );
            var taperEnd = Vector3.forward * (hole + TaperFullRadiusLength + SmoothCollapseLength);
            foreach (var distance in new[] { .349f, .35f, .351f, .65f }) {
                material.SetFloat("_SampleDistance", distance);
                var sample = Render();
                var center = new Vector3(sample.r, sample.g, sample.b);
                var expected = distance < .4f ? taperEnd : terminal;
                Assert.That(Vector3.Distance(center, expected), Is.LessThan(.0001f));
                Assert.That(sample.a, Is.Zero.Within(.0001f));
            }
        }

        [Test]
        public void IntermediateHoleKeepsTheOriginalTaperEndpoint() {
            const float hole = .125f;
            SetPath(
                deformationShader,
                (Vector3.forward * hole, Vector3.forward, 1 << 14),
                (new Vector3(.0625f, 0, .25f), Vector3.right, 1 << 14),
                (new Vector3(1, 0, .25f), Vector3.right, 0)
            );
            material.SetFloat("_SampleDistance", hole + TaperFullRadiusLength + SmoothCollapseLength + .002f);
            var end = Render();
            material.SetFloat("_SampleDistance", .45f);
            var afterNode = Render();
            Assert.That(end.a, Is.Zero.Within(.0001f));
            Assert.That(afterNode.a, Is.Zero.Within(.0001f));
            Assert.That(afterNode.r, Is.EqualTo(end.r).Within(.0001f));
            Assert.That(afterNode.b, Is.EqualTo(end.b).Within(.0001f));
        }

        [Test]
        public void GuidedPathEntranceHoleWithoutAdvanceKeepsItsFullTaper() {
            const float entrance = .2f;
            const float coneStart = entrance + TaperFullRadiusLength;
            SetPath(
                deformationShader,
                (Vector3.forward * entrance, Vector3.forward, 1 << 14),
                (Vector3.forward * .5f, Vector3.forward, 0),
                (Vector3.forward * .6f, Vector3.forward, 1 << 14),
                (Vector3.forward * .9f, Vector3.forward, 0)
            );
            var coneLength = SmoothCollapseLength;
            material.SetFloat("_SampleDistance", Mathf.Max(0, coneStart - .001f));
            Assert.That(Render().a, Is.EqualTo(1).Within(.0001f));
            var taperMiddle = coneStart + coneLength / 2;
            material.SetFloat("_SampleDistance", taperMiddle);
            var taper = Render();
            Assert.That(taper.a, Is.EqualTo(.5f).Within(.0001f));
            Assert.That(taper.b, Is.EqualTo(taperMiddle).Within(.0001f));
            material.SetFloat("_SampleDistance", coneStart + coneLength + .001f);
            var justCollapsed = Render();
            Assert.That(justCollapsed.a, Is.Zero.Within(.0001f));
            Assert.That(justCollapsed.b, Is.EqualTo(coneStart + coneLength).Within(.0001f));
            material.SetFloat("_SampleDistance", .6f + TaperFullRadiusLength - .001f);
            Assert.That(Render().a, Is.EqualTo(1).Within(.0001f));
            material.SetFloat("_SampleDistance", .6f + TaperFullRadiusLength + coneLength / 2);
            Assert.That(Render().a, Is.EqualTo(.5f).Within(.0001f));
            material.SetFloat("_SampleDistance", .6f + TaperFullRadiusLength + coneLength + .001f);
            Assert.That(Render().a, Is.Zero.Within(.0001f));
            material.SetFloat("_SampleDistance", .91f);
            Assert.That(Render().a, Is.EqualTo(1).Within(.0001f));
        }

        [TestCase(.225f, false)]
        [TestCase(.254f, false)]
        [TestCase(.225f, true)]
        [TestCase(.254f, true)]
        public void EntranceHoleTaperContinuesPastGuidedHole(float stop, bool terminal) {
            var entrance = (Vector3.forward * .2f, Vector3.forward, 1 << 14);
            var guidedHole = (Vector3.forward * stop, Vector3.forward, 1 << 14);
            if (terminal) {
                SetPath(deformationShader, entrance, guidedHole);
            } else {
                SetPath(deformationShader, entrance, guidedHole, (Vector3.forward * .5f, Vector3.forward, 0));
            }
            var coneLength = SmoothCollapseLength;
            const float coneStart = .2f + TaperFullRadiusLength;
            if (stop < coneStart) {
                material.SetFloat("_SampleDistance", .24f);
                Assert.That(Render().a, Is.EqualTo(1).Within(.0001f));
            }
            material.SetFloat("_SampleDistance", coneStart + coneLength / 2);
            Assert.That(Render().a, Is.EqualTo(.5f).Within(.0001f));
            material.SetFloat("_SampleDistance", .38f);
            var collapsed = Render();
            Assert.That(collapsed.a, Is.Zero.Within(.0001f));
            Assert.That(collapsed.b, Is.EqualTo(terminal ? stop : .5f).Within(.0001f));
        }

        [TestCase(0f, 1f)]
        [TestCase(.25f, 1f)]
        [TestCase(.5f, 1f)]
        [TestCase(.75f, .5f)]
        [TestCase(1f, 0f)]
        [TestCase(2f, 0f)]
        public void TerminalGuidedHoleTapersAtTheNode(float taperFraction, float expectedRadius) {
            const float stop = .4f;
            SetPath(
                deformationShader,
                (Vector3.forward * .2f, Vector3.forward, 0),
                (Vector3.forward * stop, Vector3.forward, 1 << 14)
            );
            const float plugLength = 2f;
            material.SetFloat("_SPS_BakedLength", plugLength);
            var pastNode = taperFraction * StationaryTaperLength * plugLength;
            material.SetFloat("_SampleDistance", stop + pastNode);
            var result = Render();
            Assert.That(result.b, Is.EqualTo(stop).Within(.0001f));
            Assert.That(result.a, Is.EqualTo(expectedRadius).Within(.0001f));
        }

        [Test]
        public void LateJoinAtTiltedTerminalHoleTapersOnlyAfterReachingIt() {
            var stop = new Vector3(.1f, 0, .015f);
            var entrance = new Vector3(.1f, 0, .005f);
            SetPath((entrance, Vector3.forward, 0));
            material.SetFloat("_Operation", 9);
            var remaining = Render().b - stop.z;
            var expected = stop + Vector3.left * remaining;
            SetPath(
                deformationShader,
                (entrance, Vector3.forward, 0),
                (stop, Vector3.left, 1 << 14)
            );
            material.SetFloat("_SampleDistance", 0);
            Assert.That(Render().a, Is.EqualTo(1).Within(.0001f));
            material.SetFloat("_SampleDistance", .3f);
            var sample = Render();
            Assert.That(sample.r, Is.EqualTo(expected.x).Within(.0001f));
            Assert.That(sample.b, Is.EqualTo(expected.z).Within(.0001f));
            Assert.That(sample.a, Is.Zero.Within(.0001f));
        }

        [Test]
        public void IndependentGuidedPathEntranceStartsItsOwnTaper() {
            SetPath(deformationShader, (Vector3.forward * .1f, Vector3.forward, 0));
            var rotation = Quaternion.LookRotation(Vector3.back, Vector3.up);
            SetContacts(
                "SPS_Self",
                Matrix4x4.identity,
                Matrix4x4.TRS(Vector3.forward * .1f, rotation, Vector3.one),
                Matrix4x4.TRS(Vector3.forward * .2f, rotation, new Vector3(2 | (1 << 14), 1, 1))
            );
            SetContacts(
                "SPS_Path",
                Matrix4x4.identity,
                Matrix4x4.TRS(Vector3.forward * .225f, rotation, new Vector3(130 | (1 << 14), 1, 1)),
                Matrix4x4.TRS(Vector3.forward * .5f, rotation, new Vector3(258, 1, 1))
            );
            var taperMiddle = .2f + TaperFullRadiusLength + SmoothCollapseLength / 2;
            material.SetFloat("_SampleDistance", taperMiddle);
            var result = Render();
            Assert.That(result.a, Is.EqualTo(.5f).Within(.0001f));
            Assert.That(result.b, Is.EqualTo(taperMiddle).Within(.0001f));
            material.SetFloat("_SampleDistance", .55f);
            Assert.That(Render().a, Is.EqualTo(1).Within(.0001f), "The ring reopens the second path.");
        }

        [TestCase(false)]
        [TestCase(true)]
        public void AdvancedEntranceConsumesTaperSinceTheLastRing(bool crossesRing) {
            const float advance = .004f;
            SetPath(
                deformationShader,
                (Vector3.forward * -advance, Vector3.forward, 1 << 14),
                (Vector3.forward * (-advance * .75f), Vector3.forward, crossesRing ? 0 : 1 << 14),
                (Vector3.forward * (-advance * .5f), Vector3.forward, 1 << 14),
                (Vector3.forward * .5f, Vector3.forward, 0)
            );
            var coneLength = SmoothCollapseLength;
            var coneStart = -advance * (crossesRing ? .5f : 1) + TaperFullRadiusLength;
            material.SetFloat("_SampleDistance", coneStart + coneLength / 2);
            Assert.That(Render().a, Is.EqualTo(.5f).Within(.0001f));
            material.SetFloat("_SampleDistance", coneStart + coneLength + .001f);
            Assert.That(Render().a, Is.Zero.Within(.0001f));
            material.SetFloat("_SampleDistance", .51f);
            Assert.That(Render().a, Is.EqualTo(1).Within(.0001f));
        }

        [TestCase(.02f)]
        [TestCase(.04f)]
        public void UnguidedTerminalHoleTaperIsIndependentOfPlugRadius(float radius) {
            SetPath(deformationShader, (Vector3.forward * .2f, Vector3.forward, 1 << 14));
            material.SetFloat("_SPS_BakedRadius", radius);
            var coneLength = SmoothCollapseLength;
            const float coneStart = .2f + TaperFullRadiusLength;
            material.SetFloat("_SampleDistance", .225f);
            var fullRadius = Render();
            Assert.That(fullRadius.a, Is.EqualTo(1).Within(.0001f));
            Assert.That(fullRadius.b, Is.EqualTo(.225f).Within(.0001f));
            var taperMiddle = coneStart + coneLength / 2;
            material.SetFloat("_SampleDistance", taperMiddle);
            var taper = Render();
            Assert.That(taper.a, Is.EqualTo(.5f).Within(.001f));
            Assert.That(taper.b, Is.EqualTo(taperMiddle).Within(.0001f));
            var taperEnd = coneStart + coneLength;
            material.SetFloat("_SampleDistance", taperEnd + .01f);
            var collapsed = Render();
            Assert.That(collapsed.a, Is.Zero.Within(.0001f));
            Assert.That(collapsed.b, Is.EqualTo(taperEnd).Within(.0001f));
        }

        [Test]
        public void AlternateProfilesPinClosingHolesAndMoveThroughReopeningsAndTerminalHoles() {
            var hole = (Vector3.forward * .2f, Vector3.forward, 1 << 14);
            var ring = (Vector3.forward * .7f, Vector3.forward, 0);
            void Check(float distance, float position) {
                material.SetFloat("_SampleDistance", distance);
                var sample = Render();
                Assert.That(sample.b, Is.EqualTo(position).Within(.0001f));
                Assert.That(sample.a, Is.EqualTo(.5f).Within(.0001f));
            }
            SetPath(alternateTaperShader, hole);
            Check(.25f, .2f);
            SetPath(alternateTaperShader, hole, ring);
            Check(.25f, .2f);
            Check(.6f, .6f);
            SetPath(alternateTaperShader, (Vector3.forward * .1f, Vector3.forward, 0), hole);
            Check(.3f, .3f);
        }

        [TestCase(false, false, 4, false)]
        [TestCase(false, false, 6, true)]
        [TestCase(true, false, 6, true)]
        [TestCase(false, true, 0, true)]
        [TestCase(false, true, 2, false)]
        public void NativeZFlagsOverrideInstantCollapse(bool reopens, bool terminalGuide, int zFlags, bool instant) {
            const float holePosition = .2f;
            const float sampleDistance = holePosition + TaperFullRadiusLength + SmoothCollapseLength / 2;
            var hole = (Vector3.forward * holePosition, Vector3.forward, 1 << 14);
            var nodes = terminalGuide
                ? new[] { (Vector3.forward * .1f, Vector3.forward, 0), hole }
                : reopens ? new[] { hole, (Vector3.forward * .7f, Vector3.forward, 0) } : new[] { hole };
            SetPath(deformationShader, nodes, terminalGuide ? 1 : 0, zFlags | (1 << 14));
            material.SetFloat("_SampleDistance", sampleDistance);
            var sample = Render();
            Assert.That(sample.b, Is.EqualTo(instant ? holePosition : sampleDistance).Within(.0001f));
            Assert.That(sample.a, Is.EqualTo(.5f).Within(.0001f));
        }

        [TestCase(2, false)]
        [TestCase(6, true)]
        public void NativeZFlagsOverrideInstantReopening(int zFlags, bool instant) {
            const float ringPosition = .7f;
            const float sampleDistance = ringPosition - TaperFullRadiusLength - SmoothCollapseLength / 2;
            var nodes = new[] {
                (Vector3.forward * .2f, Vector3.forward, 1 << 14),
                (Vector3.forward * ringPosition, Vector3.forward, 0)
            };
            SetPath(deformationShader, nodes, 1, zFlags);
            material.SetFloat("_SampleDistance", sampleDistance);
            var sample = Render();
            Assert.That(sample.b, Is.EqualTo(instant ? ringPosition : sampleDistance).Within(.0001f));
            Assert.That(sample.a, Is.EqualTo(.5f).Within(.0001f));
        }
    }
}
