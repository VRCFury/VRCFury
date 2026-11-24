using System;
using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.UIElements;
using VF.Builder;
using VF.Builder.Haptics;
using VF.Component;
using VF.Inspector;
using VF.Feature.Base;
using VF.Injector;
using VF.Model.Feature;
using VF.Service;
using VF.Utils;
using VRC.SDK3.Avatars.Components;
using VRC.SDK3.Dynamics.Contact.Components;
using Object = UnityEngine.Object;

namespace VF.Tests {
    internal abstract class SpsOptionsTestBase {
        protected class ChannelPreviewBones : SpsAutoChannelGenerator {
            public HumanBodyBones? GetClosestBone(VFGameObject obj) => HumanBodyBones.Head;
            public VFGameObject GetBone(VFGameObject obj, HumanBodyBones bone) => null;
            public Vector3? GetAvatarViewPosition(VFGameObject obj) => null;
        }
    }
}
