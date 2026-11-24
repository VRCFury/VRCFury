#if VRCSDK_HAS_CONTACT_PARAMETER_FLAGS
namespace VF.Tests {
    using static VF.Inspector.VRCFuryHapticPlugEditor;
    using Party = VF.Builder.Haptics.SelfAndOthersReceiverService.Party;
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Reflection;
    using NUnit.Framework;
    using UnityEditor.Animations;
    using UnityEngine;
    using UnityEngine.Animations;
    using UnityEngine.Playables;
    using VF.Builder;
    using VF.Builder.Haptics;
    using VF.Component;
    using VF.Injector;
    using VF.Inspector;
    using VF.Model.Feature;
    using VF.Service;
    using VF.Utils;
    using VF.Utils.Controller;
    using VRC.SDK3.Avatars.Components;
    using VRC.SDK3.Dynamics.Contact.Components;
    using VRC.Dynamics;
    using VRC.SDKBase;
    using Object = UnityEngine.Object;

    internal class SpsDepthAnimationServiceTest : SpsAvatarBuildTestBase {
        [TestCase(0f, false, false)]
        [TestCase(0.1f, false, false)]
        [TestCase(0.1f, false, true)]
        [TestCase(0f, true, false)]
        [TestCase(0f, true, true)]
        [TestCase(0.1f, true, false)]
        [TestCase(0f, true, false, true)]
        [TestCase(0.1f, true, true, true)]
        public void PlugEntryRadiusAnimationsUseSampledRadiusWithDepthAnimationOptions(float smoothingSeconds, bool motionTime, bool self, bool savedReverseClip = false) {
            if (!BuildTargetUtils.IsDesktop()) Assert.Ignore("Requires desktop SPS baking");
            injector.Set("componentObject", new Func<VFGameObject>(() => root));
            var globals = injector.GetService<GlobalsService>();
            globals.allBuildersInRun = new List<VF.Feature.Base.FeatureBuilder>();
            globals.allFeaturesInRun = new List<FeatureModel> {
                new SpsOptions { autoModeAvailable = false, legacyModeAvailable = false }
            };
            globals.addOtherFeature = _ => { };
            var socket = root.AddComponent<VRCFuryHapticSocket>();
            socket.addMenuItem = true;
            socket.name = "RadiusTest";
            socket.channelSettings.useSharedTag = false;
            socket.channelSettings.channels.Add(new VF.Model.SpsChannelSettings.Channel { id = "test" });
            var output = fx.GetOrCreateFloat("Radius action output");
            var action = new VRCFuryHapticSocket.PlugEntryRadiusAction {
                range = new Vector2(0, 0.1f), smoothingSeconds = smoothingSeconds, enableSelf = self, reverseClip = savedReverseClip
            };
            if (motionTime) {
                var clip = VFClip.Create("Radius motion time");
                clip.SetAap(output, AnimationCurve.Linear(0, 0, 1, 1));
                action.actionSet.actions.Add(new VF.Model.StateAction.AnimationClipAction { vfClip = clip });
            } else {
                action.actionSet.actions.Add(new VF.Model.StateAction.FxFloatAction { name = output.name, value = 1 });
            }
            socket.plugEntryRadiusActions.Add(action);
            injector.GetService<BakeHapticSocketsService>().Apply();
            var toggle = SocketMenuParameter("RadiusTest");
            Start();
            playable.SetFloat(toggle, 1);
            var receiver = self ? Merged() : Main(false);
            Set(receiver, 0.9f, 0.66f, length: 0.6f, radius: 0.2f, native: true);
            playable.SetFloat(receiver.parameter + "_SenderLossyScaleY", 0.05f);
            Evaluate(120);
            Assert.That(playable.GetFloat(output), Is.Zero.Within(0.01f), "Radius animations stay off beyond 5% outside");
            Set(receiver, 0.9f, 0.615f, length: 0.6f, radius: 0.2f, native: true);
            playable.SetFloat(receiver.parameter + "_SenderLossyScaleY", 0.05f);
            Evaluate(120);
            Assert.That(playable.GetFloat(output), Is.EqualTo(0.25f).Within(0.01f), "2.5% outside gives half the sampled radius");
            Set(receiver, 1, 0.6f, length: 0.6f, radius: 0.2f, native: true);
            playable.SetFloat(receiver.parameter + "_SenderLossyScaleY", 0.05f);
            Evaluate(120);
            Assert.That(playable.GetFloat(output), Is.EqualTo(0.5f).Within(0.01f), "Tip touching the entrance permits radius animations");
            Set(receiver, 1, 0.3f, length: 0.6f, radius: 0.2f, native: true);
            playable.SetFloat(receiver.parameter + "_SenderLossyScaleY", 0.05f);
            Evaluate(120);
            Assert.That(playable.GetFloat(output), Is.EqualTo(0.5f).Within(0.01f));
            playable.SetFloat(receiver.parameter + "_SenderLossyScaleY", 0.0005f);
            Evaluate(120);
            Assert.That(playable.GetFloat(output), Is.EqualTo(0.005f).Within(0.0001f),
                "Radius animations must follow values below 1% of the animation range");
            playable.SetFloat(receiver.parameter + "_SenderLossyScaleY", 0.1f);
            Evaluate(120);
            Assert.That(playable.GetFloat(output), Is.EqualTo(1).Within(0.01f));
            Set(receiver, 0.9f, 0.66f, length: 0.6f, radius: 0.2f, native: true);
            playable.SetFloat(receiver.parameter + "_SenderLossyScaleY", 0.1f);
            if (smoothingSeconds > 0) {
                Evaluate(4);
                Assert.That(playable.GetFloat(output), Is.GreaterThan(0.01f), "Withdrawing must let smoothing fade the animation out");
            }
            Evaluate(120);
            Assert.That(playable.GetFloat(output), Is.Zero.Within(0.01f), "Withdrawing returns the animation to zero");
            Set(receiver, 1, 0.3f, length: 0.6f, radius: 0.2f, native: true);
            playable.SetFloat(receiver.parameter + "_SenderLossyScaleY", 0.1f);
            Evaluate(120);
            playable.SetFloat(toggle, 0);
            Evaluate(120);
            Assert.That(playable.GetFloat(output), Is.Zero.Within(0.01f), "Disabled sockets must stop radius animations");
            playable.SetFloat(toggle, 1);
            Evaluate(120);
            Assert.That(playable.GetFloat(output), Is.EqualTo(1).Within(0.01f));
            Set(receiver, 0, -1, length: 0.6f, radius: 0.2f, native: true);
            Evaluate(120);
            Assert.That(playable.GetFloat(output), Is.Zero.Within(0.01f));
        }

