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
    using VF.Hooks.GestureManagerFixes;
    using VF.Model.Feature;
    using VF.Service;
    using VF.Utils;
    using VF.Utils.Controller;
    using VRC.SDK3.Avatars.Components;
    using VRC.SDK3.Dynamics.Contact.Components;
    using VRC.Dynamics;
    using VRC.SDKBase;
    using Object = UnityEngine.Object;

    internal class FixGestureManagerAdvancedContactsHookTest : AdvancedContactParameterBridgeTestBase {
        [Test]
        public void GestureManagerAdvancedContactsUseThePlayableParameterBridge() {
            VerifyAdvancedContactParameterBridge((receiver, playableAccess) => {
                receiver.paramAccess = new GestureParameterAccess(playableAccess, receiver.parameter);
                FixGestureManagerAdvancedContactsHook.Postfix(playableAccess, receiver);
            });
        }

        private sealed class GestureParameterAccess : IAnimParameterAccess {
            private readonly IAnimParameterAccess inner;
            public GestureParameterAccess(object module, string name) {
                inner = ((Func<string, IAnimParameterAccess>)module)(name);
            }
            public float floatVal { get => inner.floatVal; set => inner.floatVal = value; }
            public int intVal { get => inner.intVal; set => inner.intVal = value; }
            public bool boolVal { get => inner.boolVal; set => inner.boolVal = value; }
        }
    }
}
#endif
