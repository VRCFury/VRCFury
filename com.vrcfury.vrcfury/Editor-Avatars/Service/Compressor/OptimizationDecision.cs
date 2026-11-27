using System;
using System.Collections.Generic;
using System.Linq;
using VF.Utils;
using VRC.SDK3.Avatars.ScriptableObjects;

namespace VF.Service.Compressor {
    internal class OptimizationDecision {
        public int numberSlots = 0;
        public int boolSlots = 0;

        public IList<VRCExpressionParameters.Parameter> compress = new VRCExpressionParameters.Parameter[] { };

        public int GetIndexBitCount() {
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
            return originalCost - compress.Sum(p => p.TypeCost()) + GetSyncCost(numberSlots, boolSlots, GetBatchCount());
        }

        public int GetBatchCount() {
            var boolCount = compress.Count(p => p.valueType == VRCExpressionParameters.ValueType.Bool);
            return GetBatchCount(compress.Count - boolCount, boolCount, numberSlots, boolSlots);
        }

        private static int GetBatchCount(int numberCount, int boolCount, int numberSlots, int boolSlots) {
            int CountBatches(int count, int slots) => count == 0 ? 0 : (count - 1) / slots + 1;
            return Math.Max(CountBatches(numberCount, numberSlots), CountBatches(boolCount, boolSlots));
        }

        private static int GetSyncCost(int numberSlots, int boolSlots, int batchCount) {
            return GetIndexBitCount(batchCount) + numberSlots * 8 + boolSlots;
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
            var numberCount = compress.Count - boolCount;
            boolSlots = boolCount > 0 ? 1 : 0;
            numberSlots = numberCount > 0 ? 1 : 0;
            var maxCost = ExpressionParamsService.GetMaxCost();
            var fixedCost = originalCost - compress.Sum(p => p.TypeCost());
            bool Fits(int numbers, int bools) {
                var batches = GetBatchCount(numberCount, boolCount, numbers, bools);
                return fixedCost + GetSyncCost(numbers, bools, batches) <= maxCost;
            }
            while (true) {
                if (numberSlots < numberCount
                    && Fits(numberSlots + 1, boolSlots)
                    && (boolCount == 0 || (float)numberSlots / numberCount < (float)boolSlots / boolCount)
                   ) {
                    numberSlots++;
                } else if (boolSlots < boolCount
                           && Fits(numberSlots, boolSlots + 1)
                          ) {
                    boolSlots++;
                } else {
                    break;
                }
            }
        }
    }
}
