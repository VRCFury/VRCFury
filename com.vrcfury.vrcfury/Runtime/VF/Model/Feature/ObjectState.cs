using System;
using System.Collections.Generic;
using UnityEngine;
using VF.Model.StateAction;

namespace VF.Model.Feature {
    [Serializable]
    internal class ObjectState : NewFeatureModel {
        public List<ObjState> states = new List<ObjState>();

        [Serializable]
        public class ObjState {
            public GameObject obj;
            public Action action = Action.DEACTIVATE;
        }
        public enum Action {
            DEACTIVATE,
            ACTIVATE,
            DELETE
        }

        public override IList<FeatureModel> Migrate(MigrateRequest request) {
            var apply = new ApplyDuringUpload();
            apply.action = new State();
            foreach (var s in states) {
                if (s.obj == null) continue;
                if (s.action == Action.DELETE) {
                    if (!request.fakeUpgrade) {
                        var vrcf = s.obj.AddComponent<VRCFury>();
                        vrcf.content = new DeleteDuringUpload();
                        VRCFury.MarkDirty(vrcf);
                    }
                } else if (s.action == ObjectState.Action.ACTIVATE) {
                    apply.action.actions.Add(new ObjectToggleAction() {
                        mode = ObjectToggleAction.Mode.TurnOn,
                        obj = s.obj
                    });
                } else if (s.action == ObjectState.Action.DEACTIVATE) {
                    apply.action.actions.Add(new ObjectToggleAction() {
                        mode = ObjectToggleAction.Mode.TurnOff,
                        obj = s.obj
                    });
                }
            }

            var output = new List<FeatureModel>();
            if (apply.action.actions.Count > 0) {
                output.Add(apply);
            }
            return output;
        }
    }
}