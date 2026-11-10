using System;
using System.Collections.Generic;
using System.Linq;
using VF.Model;
using VF.Utils;
using ChannelSet = VF.Builder.Haptics.SpsChannelsService.ChannelSet;

namespace VF.Builder.Haptics {
    internal class SpsChannelContacts {
        public readonly UnityEngine.Component component;
        private readonly Func<SpsChannelSettings, ChannelSet> createSet;
        internal readonly List<ChannelSet> sets = new();
        internal readonly List<Action<IReadOnlyList<ChannelSet>>> allCallbacks = new();
        internal bool finished;
        public ChannelSet Default => sets[0];

        internal SpsChannelContacts(UnityEngine.Component component, Func<SpsChannelSettings, ChannelSet> createSet) {
            this.component = component;
            this.createSet = createSet;
            sets.Add(createSet(null));
        }
        public ChannelSet Request(SpsChannelSettings settings) {
            CheckOpen();
            var candidate = createSet(settings);
            var existing = sets.FirstOrDefault(s => s.SameAs(candidate));
            if (existing != null) return existing;
            sets.Add(candidate);
            return candidate;
        }

        public void OnAllSetsAvailable(Action<IReadOnlyList<ChannelSet>> callback) { CheckOpen(); allCallbacks.Add(callback); }
        internal void CheckOpen() {
            if (finished) throw new Exception("SPS channels have already been finalized.");
        }

    }
}
