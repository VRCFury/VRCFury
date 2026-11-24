#if VRCSDK_HAS_CONTACT_PARAMETER_FLAGS
#if !AV3EMU_HAS_ADVANCED_CONTACTS
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
#if !AV3EMU_HAS_ADVANCED_CONTACTS
    using VF.Hooks.Av3EmuFixes;
#endif
    using VF.Model.Feature;
    using VF.Service;
    using VF.Utils;
    using VF.Utils.Controller;
    using VRC.SDK3.Avatars.Components;
    using VRC.SDK3.Dynamics.Contact.Components;
    using VRC.Dynamics;
    using VRC.SDKBase;
    using Object = UnityEngine.Object;

    internal class Av3EmuAdvancedContactsFixHookTest : AdvancedContactParameterBridgeTestBase {
        [Test]
        public void Av3EmuAdvancedContactsUseThePlayableParameterBridge() {
            VerifyAdvancedContactParameterBridge((receiver, playableAccess) => {
                receiver.paramAccess = new Av3EmuParameterAccess { runtime = playableAccess, paramName = receiver.parameter };
                // A skipped remote contact must retain its existing advanced accessors.
                var before = receiver.param_PointSeparation;
                Av3EmuAdvancedContactsFixHook.Postfix(new object(), new[] { receiver });
                Assert.That(receiver.param_PointSeparation, Is.SameAs(before));
                Av3EmuAdvancedContactsFixHook.Postfix(playableAccess, new[] { receiver });
            });
        }

        public sealed class Av3EmuParameterAccess : IAnimParameterAccess {
            public Func<string, IAnimParameterAccess> runtime;
            public string paramName;
            public float floatVal { get => runtime(paramName).floatVal; set => runtime(paramName).floatVal = value; }
            public int intVal { get => (int)floatVal; set => floatVal = value; }
            public bool boolVal { get => floatVal != 0; set => floatVal = value ? 1 : 0; }
        }
    }
}
#endif
#endif