        [TestCase("party")]
        [TestCase("shape")]
        public void AnimationWithNoAllowedInteractionsIsSkipped(string pair) {
            root.AddComponent<VRCFuryHapticPlug>();
            injector.Set("componentObject", new Func<VFGameObject>(() => root));
            var action = new VRCFuryHapticSocket.DepthActionNew {
                enableSelf = pair != "party", enableOthers = pair != "party",
                allowHole = pair != "shape", allowRing = pair != "shape"
            };
            var parameters = fx.parameters.Length;
            var layers = fx.GetLayers().Count();
            Assert.DoesNotThrow(() => injector.GetService<SpsDepthAnimationService>().CreateAnims(new[] { action }, root,
                "Skipped", _ => throw new Exception("Skipped animations must not request measurements"), root));
            Assert.That(fx.parameters.Length, Is.EqualTo(parameters));
            Assert.That(fx.GetLayers().Count(), Is.EqualTo(layers));
        }

        [Test]
        public void PlugDepthAnimationFiltersBeforeSmoothing(
            [Values(false, true)] bool motionTime, [Values(0f, 0.1f)] float seconds,
            [Values(false, true)] bool selfOnly, [Values(false, true)] bool holeAllowed,
            [Values(false, true)] bool reverse) {
            root.AddComponent<VRCFuryHapticPlug>();
            injector.Set("componentObject", new Func<VFGameObject>(() => root));
            var depth = fx.CreateUniqueFloat("Filtered depth", -0.125f);
            var hole = fx.CreateUniqueFloat("Filtered hole");
            var output = fx.GetOrCreateFloat("Filtered output");
            var action = new VRCFuryHapticSocket.DepthActionNew {
                range = new Vector2(-0.25f, 0), units = VRCFuryHapticSocket.DepthActionUnits.Meters,
                enableSelf = selfOnly, enableOthers = !selfOnly,
                allowHole = holeAllowed, allowRing = !holeAllowed,
                smoothingSeconds = seconds, reverseClip = reverse
            };
            if (motionTime) {
                var clip = VFClip.Create("Filtered motion");
                clip.SetAap(output, AnimationCurve.Linear(0, 0, 1, 1));
                action.actionSet.actions.Add(new VF.Model.StateAction.AnimationClipAction { vfClip = clip });
            } else action.actionSet.actions.Add(new VF.Model.StateAction.FxFloatAction { name = output.name, value = 1 });
            injector.GetService<SpsDepthAnimationService>().CreateAnims(new[] { action }, root, "Filtered", party => {
                Assert.That(party, Is.EqualTo(selfOnly ? Party.Self : Party.Others));
                return source => source == SpsSocketIsHole ? hole : depth;
            }, root);
            Start();
            foreach (var isHole in new[] { false, true }) {
                playable.SetFloat(hole, isHole ? 1 : 0);
                Evaluate(120);
                Assert.That(playable.GetFloat(output), Is.EqualTo(isHole == holeAllowed ? 0.5f : 0).Within(0.001f));
            }
            if (seconds > 0 && !reverse) {
                playable.SetFloat(hole, holeAllowed ? 1 : 0);
                Evaluate(120);
                playable.SetFloat(hole, holeAllowed ? 0 : 1);
                Evaluate(3);
                Assert.That(playable.GetFloat(output), Is.GreaterThan(0.001f), "Filters feed smoothing instead of snapping the animation off");
                Evaluate(120);
                Assert.That(playable.GetFloat(output), Is.Zero.Within(0.001f));
            }
        }

