using System.Linq;
using com.vrcfury.udon;
using UdonSharp;
using UdonSharpEditor;
using VF.Builder.Haptics;
using VF.Component;
using VF.Injector;
using VF.Inspector;
using VF.Utils;
using VRC.SDK3.Dynamics.Contact.Components;

namespace VF.Features {
    [VFService]
    internal class SpsUdonSupportBuilder {
        [VFAutowired] private readonly SpsSocketReceiverService socketReceivers;
        [VFAutowired] private readonly SpsChannelsService channels;

        internal void Apply(
            SpsSocketUdonSupport udonSupport,
            VRCFuryHapticSocket socket,
            VRCFuryHapticSocketEditor.BakeResult bakeResult
        ) {
            var main = bakeResult.plugReceivers;
            var plugs = main.Find(false, false);
            var width = main.request.supportsLegacy
                ? socketReceivers.GetLegacyWidthReceiver(main).Find(false, false) : null;
            channels.Apply(bakeResult.channelContacts);
            plugs.component.allowSelf = true;
            udonSupport.plugRoot = plugs.component;
            udonSupport.width = width?.component;
            udonSupport.nativeTags = udonSupport.plugRoot.collisionTags
                .Where(tag => tag.StartsWith(SpsTags.FormatPlug(""))).Distinct().ToArray();
            AddForwarder(udonSupport.plugRoot,
                udonSupport,
                nameof(SpsSocketUdonSupport.contactEnterInfo),
                nameof(SpsSocketUdonSupport.contactExitInfo),
                nameof(SpsSocketUdonSupport._OnEnter),
                nameof(SpsSocketUdonSupport._OnExit)
            );
            if (udonSupport.width != null) {
                udonSupport.width.allowSelf = true;
                AddForwarder(udonSupport.width, udonSupport,
                    nameof(SpsSocketUdonSupport.contactEnterInfo),
                    nameof(SpsSocketUdonSupport.contactExitInfo),
                    nameof(SpsSocketUdonSupport._OnWidthEnter),
                    nameof(SpsSocketUdonSupport._OnWidthExit));
            }
        }

        internal void Apply(
            SpsPlugUdonSupport udonSupport,
            VRCFuryHapticPlugEditor.BakeResult bakeResult
        ) {
            udonSupport.plugRoot = bakeResult.bakeRoot;
            udonSupport.plugLengthLocal = bakeResult.localLength;
            udonSupport.plugWidthLocal = bakeResult.localRadius;
            var receiver = bakeResult.socketReceivers.Find(false, false);
            channels.Apply(bakeResult.channelContacts);
            receiver.component.allowSelf = true;
            udonSupport.socketRoot = receiver.component;
            AddForwarder(receiver.component,
                udonSupport,
                nameof(SpsPlugUdonSupport.contactEnterInfo),
                nameof(SpsPlugUdonSupport.contactExitInfo),
                nameof(SpsPlugUdonSupport._OnEnter),
                nameof(SpsPlugUdonSupport._OnExit)
            );
        }

        private static void AddForwarder(
            VRCContactReceiver receiver,
            UdonSharpBehaviour target,
            string enterField,
            string exitField,
            string onEnter,
            string onExit
        ) {
            var forwarder = receiver.gameObject.AddUdonSharpComponent<SpsContactForwarder>();
            forwarder.target = target;
            forwarder.enterField = enterField;
            forwarder.exitField = exitField;
            forwarder.onEnter = onEnter;
            forwarder.onExit = onExit;
        }
    }
}
