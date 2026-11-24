using System;
using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using UnityEditor;
using UnityEditor.UIElements;
using UnityEngine;
using UnityEngine.UIElements;
using VF.Builder;
using VF.Feature;
using VF.Inspector;
using VF.Model;
using VF.Model.Feature;
using VF.Service;
using VF.Utils;
using Object = UnityEngine.Object;

namespace VF.Tests {
    internal abstract class ObjectReferenceTestBase {
        protected class Fields : ScriptableObject {
            public ObjectReference target = new ObjectReference();
            public List<ObjectReference> targets = new List<ObjectReference>();
            public ObjectReference<SkinnedMeshRenderer> typed = new ObjectReference<SkinnedMeshRenderer>();
        }
    }
}
