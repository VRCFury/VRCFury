namespace VF.Tests {
    using System;
    using System.Collections.Generic;
    using System.Linq;
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
    using VF.Model.StateAction;
    using VF.Service;
    using VF.Utils;
    using VF.Utils.Controller;
    using VRC.SDK3.Avatars.Components;
    using VRC.SDK3.Dynamics.Contact.Components;
    using Object = UnityEngine.Object;
    using Flags = VF.Component.VRCFuryHapticSocket.AddLight;
    using ScaleFlags = VF.Builder.Haptics.SpsTags.SocketScaleFlagsX;
    using Party = VF.Builder.Haptics.SelfAndOthersReceiverService.Party;

    internal class ChangeSpsTagActionBuilderTest : SpsSenderBakeTestBase {
        [TestCase(false)]
        [TestCase(true)]
        public void ChangeSpsTagActionNoLongerGeneratesAnimation(bool debugMode) {
            var builder = new VF.Actions.ChangeSpsTagActionBuilder();
            var clip = builder.Build(new VF.Model.StateAction.ChangeSpsTagAction { target = root.transform }, debugMode);
            Assert.That(clip.GetFloatBindings(), Is.Empty);
        }
    }
}
