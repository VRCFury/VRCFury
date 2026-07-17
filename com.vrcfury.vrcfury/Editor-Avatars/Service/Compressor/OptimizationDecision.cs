using System;
using System.Collections.Generic;
using System.Linq;
using VF.Utils;
using VRC.SDK3.Avatars.ScriptableObjects;

namespace VF.Service.Compressor {
    internal class OptimizationDecision {
        public int numberSlots = 0;
        public int boolSlots = 0;
        public bool useBadPriorityMethod;
        public IList<VRCExpressionParameters.Parameter> compress = new VRCExpressionParameters.Parameter[] { };

        private OptimizationDecision TempCopy(Action<OptimizationDecision> with) {
            var copy = new OptimizationDecision {
                numberSlots = numberSlots,
                boolSlots = boolSlots,
                useBadPriorityMethod = useBadPriorityMethod,
                compress = compress.ToList()
            };
            with.Invoke(copy);
            return copy;
        }

        public int GetIndexBitCount() {
            if (useBadPriorityMethod) {
                return 8;
            }

            return GetIndexBitCount(GetBatchCount());
        }

        public static int GetIndexBitCount(int batchCount) {
            var maxSyncId = batchCount + 1;
            var bits = 1;
            while ((1 << bits) < maxSyncId) {
                bits++;
            }
            return bits;
        }

        public int GetFinalCost(int originalCost) {
            return originalCost
                   + GetIndexBitCount()
                   + numberSlots * 8
                   + boolSlots
                   - compress.Sum(p => p.TypeCost());
        }

        public int GetBatchCount() {
            var batches = GetBatches();
            return Math.Max(batches.numberBatches.Count, batches.boolBatches.Count);
        }

        public (
            List<List<VRCExpressionParameters.Parameter>> numberBatches,
            List<List<VRCExpressionParameters.Parameter>> boolBatches
            ) GetBatches() {
            var numbersToOptimize =
                compress.Where(i => i.valueType != VRCExpressionParameters.ValueType.Bool).ToList();
            var boolsToOptimize =
                compress.Where(i => i.valueType == VRCExpressionParameters.ValueType.Bool).ToList();
            var numberBatches = numbersToOptimize
                .Chunk(numberSlots)
                .Select(chunk => chunk.ToList())
                .ToList();
            var boolBatches = boolsToOptimize
                .Chunk(boolSlots)
                .Select(chunk => chunk.ToList())
                .ToList();
            return (numberBatches, boolBatches);
        }

        /**
         * Attempts to expand the number of used number and bool slots up until the avatar's bits are full,
         * to increase parallelism and reduce the time needed for a full sync.
         * If both bools and numbers are compressed, it attempts to keep the batch count the same so it's not
         * wasting time syncing only bools or only numbers during some batches.
         */
        public void Optimize(int originalCost) {
            var boolCount = compress.Count(p => p.valueType == VRCExpressionParameters.ValueType.Bool);
            var numberCount = compress.Count(p => p.valueType != VRCExpressionParameters.ValueType.Bool);
            boolSlots = boolCount > 0 ? 1 : 0;
            numberSlots = numberCount > 0 ? 1 : 0;
            var maxCost = VRCExpressionParametersExtensions.GetMaxCost();
            //maxCost = 50;
            while (true) {
                if (numberSlots < numberCount
                    && TempCopy(o => o.numberSlots++).GetFinalCost(originalCost) <= maxCost
                    && (boolCount == 0 || (float)numberSlots / numberCount < (float)boolSlots / boolCount)
                   ) {
                    numberSlots++;
                } else if (boolSlots < boolCount
                           && TempCopy(o => o.boolSlots++).GetFinalCost(originalCost) <= maxCost
                          ) {
                    boolSlots++;
                } else {
                    break;
                }
            }
        }
    }
}
