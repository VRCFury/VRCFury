using System;
using System.Collections.Generic;
using VF.Builder.Haptics;
using VF.Component;
using VF.Feature.Base;
using VF.Injector;
using VF.Model.StateAction;
using VF.Utils;
using VF.Utils.Controller;

namespace VF.Service {
    [VFService]
    internal class ChangeSpsChannelsService {
        [VFAutowired] private readonly VFGameObject avatarObject;
        [VFAutowired] private readonly SpsChannelsService channels;
        [VFAutowired] private readonly ControllersService controllers;
        [VFAutowired] private readonly AvatarBindingStateService bindingState;
        private readonly Dictionary<SpsChannelContacts, List<(SpsChannelsService.ChannelSet set, VFAParam weight)>> requests = new();

        public VFClip Request(ChangeSpsChannelsAction action) {
            var obj = action.target.asVf();
            if (obj == null || !obj.IsSameOrChildOf(avatarObject))
                throw new Exception("Change SPS Channels requires a socket or plug on this avatar.");
            var plug = obj.GetComponent<VRCFuryHapticPlug>();
            var socket = obj.GetComponent<VRCFuryHapticSocket>();
            if ((plug != null ? 1 : 0) + (socket != null ? 1 : 0) != 1)
                throw new Exception("Change SPS Channels requires exactly one SPS socket or plug.");
            var data = channels.Get(plug != null ? (UnityEngine.Component)plug : socket);
            var set = data.Request(action.channelSettings);
            var weight = controllers.GetFx().CreateUniqueFloat("SPS Channels/" + obj.name);
            requests.GetOrCreate(data, () => new()).Add((set, weight));
            return weight.MakeSetter(1);
        }

        [FeatureBuilderAction(FeatureOrder.ChangeSpsChannels)]
        public void Apply() {
            foreach (var (data, tree) in channels.Baked) {
                channels.Apply(data, sets => {
                    if (sets.Count > 1) {
                        if (tree == null) throw new Exception("SPS channel changes require a registered animator tree.");
                        VFMotion selected = data.Default.clip;
                        // Later requests win ties. Contact activation and shader metadata switch together.
                        foreach (var (set, weight) in requests.GetOrDefault(data) ?? new())
                            selected = BlendtreeMath.GreaterThan(weight, 0.5f, true).create(set.clip, selected);
                        tree.Value.Add(selected);
                    }
                    bindingState.ApplyClip(data.Default.clip, "Initial SPS channels");
                });
            }
        }
    }
}
