using System;
using System.Collections.Generic;
using System.Linq;
using VF.Component;
using VF.Injector;
using VF.Utils;

namespace VF.Builder.Haptics {
    [VFService]
    internal class SpsSocketReceiverService {
        [VFAutowired] private readonly SelfAndOthersReceiverService receivers;
        [VFAutowired] private readonly SpsConfigurer configurer;
        private readonly Dictionary<VRCFuryHapticSocket, SelfAndOthersReceiverService.Receivers> cache = new();
        private readonly Dictionary<SelfAndOthersReceiverService.Receivers, SelfAndOthersReceiverService.Receivers> widths = new();

        public SelfAndOthersReceiverService.Receivers GetReceivers(
            VRCFuryHapticSocket socket, VFGameObject frame, string name, bool includeLegacy = true
        ) => cache.GetOrCreate(socket, () => {
            string[] Tags(bool self) {
                var tags = configurer.GetSocketChannels(socket, self
                        ? SpsConfigurer.ChannelMode.ReceiverSelf : SpsConfigurer.ChannelMode.ReceiverOthers)
                    .OrderBy(c => c, StringComparer.Ordinal).Select(SpsTags.FormatPlug).ToList();
                if (!self && socket.useSharedTag && includeLegacy) tags.Add(SpsTags.LegacyPlugSphere);
                return tags.ToArray();
            }
            return receivers.Create(new SelfAndOthersReceiverService.Request {
                frame = frame, name = name + "/Depth", worldRadius = includeLegacy ? 1 : 0.2f,
                objectName = (self, merged) => "Receiver - Plugs (" + (self ? "Self" : "Others") + (merged ? " Merged" : "") + ")",
                selfTags = Tags(true), othersTags = Tags(false)
            });
        });

        public SelfAndOthersReceiverService.Receivers GetLegacyWidthReceiver(SelfAndOthersReceiverService.Receivers main) =>
            widths.GetOrCreate(main, () => receivers.Create(new SelfAndOthersReceiverService.Request {
                frame = main.request.frame, name = main.request.name + "/LegacyWidth",
                objectName = (self, merged) => "Receiver - Legacy Plug Width (Others)",
                worldRadius = main.request.worldRadius, othersTags = new[] { SpsTags.LegacyPlugWidth }
            }));
    }
}
