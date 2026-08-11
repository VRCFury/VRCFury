using System.Collections.Generic;
using com.vrcfury.udon;
using UdonSharp;
using UdonSharpEditor;
using UnityEngine;
using VF.Builder.Haptics;
using VF.Inspector;
using VF.Utils;
using VRC.SDK3.Dynamics.Contact.Components;

namespace VF.Features {
    internal static class SpsUdonSupportBuilder {
        internal static void Apply(
            SpsSocketUdonSupport udonSupport,
            VRCFuryHapticSocketEditor.BakeResult bakeResult
        ) {
            var udonObject = GameObjects.Create("udon", bakeResult.bakeRoot);
            ConstraintUtils.MakeWorldSpace(udonObject);

            udonSupport.tip = AddReceiver(
                udonObject,
                udonSupport,
                "tip",
                HapticUtils.CONTACT_PEN_MAIN,
                nameof(SpsSocketUdonSupport.contactEnterInfo),
                nameof(SpsSocketUdonSupport.contactExitInfo),
                nameof(SpsSocketUdonSupport._OnTipEnter),
                nameof(SpsSocketUdonSupport._OnTipExit)
            );
            udonSupport.width = AddReceiver(
                udonObject,
                udonSupport,
                "width",
                HapticUtils.CONTACT_PEN_WIDTH,
                nameof(SpsSocketUdonSupport.contactEnterInfo),
                nameof(SpsSocketUdonSupport.contactExitInfo),
                nameof(SpsSocketUdonSupport._OnWidthEnter),
                nameof(SpsSocketUdonSupport._OnWidthExit)
            );
            udonSupport.root = AddReceiver(
                udonObject,
                udonSupport,
                "root",
                HapticUtils.CONTACT_PEN_ROOT,
                nameof(SpsSocketUdonSupport.contactEnterInfo),
                nameof(SpsSocketUdonSupport.contactExitInfo),
                nameof(SpsSocketUdonSupport._OnRootEnter),
                nameof(SpsSocketUdonSupport._OnRootExit)
            );
        }

        internal static void Apply(
            SpsPlugUdonSupport udonSupport,
            VRCFuryHapticPlugEditor.BakeResult bakeResult
        ) {
            var udonObject = GameObjects.Create("udon", bakeResult.bakeRoot);
            ConstraintUtils.MakeWorldSpace(udonObject);
            udonSupport.plugLengthLocal = bakeResult.worldLength / bakeResult.bakeRoot.worldScale.x;
            udonSupport.plugWidthLocal = bakeResult.worldRadius / bakeResult.bakeRoot.worldScale.x;
            udonSupport.socketRoots = AddReceiver(
                udonObject,
                udonSupport,
                "socketRoot",
                HapticUtils.TagSpsSocketRoot,
                nameof(SpsPlugUdonSupport.contactEnterInfo),
                nameof(SpsPlugUdonSupport.contactExitInfo),
                nameof(SpsPlugUdonSupport._OnEnter),
                nameof(SpsPlugUdonSupport._OnExit)
            );
        }

        private static VRCContactReceiver AddReceiver(
            VFGameObject parent,
            UdonSharpBehaviour target,
            string name,
            string tag,
            string enterField,
            string exitField,
            string onEnter,
            string onExit
        ) {
            var contactObject = GameObjects.Create(name, parent);
            var receiver = contactObject.AddComponent<VRCContactReceiver>();
            receiver.radius = 3;
            receiver.collisionTags = new List<string> { tag };

            var forwarder = ((GameObject)contactObject).AddUdonSharpComponent<SpsContactForwarder>();
            forwarder.target = target;
            forwarder.enterField = enterField;
            forwarder.exitField = exitField;
            forwarder.onEnter = onEnter;
            forwarder.onExit = onExit;
            return receiver;
        }
    }
}
