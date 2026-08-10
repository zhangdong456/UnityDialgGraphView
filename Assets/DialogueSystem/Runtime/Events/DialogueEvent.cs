using System;

namespace DialogueSystem
{
    /// <summary>
    /// 对话事件基类。事件节点会按顺序执行其中每个事件的 Execute。
    /// 自定义事件继承此类即可,会自动出现在编辑器事件列表的添加菜单里。
    /// 模板示例见 AddQuestEvent / CompleteQuestEvent / SetIntEvent。
    /// </summary>
    [Serializable]
    public abstract class DialogueEvent
    {
        /// <summary>执行事件。context 为对话运行上下文(黑板数值、任务状态等)。</summary>
        public abstract void Execute(DialogueContext context);

        /// <summary>编辑器里显示的摘要。</summary>
        public virtual string GetSummary() => GetType().Name;
    }
}
