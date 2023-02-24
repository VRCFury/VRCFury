using System;
using System.Collections.Generic;
using UnityEngine;

namespace VF.Model {
    public class VRCFGlobalCollider : VRCFuryComponent {
        public float radius = 0.1f;
        public Transform rootTransform;

        public Transform GetTransform() {
            return rootTransform != null ? rootTransform : transform;
        }
    }
}
