using UnityEditor.Animations;
using UnityEngine;

namespace VF.Utils.Controller {
    // The controller's parameter model. Unity objects are only used when loading and saving.
    internal class VFAParam {
        public string name { get; }
        // A double preserves every Unity float and int, including ints beyond float's exact range.
        private double defaultValue;
        internal VFController Controller { get; set; }
        public AnimatorControllerParameterType type { get; private set; }

        internal VFAParam(string name, AnimatorControllerParameterType type) {
            this.name = name;
            this.type = type;
        }

        internal static VFAParam Load(AnimatorControllerParameter parameter) {
            var output = new VFAParam(parameter.name, parameter.type);
            switch (parameter.type) {
                case AnimatorControllerParameterType.Bool: output.defaultValue = parameter.defaultBool ? 1 : 0; break;
                case AnimatorControllerParameterType.Int: output.defaultValue = parameter.defaultInt; break;
                case AnimatorControllerParameterType.Float: output.defaultValue = parameter.defaultFloat; break;
            }
            return output;
        }

        internal AnimatorControllerParameter Save() => new AnimatorControllerParameter {
            name = name,
            type = type,
            defaultBool = defaultValue != 0,
            defaultInt = (int)System.Math.Round(defaultValue),
            defaultFloat = (float)defaultValue
        };

        internal VFAParam Clone(VFController controller) {
            var copy = (VFAParam)MemberwiseClone();
            copy.Controller = controller;
            return copy;
        }

        public VFAParam WithName(string name) => new VFAParam(name, type) {
            defaultValue = defaultValue,
            Controller = Controller
        };
        public float GetDefault() => (float)defaultValue;
        public static implicit operator string(VFAParam value) => value.name;
        public override string ToString() => name;

        public VFAParam CopyTo(VFController controller) {
            return controller.AddOrMergeParam(Clone(controller));
        }

        internal void UpgradeType(AnimatorControllerParameterType type) {
            type = VFController.CombineParameterTypes(this.type, type);
            if (this.type == type) return;
            this.type = type;
            if (type == AnimatorControllerParameterType.Float) defaultValue = (float)defaultValue;
        }

        public void SetDefault(FloatOrBool value) {
            var floatValue = value?.GetFloat() ?? 0;
            defaultValue = floatValue;
            var intValue = (int)System.Math.Round(floatValue);
            if (type == AnimatorControllerParameterType.Bool && (floatValue == 0 || floatValue == 1)
                || type == AnimatorControllerParameterType.Trigger && floatValue == 0) return;
            UpgradeType(intValue == floatValue ? AnimatorControllerParameterType.Int : AnimatorControllerParameterType.Float);
        }

        public VFCondition IsTrue() => Is(true);
        public VFCondition IsFalse() => Is(false);
        public VFCondition Is(bool value) => Condition(value ? AnimatorConditionMode.If : AnimatorConditionMode.IfNot, 0);
        public VFCondition IsEqualTo(FloatOrBool value) => Condition(AnimatorConditionMode.Equals, value.GetFloat());
        public VFCondition IsNotEqualTo(FloatOrBool value) => Condition(AnimatorConditionMode.NotEqual, value.GetFloat());
        public VFCondition IsGreaterThan(float value) => Condition(AnimatorConditionMode.Greater, value);
        public VFCondition IsLessThan(float value) => Condition(AnimatorConditionMode.Less, value);
        public VFCondition IsGreaterThanOrEquals(float value) => IsLessThan(value).Not();
        public VFCondition IsLessThanOrEquals(float value) => IsGreaterThan(value).Not();

        private VFCondition Condition(AnimatorConditionMode mode, float value) => new VFCondition(new AnimatorCondition {
            mode = mode, parameter = name, threshold = value
        });

        public VFClip MakeSetter(FloatOrBool value) {
            var floatValue = value.GetFloat();
            var clip = VFClip.Create($"AAP: {name} = {floatValue}");
            clip.SetAap(name, floatValue);
            return clip;
        }

        public VFMotion MakeCopier(string from, float minSupported = 0, float maxSupported = float.MaxValue, float multiplier = 1) {
            var motionName = $"AAP: {name} = {from}";
            if (multiplier != 1) motionName += $" * {multiplier}";
            if (minSupported >= 0) {
                var tree = VFBlendTreeDirect.Create(motionName);
                tree.Add(from, MakeSetter(multiplier));
                return tree;
            }
            return VFBlendTree1D.CreateWithData(motionName, from,
                (minSupported, MakeSetter(minSupported * multiplier)),
                (maxSupported, MakeSetter(maxSupported * multiplier)));
        }
    }
}
