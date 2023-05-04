using System;
using System.Collections.Generic;
using UnityEngine;

namespace VF.Component {
    public class VRCFuryGlobalCollider : VRCFuryComponent {
        public float radius = 0.1f;
        public float height = 0;
        public Transform rootTransform;

        public Transform GetTransform() {
            return rootTransform != null ? rootTransform : transform;
        }
    }
}
