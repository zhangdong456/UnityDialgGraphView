using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace DialogueSystem
{
    /// <summary>
    /// 状态分支节点里的一条分支:一组条件全部满足时命中。
    /// 注意:条件为空的分支永远不会命中,兜底请使用"默认"出口。
    /// </summary>
    [Serializable]
    public class BranchCase
    {
        public string label;

        [SerializeReference]
        public List<DialogueCondition> conditions = new List<DialogueCondition>();

        public bool Matches(DialogueContext context)
        {
            if (conditions == null || conditions.Count == 0) return false;
            return conditions.All(c => c != null && c.Evaluate(context));
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
            for (int i = 0; i < cases.Count; i++)
                if (cases[i] != null && cases[i].Matches(context))
                    return i;
            return cases.Count;
        }

        public override string GetSummary() => $"共 {cases.Count} 条分支 + 默认";
    }
}
