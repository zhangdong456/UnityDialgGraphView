using System;
using UnityEngine;

namespace DialogueSystem
{
    /// <summary>
    /// 所有对话节点的基类。
    /// 自定义节点继承此类后,会自动出现在图编辑器的"创建节点"菜单中,
    /// 默认带一个输入端口和一个输出端口,运行时默认沿 0 号输出端口继续。
    /// 如需自定义端口或运行行为,请在 DialogueGraphNode / DialoguePlayer 中扩展。
    /// </summary>
    [Serializable]
    public abstract class DialogueNodeData
    {
        [HideInInspector] public string guid;
        [HideInInspector] public Vector2 position;

        /// <summary>节点在图上显示的摘要文本。</summary>
        public virtual string GetSummary() => string.Empty;
    }
}
