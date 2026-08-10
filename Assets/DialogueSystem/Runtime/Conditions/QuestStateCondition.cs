using System;

namespace DialogueSystem
{
    /// <summary>任务状态条件:某个任务处于指定状态(未接取/进行中/已完成)时满足。</summary>
    [Serializable]
    public class QuestStateCondition : DialogueCondition
    {
        public string questId;
        public QuestStatus requiredStatus = QuestStatus.Completed;

        public override bool Evaluate(DialogueContext context)
            => context.Quests.GetStatus(questId) == requiredStatus;

        public override string GetSummary() => $"任务[{questId}] {requiredStatus}";
    }
}
