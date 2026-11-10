using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using VF.Component;
using VF.Injector;
using VF.Model;
using VF.Utils;
using VF.Utils.Controller;
using Party = VF.Builder.Haptics.SelfAndOthersReceiverService.Party;

namespace VF.Builder.Haptics {
    [VFService]
    internal class SpsChannelsService {
        [VFAutowired] private readonly SpsConfigurer configurer;
        private readonly Dictionary<UnityEngine.Component, SpsChannelContacts> components = new();
        private readonly Dictionary<SpsChannelContacts, Lazy<VFBlendTreeDirect>> trees = new();
        public IEnumerable<(SpsChannelContacts data, Lazy<VFBlendTreeDirect> tree)> Baked =>
            components.Values.Select(data => (data, trees.GetOrDefault(data)));

        public SpsChannelContacts Register(UnityEngine.Component component, Lazy<VFBlendTreeDirect> tree) {
            var data = Get(component);
            trees.Add(data, tree);
            return data;
        }

        public sealed class ChannelSet {
            public readonly bool isPublic;
            public readonly string[] sender;
            public readonly string[] self;
            public readonly string[] others;
            public IEnumerable<string> paths => self.Union(others).OrderBy(c => c, StringComparer.Ordinal);
            public readonly VFClip clip = VFClip.Create("SPS Channels");
            internal readonly HashSet<VFGameObject> objects = new();
            internal bool finalized;

            public void Register(VFGameObject obj) {
                if (finalized) throw new InvalidOperationException("SPS channels have already been finalized.");
                if (obj == null) throw new ArgumentNullException(nameof(obj));
                objects.Add(obj);
            }

            internal ChannelSet(bool isPublic, IEnumerable<string> sender, IEnumerable<string> self, IEnumerable<string> others) {
                this.isPublic = isPublic;
                this.sender = sender.OrderBy(c => c, StringComparer.Ordinal).ToArray();
                this.self = self.OrderBy(c => c, StringComparer.Ordinal).ToArray();
                this.others = others.OrderBy(c => c, StringComparer.Ordinal).ToArray();
            }

            internal bool SameAs(ChannelSet other) => isPublic == other.isPublic
                && sender.SequenceEqual(other.sender) && self.SequenceEqual(other.self) && others.SequenceEqual(other.others);
        }

        public SpsChannelContacts Get(UnityEngine.Component component) => components.GetOrCreate(component,
            () => new SpsChannelContacts(component, settings => {
                settings ??= component is VRCFuryHapticPlug p ? p.channelSettings : ((VRCFuryHapticSocket)component).channelSettings;
                IEnumerable<string> Channels(Party party) =>
                    configurer.GetChannels(component.owner(), component is VRCFuryHapticPlug, party, settings);
                var set = new ChannelSet(settings.useSharedTag, Channels(Party.Merged),
                    Channels(Party.Self), Channels(Party.Others));
                if (set.sender.Length == 0) throw new Exception(
                    "SPS requires at least one channel that allows Self or Others. Enable the public channel or add a custom channel."
                    + "\nComponent: " + component.owner().GetDebugPath());
                return set;
            }));

        // Worlds and detached baking use the component's default set immediately.
        // Avatar builds supply a selector after all normal action builders have registered requests.
        public void Apply(SpsChannelContacts data, Action<IReadOnlyList<ChannelSet>> select = null) {
            if (data.finished) return;
            data.finished = true;
            foreach (var callback in data.allCallbacks) callback(data.sets);
            foreach (var set in data.sets) set.finalized = true;
            // A single set needs no activation control; other consumers may own these objects' enabled states.
            if (data.sets.Count > 1) {
                foreach (var obj in data.sets.SelectMany(set => set.objects).Distinct()) {
                    foreach (var set in data.sets) set.clip.SetEnabled(obj, set.objects.Contains(obj));
                }
            }
            if (select != null) select(data.sets);
            else {
                var clip = (AnimationClip)data.Default.clip.Save(data.component.owner().root);
                try { clip.SampleAnimation(data.component.owner().root, 0); }
                finally { UnityEngine.Object.DestroyImmediate(clip); }
            }
        }
    }
}