        [Test]
        public void SocketDepthAnimationWeightsByLocalPlugRadius(
            [Values(false, true)] bool motionTime,
            [Values(false, true)] bool optimize,
            [Values(0f, 0.1f)] float smoothingSeconds,
            [Values(0f, 0.05f)] float zeroRadius) {
            optimizeTrees = optimize;
            root.AddComponent<VRCFuryHapticSocket>();
            injector.Set("componentObject", new Func<VFGameObject>(() => root));
            var depth = fx.CreateUniqueFloat("Raw depth", -0.125f);
            var radius = fx.CreateUniqueFloat("Local plug radius", (zeroRadius + 0.25f) / 2);
            var output = fx.GetOrCreateFloat("Weighted depth output");
            var action = new VRCFuryHapticSocket.DepthActionNew {
                range = new Vector2(-0.25f, 0), plugRadiusForFullWeight = 0.25f,
                smoothingSeconds = smoothingSeconds
            };
            action.plugRadiusForZeroWeight = zeroRadius;
            if (motionTime) {
                var clip = VFClip.Create("Weighted motion time");
                clip.SetAap(output, AnimationCurve.Linear(0, 0, 1, 1));
                action.actionSet.actions.Add(new VF.Model.StateAction.AnimationClipAction { vfClip = clip });
            } else {
                action.actionSet.actions.Add(new VF.Model.StateAction.FxFloatAction { name = output.name, value = 1 });
            }
            injector.GetService<SpsDepthAnimationService>().CreateAnims(new[] { action }, root, "Weighted",
                _ => measurement => measurement == SpsPlugRadiusLocal ? radius : depth, root);
            Start();
            Evaluate(120);
            Assert.That(playable.GetFloat(output), Is.EqualTo(0.25f).Within(0.01f), "Half depth times half radius weight");
            playable.SetFloat(radius, 0.5f);
            Evaluate(120);
            Assert.That(playable.GetFloat(output), Is.EqualTo(0.5f).Within(0.01f), "Radius weight must cap at one");
            playable.SetFloat(radius, 0);
            Evaluate(120);
            Assert.That(playable.GetFloat(output), Is.Zero.Within(0.001f));
        }

