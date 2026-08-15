using System;
using System.Collections.Generic;
using UnityEngine;

namespace DialogueSystem
{
    /// <summary>
    /// 选择节点里的一个选项。
    /// 可以挂任意多个条件(DialogueCondition 子类):
    /// conditionMode = All(并)时全部满足才显示;Any(或)时任一满足即显示。留空表示无条件。
    /// </summary>
    [Serializable]
    public class ChoiceOption
    {
        public string choiceText;

        [Tooltip("多个条件的组合方式:并=全部满足才显示;或=任一满足即显示。默认并。")]
        public ConditionCombineMode conditionMode = ConditionCombineMode.All;

        [Tooltip("按上方组合方式判断;留空表示无条件")]
        [SerializeReference]
        public List<DialogueCondition> conditions = new List<DialogueCondition>();

        public bool IsVisible(DialogueContext context)
        {
            // 留空 = 无条件,始终显示(与旧版行为一致)
            if (conditions == null || conditions.Count == 0) return true;

            // 没有上下文时,存在任何真实条件都不满足(与旧版行为一致)
            if (context == null)
            {
                for (int i = 0; i < conditions.Count; i++)
                    if (conditions[i] != null) return false;
                return true;
            }

            if (conditionMode == ConditionCombineMode.Any)
            {
                // 或:任一真实条件满足即可;空元素忽略
                for (int i = 0; i < conditions.Count; i++)
                    if (conditions[i] != null && conditions[i].Evaluate(context))
                        return true;
                return false;
            }

            // 并:全部真实条件满足才显示;空元素忽略(与旧版 c == null 视为满足等价)
            for (int i = 0; i < conditions.Count; i++)
                if (conditions[i] != null && !conditions[i].Evaluate(context))
                    return false;
            return true;
        }
    }

    /// <summary>
    /// 选择节点:只负责提供多个选项,每个选项一个输出端口。
    /// 如果需要在选项前显示 NPC 的说话者和内容,请在它前面连接一个 DialogueNode。
    /// 运行时按条件过滤后交给 UI,玩家选择后沿对应端口继续。
    /// </summary>
    [Serializable]
    public class ChoiceNode : DialogueNodeData
    {
        public List<ChoiceOption> choices = new List<ChoiceOption>();

        public override string GetSummary() => $"共 {(choices == null ? 0 : choices.Count)} 个选项";
    }
}
