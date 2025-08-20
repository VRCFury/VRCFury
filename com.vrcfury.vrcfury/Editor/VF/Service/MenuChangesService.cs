using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;
using VF.Feature;
using VF.Feature.Base;
using VF.Injector;
using VF.Model.Feature;
using VF.Utils;

namespace VF.Service {
    [VFService]
    internal class MenuChangesService {
        [VFAutowired] private readonly GlobalsService globals;
        [VFAutowired] private readonly MenuService menuService;
        private MenuManager menu => menuService.GetMenu();
        private readonly List<NewFeatureModel> extraPreActions = new List<NewFeatureModel>();
        private readonly List<SetIconBuilder> iconChanges = new List<SetIconBuilder>();

        public void AddExtraAction(NewFeatureModel model) {
            extraPreActions.Add(model);
        }

        [FeatureBuilderAction(FeatureOrder.MoveMenuItems)]
        public void Apply() {
            var allActions = extraPreActions.Concat(globals.allFeaturesInRun).ToArray();
            
            var iconChanges = globals.allBuildersInRun.OfType<SetIconBuilder>().ToList();

            void BetweenSteps() {
                foreach (var iconChange in iconChanges.ToArray()) {
                    var icon = iconChange.model.icon.Get();
                    var path = MenuManager.PrependFolders(iconChange.model.path, iconChange.featureBaseObject);
                    var result = menu.SetIcon(path, icon);
                    if (result) {
                        var filePath = icon != null ? AssetDatabase.GetAssetPath(icon) : "";
                        Debug.Log($"Changed icon of {path} to {filePath}");
                        iconChanges.Remove(iconChange);
                    }
                }
            }

            BetweenSteps();
            foreach (var model in allActions.OfType<MoveMenuItem>()) {
                Debug.Log($"Moving {model.fromPath} to {model.toPath}");
                var result = menu.Move(model.fromPath, model.toPath);
                if (result) {
                    BetweenSteps();
                }
            }
        }
    }
}
