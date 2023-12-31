using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using UnityEditor.Animations;
using VF.Inspector;
using AnimatorConditions = System.Collections.Generic.IEnumerable<UnityEditor.Animations.AnimatorCondition>;
using AnimatorConditionsUnion = System.Collections.Generic.IEnumerable<
    System.Collections.Generic.IEnumerable<UnityEditor.Animations.AnimatorCondition>>;

namespace VF.Builder {
    /**
     * This class can take any arbitrary set of animator transform conditions,
     * and AND, OR, or NOT them cleanly with other conditions.
     */
    public static class AnimatorConditionLogic {
        private static AnimatorCondition Not(AnimatorCondition input) {
            var copy = input;
            switch (copy.mode) {
                case AnimatorConditionMode.Equals:
                    copy.mode = AnimatorConditionMode.NotEqual;
                    break;
                case AnimatorConditionMode.Greater:
                    copy.mode = AnimatorConditionMode.Less;
                    copy.threshold = VRCFuryEditorUtils.NextFloatUp(copy.threshold);
                    break;
                case AnimatorConditionMode.If:
                    copy.mode = AnimatorConditionMode.IfNot;
                    break;
                case AnimatorConditionMode.Less:
                    copy.mode = AnimatorConditionMode.Greater;
                    copy.threshold = VRCFuryEditorUtils.NextFloatDown(copy.threshold);
                    break;
                case AnimatorConditionMode.IfNot:
                    copy.mode = AnimatorConditionMode.If;
                    break;
                case AnimatorConditionMode.NotEqual:
                    copy.mode = AnimatorConditionMode.Equals;
                    break;
                default:
                    throw new Exception("Unknown condition mode: " + copy.mode);
            }
            return copy;
        }
        
        public static AnimatorConditionsUnion Not(AnimatorConditionsUnion input) {
            AnimatorConditionsUnion emptyProduct = new[] { Enumerable.Empty<AnimatorCondition>() }; 
            var output = input.Aggregate(
                emptyProduct, 
                (accumulator, sequence) => 
                    from accseq in accumulator 
                    from item in sequence 
                    select accseq.Concat(new[] {Not(item)}));
            return Simplify(output);
        }
        
        public static AnimatorConditionsUnion And(AnimatorConditionsUnion in1, AnimatorConditionsUnion in2) {
            var combined = in1.SelectMany(a => in2.Select(b => a.Concat(b)));
            return Simplify(combined);
        }

        public static AnimatorConditionsUnion Or(AnimatorConditionsUnion in1, AnimatorConditionsUnion in2) {
            var combined = in1.Concat(in2);
            return Simplify(combined);
        }

        private static AnimatorConditionsUnion Simplify(AnimatorConditionsUnion conds) {
            // Simplify each transform
            conds = conds.Select(Simplify);

            // Remove duplicates
            var all = conds
                .Select(c => new HashSet<AnimatorCondition>(c))
                .Distinct(HashSet<AnimatorCondition>.CreateSetComparer())
                .ToArray();
            
            // Remove supersets
            conds = all.Where(c => {
                var set = c.ToImmutableHashSet();
                var hasSubset = all.Any(other => c != other && set.IsProperSupersetOf(other));
                return !hasSubset;
            });

            // Remove impossible
            conds = conds.Where(c => !IsImpossible(c));

            return conds.ToArray();
        }

        private static string Stringify(AnimatorConditions conds) {
            return string.Join("|", conds.Select(c => c.mode + "." + c.parameter + "." + c.threshold));
        }
        
        private static AnimatorConditions Simplify(AnimatorConditions conds) {
            var all = conds.ToArray();

            // Remove redundant conditions
            conds = all.Where(c => !IsRedundant(c, all));
            
            // Remove duplicate conditions
            conds = conds.GroupBy(c => c).Select(grp => grp.First());
            
            // Sort
            conds = conds.OrderBy(c => c.parameter);

            return conds.ToArray();
        }

        private static bool IsImpossible(AnimatorConditions conds) {
            var all = conds.ToList();
            return all.Any(c => IsImpossible(c, all));
        }

        private static bool IsImpossible(AnimatorCondition rule, AnimatorConditions others) {
            foreach (var other in others) {
                if (rule.parameter != other.parameter) continue;
                switch (rule.mode) {
                    case AnimatorConditionMode.Equals:
                        if (other.mode == AnimatorConditionMode.Greater && other.threshold >= rule.threshold)
                            return true;
                        if (other.mode == AnimatorConditionMode.Equals && other.threshold != rule.threshold)
                            return true;
                        if (other.mode == AnimatorConditionMode.Less && other.threshold <= rule.threshold)
                            return true;
                        if (other.mode == AnimatorConditionMode.NotEqual && other.threshold == rule.threshold)
                            return true;
                        break;
                    case AnimatorConditionMode.Greater:
                        if (other.mode == AnimatorConditionMode.Equals && other.threshold <= rule.threshold)
                            return true;
                        if (other.mode == AnimatorConditionMode.Less && other.threshold <= rule.threshold)
                            return true;
                        break;
                    case AnimatorConditionMode.Less:
                        if (other.mode == AnimatorConditionMode.Equals && other.threshold >= rule.threshold)
                            return true;
                        if (other.mode == AnimatorConditionMode.Greater && other.threshold >= rule.threshold)
                            return true;
                        break;
                    case AnimatorConditionMode.NotEqual:
                        if (other.mode == AnimatorConditionMode.Equals && other.threshold == rule.threshold)
                            return true;
                        break;
                    case AnimatorConditionMode.If:
                        if (other.mode == AnimatorConditionMode.IfNot) return true;
                        break;
                    case AnimatorConditionMode.IfNot:
                        if (other.mode == AnimatorConditionMode.If) return true;
                        break;
                }
            }
            return false;
        }
        private static bool IsRedundant(AnimatorCondition rule, AnimatorConditions others) {
            foreach (var other in others) {
                if (rule.parameter != other.parameter) continue;
                switch (rule.mode) {
                    case AnimatorConditionMode.Greater:
                        if (other.mode == AnimatorConditionMode.Greater && other.threshold > rule.threshold)
                            return true;
                        if (other.mode == AnimatorConditionMode.Equals && other.threshold > rule.threshold)
                            return true;
                        break;
                    case AnimatorConditionMode.Less:
                        if (other.mode == AnimatorConditionMode.Less && other.threshold < rule.threshold)
                            return true;
                        if (other.mode == AnimatorConditionMode.Equals && other.threshold < rule.threshold)
                            return true;
                        break;
                    case AnimatorConditionMode.NotEqual:
                        if (other.mode == AnimatorConditionMode.Greater && other.threshold >= rule.threshold)
                            return true;
                        if (other.mode == AnimatorConditionMode.Equals && other.threshold != rule.threshold)
                            return true;
                        if (other.mode == AnimatorConditionMode.Less && other.threshold <= rule.threshold)
                            return true;
                        break;
                }
            }
            return false;
        }
    }
}