        [Test]
        public void ZeroPlugWidthLetsLowerLayersDriveTheDepthAction(
            [Values(false, true)] bool motionTime,
            [Values(false, true)] bool optimize,
            [Values(false, true)] bool reverse,
            [Values(0f, 0.05f)] float zeroRadius) {
            optimizeTrees = optimize;
            root.AddComponent<VRCFuryHapticSocket>();
            injector.Set("componentObject", new Func<VFGameObject>(() => root));
            var depth = fx.CreateUniqueFloat("Raw depth", -0.0625f);
            var radius = fx.CreateUniqueFloat("Local plug radius");
            var output = fx.GetOrCreateFloat("Shared weighted output");
            fx.NewLayer("Lower output").NewState("Lower output").WithAnimation(output.MakeSetter(0.6f));
            var action = new VRCFuryHapticSocket.DepthActionNew {
                range = new Vector2(-0.25f, 0), plugRadiusForFullWeight = 0.25f, reverseClip = reverse
            };
            var clip = VFClip.Create("Weighted depth action");
            action.plugRadiusForZeroWeight = zeroRadius;
            clip.SetAap(output, motionTime ? AnimationCurve.Linear(0, 0, 1, 1) : AnimationCurve.Constant(0, 0, 1));
            action.actionSet.actions.Add(new VF.Model.StateAction.AnimationClipAction { vfClip = clip });
            injector.GetService<SpsDepthAnimationService>().CreateAnims(new[] { action }, root, "Shared weighted",
                _ => measurement => measurement == SpsPlugRadiusLocal ? radius : depth, root);
            Start();
            Evaluate(120);
            Assert.That(playable.GetFloat(output), Is.EqualTo(0.6f).Within(0.001f));
            playable.SetFloat(radius, 0.125f);
            Evaluate(120);
            var radiusWeight = (0.125f - zeroRadius) / (0.25f - zeroRadius);
            Assert.That(playable.GetFloat(output), Is.EqualTo((motionTime && reverse ? 0.75f : 0.25f) * radiusWeight).Within(0.001f));
            foreach (var value in new[] { zeroRadius, 0, -0.125f }) {
                playable.SetFloat(radius, value);
                Evaluate(120);
                Assert.That(playable.GetFloat(output), Is.EqualTo(0.6f).Within(0.001f), "Zero width must release the shared property");
            }
        }

        [Test]
        public void WidthMultiplierUsesSharedReceiversAndSocketLocalUnits(
            [Values(0, 1, 2)] int unitsIndex,
            [Values(0, 1, 2)] int partyIndex) {
            var units = (VRCFuryHapticSocket.DepthActionUnits)unitsIndex;
            var party = (Party)partyIndex;
            var measurements = Socket(legacy: false);
            _ = measurements(party)(SpsDepthMeters);
            var receiverCount = party == Party.Merged ? 4 : 1;
            injector.Set("componentObject", new Func<VFGameObject>(() => root));
            var output = fx.GetOrCreateFloat("Local width output");
            var action = new VRCFuryHapticSocket.DepthActionNew {
                range = new Vector2(-0.5f, 0), units = units, plugRadiusForFullWeight = 0.2f,
                enableSelf = party != Party.Others, enableOthers = party != Party.Self
            };
            action.actionSet.actions.Add(new VF.Model.StateAction.FxFloatAction { name = output.name, value = 1 });
            injector.GetService<SpsDepthAnimationService>().CreateAnims(new[] { action }, root, "Local width", measurements, root);
            Start();
            var receiver = party == Party.Merged ? Merged() : Main(party == Party.Self);
            Assert.That(root.GetComponentsInChildren<VRCContactReceiver>(true), Has.Length.EqualTo(receiverCount),
                "Width weighting must reuse the depth/deformation receivers");
            foreach (var resize in new[] { 0.5f, 1, 2 }) {
                root.transform.localScale = Vector3.one * resize;
                playable.SetFloat(scale, resize);
                Set(receiver, 1, 0.35f * resize, length: 0.6f * resize, radius: 0.1f * resize, native: true);
                Evaluate(120);
                var depthWeight = units == VRCFuryHapticSocket.DepthActionUnits.Local ? 0.5f
                    : units == VRCFuryHapticSocket.DepthActionUnits.Meters ? Mathf.Clamp01(0.5f * resize)
                    : 0.25f / 0.6f / 0.5f;
                Assert.That(playable.GetFloat(output), Is.EqualTo(depthWeight * 0.5f).Within(0.003f),
                    "Width weight must use local units independently of depth units, party, and live avatar scale");
            }
        }

