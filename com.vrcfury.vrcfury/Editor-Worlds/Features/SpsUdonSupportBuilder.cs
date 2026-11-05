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

        internal void Apply(
            SpsSocketUdonSupport udonSupport,
            VRCFuryHapticSocket socket,
            VRCFuryHapticSocketEditor.BakeResult bakeResult
        ) {
            var main = socketReceivers.GetReceivers(socket, bakeResult.worldSpace, socket.name);
            udonSupport.plugs = new[] { main.Find(true, false), main.Find(false, false) };
            udonSupport.nativeTags = udonSupport.plugs.SelectMany(r => r.collisionTags)
                .Where(tag => tag.StartsWith(SpsTags.FormatPlug(""))).Distinct().ToArray();
            foreach (var receiver in udonSupport.plugs) AddForwarder(receiver,
                udonSupport,
                nameof(SpsSocketUdonSupport.contactEnterInfo),
                nameof(SpsSocketUdonSupport.contactExitInfo),
                nameof(SpsSocketUdonSupport._OnEnter),
                nameof(SpsSocketUdonSupport._OnExit)
            );
            udonSupport.width = null;
            if (socket.useSharedTag) {
                udonSupport.width = socketReceivers.GetLegacyWidthReceiver(main).Find(false, false);
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
            udonSupport.socketRoots = new[] {
                bakeResult.socketReceivers.Find(true, false),
                bakeResult.socketReceivers.Find(false, false)
            };
            foreach (var receiver in udonSupport.socketRoots) AddForwarder(receiver,
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
