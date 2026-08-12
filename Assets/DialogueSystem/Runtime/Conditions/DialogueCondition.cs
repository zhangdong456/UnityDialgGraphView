using System;

namespace DialogueSystem
{
    /// <summary>
    /// 条件基类。用于选择节点的选项显示条件、状态分支节点的分支条件。
    /// 自定义条件继承此类并实现 Evaluate,会自动出现在编辑器的添加菜单里。
    /// </summary>
    [Serializable]
    public abstract class DialogueCondition
    {
        /// <summary>返回条件是否满足。context 为对话运行上下文(黑板数值、任务状态等)。</summary>
        public abstract bool Evaluate(DialogueContext context);

        /// <summary>编辑器里显示的摘要。</summary>
        public virtual string GetSummary() => DialogueTypeMetadata.GetDisplayName(GetType());
    }
}
