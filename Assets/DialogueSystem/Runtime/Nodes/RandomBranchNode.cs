using System;
using System.Collections.Generic;
using UnityEngine;

namespace DialogueSystem
{
    /// <summary>
    /// 随机分支节点:先按条件过滤出所有满足条件的分支(条件规则与状态分支一致,
    /// 支持并/或组合),再从中**随机选一条**沿其端口继续;
    /// 没有任何分支满足条件时走末尾的"默认"端口。
    ///
    /// 随机源:全局共享的 System.Random(性能好,常规选择)。
    /// 需要可复现的随机(如回放/测试)时,在播放前调用 RandomBranchNode.ResetSeed(seed)。
    /// </summary>
    [Serializable]
    public class RandomBranchNode : DialogueNodeData
    {
        /// <summary>全局共享随机源。跨节点共享一个实例,避免每次 Evaluate 都新建。</summary>
        static System.Random sharedRandom = new System.Random();

        /// <summary>重置全局共享随机源的种子(用于测试复现/回放)。影响所有随机分支节点。</summary>
        public static void ResetSeed(int seed) => sharedRandom = new System.Random(seed);

        public List<BranchCase> cases = new List<BranchCase>();

        /// <summary>
        /// 返回随机选中的输出端口下标;没有任何分支满足条件时返回 cases.Count(即"默认"端口)。
        /// 与状态分支不同:条件列表为空的分支视为"无条件",永远参与随机池
        /// (与选择节点选项"空=无条件"的约定一致;否则纯随机场景必须写假条件)。
        /// </summary>
        public int Evaluate(DialogueContext context)
        {
            if (cases == null || cases.Count == 0) return 0;

            // 收集所有参与随机的分支下标:无条件,或条件满足
            List<int> matched = null;
            for (int i = 0; i < cases.Count; i++)
            {
                if (cases[i] == null) continue;
                var conditions = cases[i].conditions;
                if (conditions == null || conditions.Count == 0
                    || cases[i].Matches(context))
                    (matched ??= new List<int>()).Add(i);
            }

            // 没有参与随机的分支 → 默认端口(下标 = cases.Count)
            if (matched == null || matched.Count == 0) return cases.Count;

            // 参与者中随机一条(等概率)
            return matched[sharedRandom.Next(matched.Count)];
        }

        public override string GetSummary() => $"共 {(cases == null ? 0 : cases.Count)} 条分支,满足条件者随机走一条 + 默认";
    }
}
