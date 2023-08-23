using UnityEngine;

namespace VF.Component
{
    [DefaultExecutionOrder(10000)]
    public class VRCFuryInitializedTester : MonoBehaviour {
        public static bool initialized = false;
        private void Awake() {
            initialized = true;
        }
    } 
}