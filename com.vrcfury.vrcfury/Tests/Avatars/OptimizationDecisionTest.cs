using System;
using System.Linq;
using NUnit.Framework;
using VF.Service;
using VF.Service.Compressor;
using VF.Utils;
using VRC.SDK3.Avatars.ScriptableObjects;

namespace VF.Tests {
    internal class OptimizationDecisionTest {
        [TestCase(0, 0, 0, 0)]
        [TestCase(3, 0, 2, 0)]
        [TestCase(0, 33, 0, 3)]
        [TestCase(7, 33, 2, 12)]
        public void CountMatchesEmittedBatches(int numbers, int bools, int numberSlots, int boolSlots) {
            var decision = new OptimizationDecision {
                compress = Parameters(numbers, bools),
                numberSlots = numberSlots,
                boolSlots = boolSlots
            };
            var batches = decision.GetBatches();
            Assert.That(decision.GetBatchCount(), Is.EqualTo(Math.Max(batches.numberBatches.Count, batches.boolBatches.Count)));
        }

        [TestCase(20, 0, 8)]
        [TestCase(0, 300, 0)]
        [TestCase(80, 200, 42)]
        [TestCase(2, 5, 250)]
        public void SlotSelectionMatchesCostOfActualBatches(int numbers, int bools, int otherCost) {
            var parameters = Parameters(numbers, bools);
            var originalCost = otherCost + parameters.Sum(p => p.TypeCost());
            var expected = new OptimizationDecision {
                compress = parameters,
                numberSlots = numbers > 0 ? 1 : 0,
                boolSlots = bools > 0 ? 1 : 0
            };

            bool Fits(int numberSlots, int boolSlots) {
                var trial = new OptimizationDecision {
                    compress = parameters,
                    numberSlots = numberSlots,
                    boolSlots = boolSlots
                };
                var batches = trial.GetBatches();
                var count = Math.Max(batches.numberBatches.Count, batches.boolBatches.Count);
                var cost = otherCost + OptimizationDecision.GetIndexBitCount(count) + numberSlots * 8 + boolSlots;
                return cost <= ExpressionParamsService.GetMaxCost();
            }

            while (true) {
                if (expected.numberSlots < numbers && Fits(expected.numberSlots + 1, expected.boolSlots)
                    && (bools == 0 || (float)expected.numberSlots / numbers < (float)expected.boolSlots / bools)) {
                    expected.numberSlots++;
                } else if (expected.boolSlots < bools && Fits(expected.numberSlots, expected.boolSlots + 1)) {
                    expected.boolSlots++;
                } else {
                    break;
                }
            }

            var optimized = new OptimizationDecision { compress = parameters };
            optimized.Optimize(originalCost);
            Assert.That(optimized.numberSlots, Is.EqualTo(expected.numberSlots));
            Assert.That(optimized.boolSlots, Is.EqualTo(expected.boolSlots));
        }

        private static VRCExpressionParameters.Parameter[] Parameters(int numbers, int bools) =>
            Enumerable.Range(0, numbers + bools).Select(index => new VRCExpressionParameters.Parameter {
                name = "Parameter " + index,
                valueType = index < numbers ? VRCExpressionParameters.ValueType.Float : VRCExpressionParameters.ValueType.Bool
            }).ToArray();
    }
}
