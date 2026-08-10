using System;

namespace DialogueSystem
{
    /// <summary>事件模板:为玩家接取一个任务。</summary>
    [Serializable]
    public class AddQuestEvent : DialogueEvent
    {
        public string questId;

        public override void Execute(DialogueContext context)
            => context.Quests.AddQuest(questId);

        public override string GetSummary() => $"接取任务[{questId}]";
    }
}