        [TestCase(false)]
        [TestCase(true)]
        public void WidthMultiplierIsIgnoredForPlugDepthAndSocketRadius(bool plug) {
            if (plug) root.AddComponent<VRCFuryHapticPlug>();
            else root.AddComponent<VRCFuryHapticSocket>();
            injector.Set("componentObject", new Func<VFGameObject>(() => root));
            var input = fx.CreateUniqueFloat("Input", plug ? -0.125f : 0.125f);
            var output = fx.GetOrCreateFloat("Output");
            var action = new VRCFuryHapticSocket.DepthActionNew {
                range = plug ? new Vector2(-0.25f, 0) : new Vector2(0, 0.25f), plugRadiusForFullWeight = 0.25f
            };
            action.actionSet.actions.Add(new VF.Model.StateAction.FxFloatAction { name = output.name, value = 1 });
            injector.GetService<SpsDepthAnimationService>().CreateAnims(new[] { action }, root, "Unsupported width",
                _ => source => {
                    Assert.That(source, Is.Not.EqualTo(SpsPlugRadiusLocal), "Unsupported actions must not request width measurements");
                    return input;
                }, root, plug ? SpsParamDepth : SpsParamPlugEntryRadius);
            Start();
            Evaluate(120);
            Assert.That(playable.GetFloat(output), Is.EqualTo(0.5f).Within(0.001f));
        }

        [TestCase(0f, false)]
        [TestCase(0.1f, false)]
        [TestCase(0f, true)]
        public void DepthActionsKeepTheirRangeAndOnlyMapWhenRequired(float smoothingSeconds, bool motionTime) {
            injector.Set("componentObject", new Func<VFGameObject>(() => root));
            var input = fx.CreateUniqueFloat("Raw depth", 0);
            var output = fx.GetOrCreateFloat("Depth action output");
            var action = new VRCFuryHapticSocket.DepthActionNew {
                range = new Vector2(-0.25f, 0), units = VRCFuryHapticSocket.DepthActionUnits.Meters,
                smoothingSeconds = smoothingSeconds
            };
            if (motionTime) {
                var clip = VFClip.Create("MotionTime action");
                clip.SetAap(output, AnimationCurve.Linear(0, 0, 1, 1));
                action.actionSet.actions.Add(new VF.Model.StateAction.AnimationClipAction { vfClip = clip });
            } else {
                action.actionSet.actions.Add(new VF.Model.StateAction.FxFloatAction { name = output.name, value = 1 });
            }
            injector.GetService<SpsDepthAnimationService>().CreateAnims(new[] { action }, root, "Test",
                _ => _ => input, root);
            var requiresMap = smoothingSeconds > 0 || motionTime;
            Assert.That(fx.parameters.Any(p => p.name.Contains("/Mapped")), Is.EqualTo(motionTime && smoothingSeconds <= 0));
            Start();
            Evaluate(5);
            Assert.That(playable.GetFloat(output), Is.Zero.Within(0.001f));
            playable.SetFloat(input, -0.125f);
            Evaluate(90);
            Assert.That(playable.GetFloat(output), Is.EqualTo(0.5f).Within(0.01f));
            if (!requiresMap) {
                playable.SetFloat(input, -0.2f);
                Evaluate(1);
                Assert.That(playable.GetFloat(output), Is.EqualTo(0.8f).Within(0.001f),
                    "Static unsmoothed actions must consume the current depth without a mapping AAP");
            }
            playable.SetFloat(input, -0.001f);
            Evaluate(120);
            Assert.That(playable.GetFloat(output), Is.EqualTo(0.004f).Within(0.0001f), "Depth animations must follow values below 1% of the animation range");
            playable.SetFloat(input, 0.1f);
            Evaluate(120);
            Assert.That(playable.GetFloat(output), Is.Zero.Within(0.0001f));
        }

