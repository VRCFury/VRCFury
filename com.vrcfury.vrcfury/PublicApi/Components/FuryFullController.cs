using JetBrains.Annotations;
using UnityEngine;
using VF.Model;
using VF.Model.Feature;
using VRC.SDK3.Avatars.Components;
using VRC.SDK3.Avatars.ScriptableObjects;

namespace com.vrcfury.api.Components {
    /** Create an instance using <see cref="FuryComponents"/> */
    [PublicAPI]
    public class FuryFullController {
        private readonly FullController c;

        internal FuryFullController(GameObject obj) {
            var vf = obj.AddComponent<VRCFury>();
            c = new FullController();
            vf.content = c;
        }

        public void AddMenu(VRCExpressionsMenu menu, string prefix = "") {
            c.menus.Add(new FullController.MenuEntry() {
                menu = menu,
                prefix = prefix
            });
        }

        public void AddController(
            RuntimeAnimatorController controller,
            VRCAvatarDescriptor.AnimLayerType type = VRCAvatarDescriptor.AnimLayerType.FX
        ) {
            c.controllers.Add(new FullController.ControllerEntry() {
                controller = controller,
                type = type
            });
        }

        public void AddParams(VRCExpressionParameters prms) {
            c.prms.Add(new FullController.ParamsEntry() {
                parameters = prms
            });
        }

        public void AddGlobalParam(string name) {
            c.globalParams.Add(name);
        }
        
        public void AddPathRewrite(string from, string to) {
            c.rewriteBindings.Add(new FullController.BindingRewrite {
                from = from,
                to = to
            });
        }
    }
}
