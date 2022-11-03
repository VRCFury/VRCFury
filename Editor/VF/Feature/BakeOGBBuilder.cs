using System.Collections.Generic;
using VF.Feature.Base;
using VF.Inspector;
using VF.Model;
using VF.Model.Feature;
using VF.Model.StateAction;

namespace VF.Feature {
    public class BakeOGBBuilder : FeatureBuilder {
        [FeatureBuilderAction((int)FeatureOrder.BakeOgbComponents)]
        public void Apply() {
            var usedNames = new List<string>();
            foreach (var c in avatarObject.GetComponentsInChildren<OGBPenetrator>(true)) {
                OGBPenetratorEditor.Bake(c, usedNames);
            }
            foreach (var c in avatarObject.GetComponentsInChildren<OGBOrifice>(true)) {
                if (c.addMenuItem) {
                    c.gameObject.SetActive(false);
                    var name = c.name;
                    if (string.IsNullOrWhiteSpace(name)) {
                        name = c.gameObject.name;
                    }
                    addOtherFeature(new Toggle() {
                        name = "Holes/" + name,
                        state = new State() {
                            actions = {
                                new ObjectToggleAction() {
                                    obj = c.gameObject
                                }
                            }
                        },
                        enableExclusiveTag = true,
                        exclusiveTag = "OGBOrificeToggles"
                    });
                }
                
                OGBOrificeEditor.Bake(c, usedNames);
            }
        }
    }
}