        [Test]
        public void EnableSpsDisablesMotionTimeDepthActions(
            [Values(false, true)] bool reverse,
            [Values(false, true)] bool optimize
        ) {
            if (!BuildTargetUtils.IsDesktop()) Assert.Ignore("Requires desktop SPS baking.");
            optimizeTrees = optimize;
            injector.Set("componentObject", new Func<VFGameObject>(() => root));
            var plug = root.AddComponent<VRCFuryHapticPlug>();
            plug.autoRenderer = plug.autoLength = plug.autoRadius = plug.enableSps = false;
            plug.length = .4f;
            plug.radius = .05f;
            var globals = injector.GetService<GlobalsService>();
            globals.allBuildersInRun = new List<VF.Feature.Base.FeatureBuilder>();
            globals.addOtherFeature = _ => { };
            var baker = injector.GetService<BakeHapticPlugsService>();
            baker.ApplyEarlyBake();
            var bakeRoot = injector.GetService<PlugFrameService>().Get(plug);
            var input = fx.CreateUniqueFloat("Depth", -.125f);
            var output = fx.CreateUniqueFloat("Depth output");
            var baseline = VFClip.Create("Lower layer");
            baseline.SetAap(output, .2f);
            fx.NewLayer("Lower layer").NewState("Lower layer").WithAnimation(baseline);
            var enabled = fx.CreateUniqueFloat("Enable SPS", 1);
            enabled.MakeExpressionFloat(synced: false);
            var builder = injector.GetService<VF.Actions.SpsOnActionBuilder>();
            var enable = new VF.Model.StateAction.SpsOnAction { target = plug };
            fx.NewLayer("Enable SPS").NewState("Enable SPS").WithAnimation(VFBlendTree1D.CreateWithData(
                "Enable SPS",
                enabled,
                (0, builder.BuildOff(enable)),
                (1, builder.Build(enable))
            ));
            var clip = VFClip.Create("Motion time");
            clip.SetAap(output, AnimationCurve.Linear(0, 0, 1, 1));
            var action = new VRCFuryHapticSocket.DepthActionNew {
                range = new Vector2(-.25f, 0),
                units = VRCFuryHapticSocket.DepthActionUnits.Meters,
                smoothingSeconds = 0,
                reverseClip = reverse
            };
            action.actionSet.actions.Add(new VF.Model.StateAction.AnimationClipAction { vfClip = clip });
            injector.GetService<SpsDepthAnimationService>().CreateAnims(new[] { action }, root, "Test", _ => _ => input, bakeRoot);
            baker.ApplySpsRewrites();
            Start();
            Evaluate(30);
            Assert.That(playable.GetFloat(output), Is.EqualTo(.5f).Within(.001f));
            playable.SetFloat(enabled, 0);
            Evaluate(10);
            Assert.That(playable.GetFloat(output), Is.EqualTo(.2f).Within(.001f), "Disabled depth actions must inherit lower layers.");
            playable.SetFloat(enabled, 1);
            Evaluate(30);
            Assert.That(playable.GetFloat(output), Is.EqualTo(.5f).Within(.001f));
        }

        [Test]
        public void InactiveDepthActionsInheritSharedAapFromLowerLayers(
            [Values(false, true)] bool optimize,
            [Values(0f, 0.1f)] float smoothingSeconds,
            [Values(false, true)] bool motionTime,
            [Values(false, true)] bool plugs,
            [Values(false, true)] bool sameOwner) {
            optimizeTrees = optimize;
            injector.Set("componentObject", new Func<VFGameObject>(() => root));
            var output = fx.GetOrCreateFloat("Shared depth output");
            var baseline = VFClip.Create("Baseline");
            baseline.SetAap(output, 0.2f);
            fx.NewLayer("Baseline").NewState("Baseline").WithAnimation(baseline);
            var inputs = new[] { fx.CreateUniqueFloat("Lower depth", 0.1f), fx.CreateUniqueFloat("Upper depth", 0.1f) };
            for (var i = 0; i < inputs.Length; i++) {
                var owner = sameOwner ? root.asVf() : GameObjects.Create((plugs ? "Plug " : "Socket ") + i, root);
                if (!sameOwner || i == 0) {
                    if (plugs) owner.AddComponent<VRCFuryHapticPlug>();
                    else owner.AddComponent<VRCFuryHapticSocket>();
                }
                var action = new VRCFuryHapticSocket.DepthActionNew {
                    range = new Vector2(-0.25f, 0), units = VRCFuryHapticSocket.DepthActionUnits.Meters,
                    smoothingSeconds = smoothingSeconds
                };
                var clip = VFClip.Create("Depth action " + i);
                clip.SetAap(output, motionTime ? AnimationCurve.Linear(0, 0, 1, 1) : AnimationCurve.Constant(0, 0, 1));
                action.actionSet.actions.Add(new VF.Model.StateAction.AnimationClipAction { vfClip = clip });
                var input = inputs[i];
                injector.GetService<SpsDepthAnimationService>().CreateAnims(new[] { action }, owner,
                    owner.name, _ => _ => input, owner);
            }
            Start();
            void Check(float lower, float upper, float expected) {
                playable.SetFloat(inputs[0], lower);
                playable.SetFloat(inputs[1], upper);
                Evaluate(180);
                Assert.That(playable.GetFloat(output), Is.EqualTo(expected).Within(0.002f),
                    $"Lower depth {lower}, upper depth {upper}: inactive actions must inherit lower layers");
            }
            Check(0.1f, 0.1f, 0.2f);
            Check(-0.15f, 0.1f, 0.6f);
            Check(-0.15f, -0.2f, 0.8f);
            Check(-0.15f, 0.1f, 0.6f);
            Check(0.1f, 0.1f, 0.2f);
        }

