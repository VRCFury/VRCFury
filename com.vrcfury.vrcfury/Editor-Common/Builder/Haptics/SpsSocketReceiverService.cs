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
        [VFAutowired] private readonly SpsChannelsService channels;
        private readonly Dictionary<VRCFuryHapticSocket, SelfAndOthersReceiverService.Receivers> cache = new();
        private readonly Dictionary<SelfAndOthersReceiverService.Receivers, SelfAndOthersReceiverService.Receivers> widths = new();

        public SelfAndOthersReceiverService.Receivers GetReceivers(
            VRCFuryHapticSocket socket, VFGameObject frame, string name, bool includeLegacy = true
        ) => cache.GetOrCreate(socket, () => {
            var data = channels.Get(socket);
            return receivers.Create(new SelfAndOthersReceiverService.Request {
                frame = frame, name = name + "/Depth", worldRadius = includeLegacy ? 1 : 0.2f, channels = data,
                objectName = (self, merged) => "Receiver - Plugs (" + (self ? "Self" : "Others") + (merged ? " Merged" : "") + ")",
                supportsLegacy = includeLegacy,
                getTags = (set, self) => (self ? set.self : set.others)
                    .Select(SpsTags.FormatPlug).Concat(!self && set.isPublic && includeLegacy
                        ? new[] { SpsTags.LegacyPlugSphere } : Array.Empty<string>())
            });
        });

        public SelfAndOthersReceiverService.Receivers GetLegacyWidthReceiver(SelfAndOthersReceiverService.Receivers main) =>
            widths.GetOrCreate(main, () => receivers.Create(new SelfAndOthersReceiverService.Request {
                frame = main.request.frame, name = main.request.name + "/LegacyWidth",
                objectName = (self, merged) => "Receiver - Legacy Plug Width (Others)",
                worldRadius = main.request.worldRadius, supportsLegacy = true,
                channels = main.request.channels, omitEmpty = true,
                getTags = (set, self) => set.isPublic ? new[] { SpsTags.LegacyPlugWidth } : Array.Empty<string>()
            }));
    }
}
