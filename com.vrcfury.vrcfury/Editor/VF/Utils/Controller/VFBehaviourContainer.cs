using System.Linq;
using UnityEngine;

namespace VF.Utils.Controller {
    internal interface VFBehaviourContainer {
        StateMachineBehaviour[] behaviours { get; set; }
    }

    internal static class VFBehaviourContainerExtensions {
        public static T AddBehaviour<T>(this VFBehaviourContainer c) where T : StateMachineBehaviour {
            var added = VrcfObjectFactory.Create<T>();
            c.behaviours = c.behaviours.Append(added).ToArray();
            return added;
        }
    }
}
