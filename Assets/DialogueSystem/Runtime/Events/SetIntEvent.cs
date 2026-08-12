using System;

namespace DialogueSystem
{
    /// <summary>事件模板:把黑板上的一个整数值设为指定值(如修改金币)。</summary>
    [DialogueEditorName("设置整数", "给黑板中的整数写入指定值")]
    [Serializable]
    public class SetIntEvent : DialogueEvent
    {
        public string key;
        public int value;

        public override void Execute(DialogueContext context)
            => context.Blackboard.SetInt(key, value);

        public override string GetSummary() => $"{key} = {value}";
    }
}
