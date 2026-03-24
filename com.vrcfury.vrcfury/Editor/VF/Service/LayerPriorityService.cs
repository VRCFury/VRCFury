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
    internal class LayerPriorityService {
        [VFAutowired] private readonly ControllersService controllers;

        private readonly Dictionary<VFLayer, int> layerPriorities = new Dictionary<VFLayer, int>();

        public void SetPriority(VFLayer layer, int priority) {
            if (priority != 0) {
                layerPriorities[layer] = priority;
            }
        }

        public int GetPriority(VFLayer layer) {
            return layerPriorities.TryGetValue(layer, out var priority) ? priority : 0;
        }

        public bool HasPriority(VFLayer layer) {
            return layerPriorities.ContainsKey(layer);
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

            // Create list with layer index and priority
            var layersWithInfo = layers.Select((layer, index) => new {
                Layer = layer,
                OriginalIndex = index,
                Priority = GetPriority(layer)
            }).ToList();
            // Sort by priority (preserving original order for same priority)
            var sortedLayers = layersWithInfo
                .OrderBy(l => l.Priority)
                .ThenBy(l => l.OriginalIndex)
                .Select(l => l.Layer)
                .ToList();

            // If layers are already in desired order return early
            if (layers.SequenceEqual(sortedLayers)) return;

            // Apply new order by moving layers to their target index
            Debug.Log($"[VRCFury] Reordering {controller.GetType()} layers:");
            for (int targetIndex = 0; targetIndex < sortedLayers.Count; targetIndex++) {
                var sortedLayer = sortedLayers[targetIndex];
                var currentIndex = controller.GetLayers().ToList().IndexOf(sortedLayer);

                if (currentIndex != targetIndex) {
                    Debug.Log($"  [{targetIndex}] {sortedLayer.name} (was {currentIndex}, priority {GetPriority(sortedLayer)})");
                    sortedLayer.Move(targetIndex);
                }
            }
        }
    }
}
