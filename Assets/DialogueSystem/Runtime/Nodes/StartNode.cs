using System;

namespace DialogueSystem
{
    /// <summary>
    /// 开始节点:对话图的入口,每张图有且仅有一个,由图编辑器自动创建。
    /// 没有输入端口,只有一个输出端口;播放时从这里开始,沿输出端口进入第一个节点。
    /// </summary>
    [Serializable]
    public class StartNode : DialogueNodeData
    {
        public override string GetSummary() => "对话从这里开始";
    }
}
