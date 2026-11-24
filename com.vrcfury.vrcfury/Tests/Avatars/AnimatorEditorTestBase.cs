using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using UnityEditor;
using UnityEditor.Animations;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Animations;
using UnityEngine.Playables;
using UnityEngine.SceneManagement;
using VF.Utils;
using VF.Hooks.UnityFixes;
using Object = UnityEngine.Object;

namespace VF.Tests {
    internal abstract class AnimatorEditorTestBase {
        protected readonly List<Object> owned = new();
        protected T Own<T>(T value) where T : Object { owned.Add(value); return value; }

        [TearDown]
        public void TearDown() {
            foreach (var value in owned.AsEnumerable().Reverse()) if (value != null) Object.DestroyImmediate(value);
            owned.Clear();
        }

        protected AnimatorController Controller() {
            var controller = Own(new AnimatorController());
            controller.AddParameter("Value", AnimatorControllerParameterType.Float);
            controller.AddParameter("Number", AnimatorControllerParameterType.Int);
            controller.AddParameter("Enabled", AnimatorControllerParameterType.Bool);
            controller.AddLayer("Base");
            var machine = Own(controller.layers[0].stateMachine);
            var state = Own(machine.AddState("Idle"));
            state.motion = Own(new AnimationClip());
            return controller;
        }
    }
}
