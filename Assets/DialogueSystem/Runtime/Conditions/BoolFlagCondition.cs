using System;

namespace DialogueSystem
{
    /// <summary>布尔标记条件:黑板上的一个 bool 值等于期望值时满足。</summary>
    [DialogueEditorName("布尔标记", "检查黑板中的布尔值")]
    [Serializable]
    public class BoolFlagCondition : DialogueCondition
    {
        public string key;
        public bool expectedValue = true;

        public override bool Evaluate(DialogueContext context)
            => context.Blackboard.GetBool(key) == expectedValue;

        public override string GetSummary() => $"{key} == {expectedValue}";
    }
}