        [TestCase(false)]
        [TestCase(true)]
        public void SmoothedDepthFoldsMappingAndPreservesTheResponseOneFrameEarlier(bool reverseMotion) {
            injector.Set("componentObject", new Func<VFGameObject>(() => root));
            var input = fx.CreateUniqueFloat("Raw depth", 0);
            var action = new VRCFuryHapticSocket.DepthActionNew {
                range = new Vector2(-0.25f, 0), units = VRCFuryHapticSocket.DepthActionUnits.Meters,
                smoothingSeconds = 0.1f, reverseClip = reverseMotion
            };
            var clip = VFClip.Create("Depth action");
            clip.SetAap(fx.GetOrCreateFloat("Depth action output"), reverseMotion ? AnimationCurve.Linear(0, 0, 1, 1) : AnimationCurve.Constant(0, 0, 1));
            action.actionSet.actions.Add(new VF.Model.StateAction.AnimationClipAction { vfClip = clip });
            injector.GetService<SpsDepthAnimationService>().CreateAnims(new[] { action }, root, "Test", _ => _ => input, root);
            Assert.That(fx.parameters.Any(p => p.name.Contains("/Mapped")), Is.False);
            var actual = fx.parameters.Single(p => p.name.Contains("/Smoothed/Pass2"));
            Assert.That(actual.GetDefault(), Is.EqualTo(reverseMotion ? 1 : 0));

            // Reproduce the former mapped-AAP + two-pass smoother with the same live speed.
            var speed = fx.parameters.Single(p => p.name.Contains("smoothingSpeed/"));
            var mapped = math.Map("Reference mapped", input, 0, -0.25f, 0, 1);
            mapped.SetDefault(actual.GetDefault());
            VFAParam ReferencePass(string name, VFAParam target) {
                var output = fx.CreateUniqueFloat(name, actual.GetDefault());
                tree.Add(VFBlendTree1D.CreateWithData(name, speed,
                    (0, output.MakeCopier(output)), (1, output.MakeCopier(target))));
                return output;
            }
            var reference = ReferencePass("Reference pass 2", ReferencePass("Reference pass 1", mapped));
            Start();
            Evaluate(120);
            foreach (var value in new[] { -0.125f, -0.5f, 0.2f, -0.2f }) {
                playable.SetFloat(input, value);
                var previous = playable.GetFloat(actual);
                for (var frame = 0; frame < 60; frame++) {
                    Evaluate(1);
                    Assert.That(playable.GetFloat(reference), Is.EqualTo(previous).Within(0.0001f),
                        $"Input {value}, frame {frame}");
                    previous = playable.GetFloat(actual);
                }
                Assert.That(playable.GetFloat(actual), Is.EqualTo(Mathf.Clamp01(value / -0.25f)).Within(0.001f));
            }
        }

        [TestCase(0f, "range")]
        [TestCase(0f, "input")]
        [TestCase(0f, "initial")]
        [TestCase(0.1f, "range")]
        [TestCase(0.1f, "input")]
        [TestCase(0.1f, "initial")]
        [TestCase(0.1f, "smoothing")]
        public void DepthActionsShareControlsOnlyWhenTheirSettingsMatch(float seconds, string difference) {
            injector.Set("componentObject", new Func<VFGameObject>(() => root));
            var selfInput = fx.CreateUniqueFloat("Merged depth", 0);
            var otherInput = fx.CreateUniqueFloat("Others depth", 0);
            var actions = Enumerable.Range(0, 3).Select(i => {
                var different = i == 2;
                var action = new VRCFuryHapticSocket.DepthActionNew {
                    range = new Vector2(different && difference == "range" ? -0.5f : -0.25f, 0),
                    units = VRCFuryHapticSocket.DepthActionUnits.Meters,
                    enableSelf = !(different && difference == "input"),
                    smoothingSeconds = different && difference == "smoothing" ? seconds * 2 : seconds,
                    reverseClip = different && difference == "initial"
                };
                var clip = VFClip.Create("MotionTime " + i);
                clip.SetAap(fx.GetOrCreateFloat("Output " + i), AnimationCurve.Linear(0, 0, 1, 1));
                action.actionSet.actions.Add(new VF.Model.StateAction.AnimationClipAction { vfClip = clip });
                return action;
            }).ToArray();
            injector.GetService<SpsDepthAnimationService>().CreateAnims(actions, root, "Test",
                party => _ => party == Party.Merged ? selfInput : otherInput, root);
            var controls = fx.parameters.Where(p => seconds > 0 ? p.name.Contains("/Smoothed/Pass2") : p.name.Contains("/Mapped")).ToArray();
            Assert.That(controls, Has.Length.EqualTo(2), "The first two actions share a control; the third must remain independent");
            Assert.That(controls.Select(p => p.GetDefault()), Is.EquivalentTo(difference == "initial" ? new[] { 0f, 1f } : new[] { 0f, 0f }));
            Start();
            playable.SetFloat(selfInput, -0.125f);
            playable.SetFloat(otherInput, -0.2f);
            Evaluate(180);
            Assert.That(playable.GetFloat("Output 0"), Is.EqualTo(0.5f).Within(0.01f));
            Assert.That(playable.GetFloat("Output 1"), Is.EqualTo(0.5f).Within(0.01f));
            var expected = difference == "range" ? 0.25f : difference == "input" ? 0.8f : 0.5f;
            Assert.That(playable.GetFloat("Output 2"), Is.EqualTo(expected).Within(0.01f));
        }

