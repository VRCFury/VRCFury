using System.Linq;
using NUnit.Framework;
using UnityEditor.Animations;
using UnityEngine;
using VF.Utils;
using VF.Utils.Controller;
using Object = UnityEngine.Object;

namespace VF.Tests {
    internal abstract class ControllerParameterTestBase {
        protected static VFAParam FindDefinition(VFController controller, string name) =>
            controller.parameters.FirstOrDefault(p => p.name == name);

        protected static VFAParam RequestParameter(VFController controller, string name,
            AnimatorControllerParameterType type, FloatOrBool def = null) {
            switch (type) {
                case AnimatorControllerParameterType.Float: return controller.GetOrCreateFloat(name, def);
                case AnimatorControllerParameterType.Int: return controller.GetOrCreateInt(name, def);
                case AnimatorControllerParameterType.Bool: return controller.GetOrCreateBool(name, def);
                default: return new VFAParam(name, type).CopyTo(controller);
            }
        }

        protected static VFAParam ImportParameter(VFController controller, string name,
            System.Action<AnimatorControllerParameter> configure) {
            var parameter = new AnimatorControllerParameter { name = name };
            configure(parameter);
            var model = VFAParam.Load(parameter);
            return controller.AddOrMergeParam(model);
        }
    }
}
