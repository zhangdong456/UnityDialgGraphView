using System;

namespace DialogueSystem
{
    /// <summary>事件模板:完成一个任务。</summary>
    [Serializable]
    public class CompleteQuestEvent : DialogueEvent
    {
        public string questId;

        public override void Execute(DialogueContext context)
            => context.Quests.CompleteQuest(questId);

        public override string GetSummary() => $"完成任务[{questId}]";
    }
}
