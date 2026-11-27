using System.Linq;
using NUnit.Framework;
using UnityEditor.Animations;
using VF.Utils;
using VF.Utils.Controller;

namespace VF.Tests {
    internal class VFLayerTest {
        [Test]
        public void RewritingOneConditionKeepsSurroundingConditionsInEachBranch() {
            var controller = VFController.Create();
            var layer = controller.NewLayer("Condition rewrites");
            var parameter = controller.GetOrCreateFloat("Middle");
            layer.NewState("Target").TransitionsFromAny().When(parameter.IsGreaterThan(0));
            var transition = layer.allTransitions.Single();
            var middle = transition.conditions.Single();
            var prefix = middle;
            prefix.parameter = "Prefix";
            var suffix = middle;
            suffix.parameter = "Suffix";
            transition.conditions = new[] { prefix, middle, suffix };

            layer.RewriteConditions(condition => condition);
            Assert.That(layer.allTransitions.Single(), Is.SameAs(transition));

            var first = middle;
            first.parameter = "First";
            var second = middle;
            second.parameter = "Second";
            layer.RewriteConditions(condition => {
                if (condition.parameter == "Middle") return AnimatorConditionExtensions.Rewritten.Or(first, second);
                return condition;
            });

            Assert.That(layer.allTransitions.Select(t => t.conditions), Is.EqualTo(new[] {
                new[] { prefix, first, suffix },
                new[] { prefix, second, suffix }
            }));
            Assert.That(transition.conditions, Is.EqualTo(new[] { prefix, middle, suffix }));
        }
    }
}
