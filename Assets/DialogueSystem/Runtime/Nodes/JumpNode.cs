using System;
using UnityEngine;

namespace DialogueSystem
{
    /// <summary>
    /// 跳转节点:跳转到另一个对话资产的入口节点继续执行。
    /// 没有输出端口;目标为空时视为对话结束。
    /// </summary>
    [Serializable]
    public class JumpNode : DialogueNodeData
    {
        [Tooltip("要跳转过去的对话资产")]
        public DialogueGraphAsset targetDialogue;

        public override string GetSummary() =>
            targetDialogue == null ? "(未设置目标)" : $"→ {targetDialogue.name}";
    }
}
