using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;
using VF.Builder;
using VF.Feature.Base;
using VF.Injector;
using VF.Model.Feature;
using VF.Service;

namespace VF.Feature {
    [VFService]
    internal class MenuChangesService {
        [VFAutowired] private readonly GlobalsService globals;
        [VFAutowired] private readonly AvatarManager manager;
        private readonly List<NewFeatureModel> extraPreActions = new List<NewFeatureModel>();

        public void AddExtraAction(NewFeatureModel model) {
            extraPreActions.Add(model);
        }

        [FeatureBuilderAction(FeatureOrder.MoveMenuItems)]
        public void Apply() {
            var allActions = extraPreActions.Concat(globals.allFeaturesInRun).ToArray();
            
            var iconChanges = allActions.OfType<SetIcon>().ToList();

            void BetweenSteps() {
                foreach (var iconChange in iconChanges.ToArray()) {
                    var icon = iconChange.icon.Get();
                    var result = manager.GetMenu().SetIcon(iconChange.path, icon);
                    if (result) {
                        var filePath = icon != null ? AssetDatabase.GetAssetPath(icon) : "";
                        Debug.Log($"Changed icon of {iconChange.path} to {filePath}");
                        iconChanges.Remove(iconChange);
                    }
                }
            }

            BetweenSteps();
            foreach (var model in allActions.OfType<MoveMenuItem>()) {
                Debug.Log($"Moving {model.fromPath} to {model.toPath}");
                var result = manager.GetMenu().Move(model.fromPath, model.toPath, model.index);
                if (result) {
                    BetweenSteps();
                }
            }
        }
    }
}