        [TestCase(0f, false)]
        [TestCase(0.2f, false)]
        [TestCase(0f, true)]
        [TestCase(0.2f, true)]
        public void DisabledDepthActionsResetTheirInputs(float smoothingSeconds, bool reverseMotion) {
            injector.Set("componentObject", new Func<VFGameObject>(() => root));
            var input = fx.CreateUniqueFloat("Raw depth", 0);
            var enabled = fx.CreateUniqueFloat("Socket enabled", 1);
            var output = fx.GetOrCreateFloat("Depth action output");
            var action = new VRCFuryHapticSocket.DepthActionNew {
                range = new Vector2(-0.25f, 0), units = VRCFuryHapticSocket.DepthActionUnits.Meters,
                smoothingSeconds = smoothingSeconds, reverseClip = reverseMotion
            };
            if (reverseMotion) {
                var clip = VFClip.Create("Reverse action");
                clip.SetAap(output, AnimationCurve.Linear(0, 0, 1, 1));
                action.actionSet.actions.Add(new VF.Model.StateAction.AnimationClipAction { vfClip = clip });
            } else {
                action.actionSet.actions.Add(new VF.Model.StateAction.FxFloatAction { name = output.name, value = 1 });
            }
            var actionRoot = GameObjects.Create("Action root", root);
            var on = VFClip.Create("Socket on"); on.SetEnabled(actionRoot, true);
            var off = VFClip.Create("Socket off"); off.SetEnabled(actionRoot, false);
            fx.NewLayer("Socket toggle").NewState("Socket toggle").WithAnimation(
                VFBlendTree1D.CreateWithData("Socket toggle", enabled, (0, off), (1, on)));
            injector.GetService<SpsDepthAnimationService>().CreateAnims(new[] { action }, root, "Test",
                _ => _ => input, actionRoot);
            var internalValues = fx.parameters.Where(p => p.name.Contains("/Mapped") || p.name.Contains("/Smoothed")).ToArray();
            Start();
            playable.SetFloat(input, reverseMotion ? 0 : -0.25f);
            Evaluate(120);
            Assert.That(playable.GetFloat(output), Is.EqualTo(1).Within(0.001f));
            playable.SetFloat(enabled, 0);
            Evaluate(4);
            Assert.That(playable.GetFloat(output), Is.Zero.Within(0.001f));
            foreach (var parameter in internalValues) {
                Assert.That(parameter.GetDefault(), Is.EqualTo(reverseMotion ? 1 : 0));
                Assert.That(playable.GetFloat(parameter), Is.EqualTo(parameter.GetDefault()));
            }
            Evaluate(20);
            playable.SetFloat(input, reverseMotion ? -0.25f : 0);
            playable.SetFloat(enabled, 1);
            if (!reverseMotion) {
                Evaluate(1);
                Assert.That(playable.GetFloat(output), Is.Zero.Within(0.001f), "A re-enabled socket must not reuse its old smoothed penetration");
            }
            Evaluate(120);
            Assert.That(playable.GetFloat(output), Is.Zero.Within(0.001f));
        }
    }
}
#endif
