using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using VF.Builder;
using VF.Feature.Base;
using VF.Injector;
using VF.Utils;
using VF.Utils.Controller;

namespace VF.Service {
    [VFService]
    internal class PriorityService {
        [VFAutowired] private readonly ControllersService controllers;
        [VFAutowired] private readonly FixWriteDefaultsService fixWriteDefaults;
        private const int MaxMenuPriority = 1 << 29;

        private readonly Dictionary<VFLayer, int> layerPriorities = new Dictionary<VFLayer, int>();

        public void SetLayerPriority(VFLayer layer, int priority) {
            if (priority != 0) {
                layerPriorities[layer] = priority;
            }
        }

        public int GetLayerPriority(VFLayer layer) {
            return layerPriorities.TryGetValue(layer, out var priority) ? priority : 0;
        }

        public bool HasLayerPriority(VFLayer layer) {
            return layerPriorities.ContainsKey(layer);
        }

        public int GetMenuSortPosition(int priority) {
            // Add safe margin (split the int range in half) to avoid colliding with the "uniqueModelNum" sort positions used by the menu manager
            var clamped = Mathf.Clamp(priority, -MaxMenuPriority, MaxMenuPriority);
            if (clamped < 0) return (int.MinValue / 2) + clamped;
            return (int.MaxValue / 2) + clamped;
        }

        [FeatureBuilderAction(FeatureOrder.ReorderLayersByPriority)]
        public void Apply() {
            // Skip if no priorities were set
            if (layerPriorities.Count == 0) {
                return;
            }

            Debug.Log($"[VRCFury] LayerPriorityService: Reordering layers based on {layerPriorities.Count} priority entries");

            foreach (var controller in controllers.GetAllUsedControllers()) {
                ReorderLayers(controller);
            }
        }

        private void ReorderLayers(ControllerManager controller) {
            var layers = controller.GetLayers().ToList();
            if (layers.Count <= 1) return;

            var hasAnyPriority = layers.Any(l => layerPriorities.ContainsKey(l));
            if (!hasAnyPriority) return;

            // Create list with layer index and sort by priority (preserving original order for same priority)
            // The base layer and the vrcfury defaults layer always stay at the very top since VRChat and VRCFury rely on them being first
            var pinnedCount = 1;
            var defaultLayer = fixWriteDefaults.GetDefaultLayer();
            while (defaultLayer != null && pinnedCount < layers.Count && layers[pinnedCount] == defaultLayer) {
                pinnedCount++;
            }

            var sortedLayers = layers.Take(pinnedCount)
                .Concat(layers
                    .Select((layer, index) => (layer, index, priority: GetLayerPriority(layer)))
                    .Skip(pinnedCount)
                    .OrderBy(l => l.priority)
                    .ThenBy(l => l.index)
                    .Select(l => l.layer))
                .ToList();

            // If layers are already in desired order return early
            if (layers.SequenceEqual(sortedLayers)) return;

            // Apply new order by moving layers to their target index
            Debug.Log($"[VRCFury] Reordering {controller.GetType()} layers:");
            for (int targetIndex = 0; targetIndex < sortedLayers.Count; targetIndex++) {
                var sortedLayer = sortedLayers[targetIndex];
                var currentIndex = controller.GetLayers().ToList().IndexOf(sortedLayer);

                if (currentIndex != targetIndex) {
                    Debug.Log($"  [{targetIndex}] {sortedLayer.name} (was {currentIndex}, priority {GetLayerPriority(sortedLayer)})");
                    sortedLayer.Move(targetIndex);
                }
            }
        }
    }
}
