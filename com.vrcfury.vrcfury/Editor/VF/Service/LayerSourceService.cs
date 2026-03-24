using System.Collections.Generic;
using JetBrains.Annotations;
using UnityEditor.Animations;
using UnityEngine;
using VF.Injector;
using VF.Utils.Controller;

namespace VF.Service {
    [VFService]
    internal class LayerSourceService {
        [VFAutowired] private readonly GlobalsService globals;
        
        public const string AvatarDescriptorSource = "VRC Avatar Descriptor";
        public const string VrcDefaultSource = "VRC Default";

        private readonly Dictionary<VFLayer, string> sources = new Dictionary<VFLayer, string>();
        private readonly HashSet<VFLayer> created = new HashSet<VFLayer>();

        public void SetSource(VFLayer sm, string source) {
            sources[sm] = source;
        }
        
        public void SetSourceToCurrent(VFLayer sm) {
            SetSource(sm, globals.currentFeatureName);
        }

        public void CopySource(VFLayer from, VFLayer to) {
            if (sources.TryGetValue(from, out var source)) {
                sources[to] = source;
            }
            if (created.Contains(from)) {
                created.Add(to);
            }
        }

        public void MarkCreated(VFLayer layer) {
            created.Add(layer);
        }

        public bool DidCreate(VFLayer layer) {
            return created.Contains(layer);
        }

        [CanBeNull]
        public string GetSource(VFLayer sm) {
            return sources.TryGetValue(sm, out var source) ? source : null;
        }
    }
}
