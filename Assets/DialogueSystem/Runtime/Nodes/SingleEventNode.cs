using System;
using UnityEngine;

namespace DialogueSystem
{
    /// <summary>
    /// 单事件节点:执行一个 DialogueEvent,然后沿"下一个"端口继续。
    /// 编辑器创建菜单的"事件节点"分组里,每个 DialogueEvent 子类都是一种独立节点;
    /// 详情面板可以随时更换事件类型;节点颜色按事件类型全局自定义(编辑器功能)。
    /// </summary>
    [Serializable]
    public class SingleEventNode : DialogueNodeData
    {
        [Tooltip("该节点执行的事件")]
        [SerializeReference]
        public DialogueEvent eventData;

        public override string GetSummary()
            => eventData == null ? "(未选择事件类型)" : eventData.GetSummary();
    }
}
