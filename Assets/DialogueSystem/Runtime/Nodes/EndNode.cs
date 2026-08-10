using System;

namespace DialogueSystem
{
    /// <summary>
    /// 结束节点:只有输入端口,走到这里对话立即结束(触发 OnEnd)。
    /// 用于显式标记对话的终点,尤其是一张图有多个出口时。
    /// </summary>
    [Serializable]
    public class EndNode : DialogueNodeData
    {
        public override string GetSummary() => "对话到此结束";
    }
}
