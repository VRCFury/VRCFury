namespace VF.Utils {
    internal class FloatOrBool {
        private readonly float value;

        private FloatOrBool(float value) {
            this.value = value;
        }

        public static implicit operator FloatOrBool(float value) => new FloatOrBool(value);
        public static implicit operator FloatOrBool(bool value) => new FloatOrBool(value ? 1 : 0);
        public float GetFloat() => value;
    }
}
