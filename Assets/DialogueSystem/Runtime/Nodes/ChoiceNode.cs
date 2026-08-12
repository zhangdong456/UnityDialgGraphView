using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace DialogueSystem
{
    /// <summary>
    /// 选择节点里的一个选项。
    /// 可以挂任意多个条件(DialogueCondition 子类),全部满足时该选项才会显示给玩家。
    /// </summary>
    [Serializable]
    public class ChoiceOption
    {
        public string choiceText;

        [Tooltip("全部满足时才显示该选项;留空表示无条件")]
        [SerializeReference]
        public List<DialogueCondition> conditions = new List<DialogueCondition>();

        public bool IsVisible(DialogueContext context)
        {
            if (conditions == null) return true;
            if (context == null && conditions.Any(c => c != null)) return false;
            return conditions.All(c => c == null || c.Evaluate(context));
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
