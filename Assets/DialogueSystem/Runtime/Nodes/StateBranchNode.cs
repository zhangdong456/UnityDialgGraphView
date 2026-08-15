using System;
using System.Collections.Generic;
using UnityEngine;

namespace DialogueSystem
{
    /// <summary>
    /// 状态分支节点里的一条分支:一组条件按组合方式判断是否命中。
    /// conditionMode = All(并)时全部满足才命中;Any(或)时任一满足即命中。
    /// 注意:条件为空的分支永远不会命中,兜底请使用"默认"出口。
    /// </summary>
    [Serializable]
    public class BranchCase
    {
        public string label;

        [Tooltip("多个条件的组合方式:并=全部满足才命中;或=任一满足即命中。默认并。")]
        public ConditionCombineMode conditionMode = ConditionCombineMode.All;

        [SerializeReference]
        public List<DialogueCondition> conditions = new List<DialogueCondition>();

        public bool Matches(DialogueContext context)
        {
            // 空条件分支永不命中(兜底必须连"默认"端口,与旧版行为一致)
            if (conditions == null || conditions.Count == 0 || context == null) return false;

            if (conditionMode == ConditionCombineMode.Any)
            {
                // 或:任一真实条件满足即命中;空元素忽略
                for (int i = 0; i < conditions.Count; i++)
                    if (conditions[i] != null && conditions[i].Evaluate(context))
                        return true;
                return false;
            }

            // 并:全部条件必须真实满足;空元素视为不满足(与旧版 c != null 语义一致)
            for (int i = 0; i < conditions.Count; i++)
                if (conditions[i] == null || !conditions[i].Evaluate(context))
                    return false;
            return true;
        }
    }

    /// <summary>
    /// 状态分支节点:自上而下判断,命中第一个满足条件的分支并沿其端口继续;
    /// 都不满足时走"默认"出口。
    /// 布尔判断建 1 条分支即可(命中/默认 两条线);
    /// 多值判断(如事件等级 1/2/3/4)建多条分支即可切出多条线。
    /// </summary>
    [Serializable]
    public class StateBranchNode : DialogueNodeData
    {
        public List<BranchCase> cases = new List<BranchCase>();

        /// <summary>返回命中的输出端口下标;cases.Count 表示"默认"出口。</summary>
        public int Evaluate(DialogueContext context)
        {
            if (cases == null) return 0;
            for (int i = 0; i < cases.Count; i++)
                if (cases[i] != null && cases[i].Matches(context))
                    return i;
            return cases.Count;
        }

        public override string GetSummary() => $"共 {(cases == null ? 0 : cases.Count)} 条分支 + 默认";
    }
}
