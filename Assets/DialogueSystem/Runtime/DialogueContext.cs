using System;
using System.Collections.Generic;

namespace DialogueSystem
{
    public enum QuestStatus
    {
        NotStarted,
        Active,
        Completed
    }

    /// <summary>
    /// 极简任务记录,供条件与事件读写。
    /// 接入你自己的任务系统时,可在自定义 DialogueEvent / DialogueCondition 里改写,
    /// 或干脆弃用本类。
    /// </summary>
    [Serializable]
    public class QuestLog
    {
        readonly HashSet<string> active = new HashSet<string>();
        readonly HashSet<string> completed = new HashSet<string>();

        public void AddQuest(string questId)
        {
            if (string.IsNullOrEmpty(questId)) return;
            if (!completed.Contains(questId)) active.Add(questId);
        }

        public void CompleteQuest(string questId)
        {
            if (string.IsNullOrEmpty(questId)) return;
            active.Remove(questId);
            completed.Add(questId);
        }

        public QuestStatus GetStatus(string questId)
        {
            if (string.IsNullOrEmpty(questId)) return QuestStatus.NotStarted;
            if (completed.Contains(questId)) return QuestStatus.Completed;
            if (active.Contains(questId)) return QuestStatus.Active;
            return QuestStatus.NotStarted;
        }

        public bool IsActive(string questId) => GetStatus(questId) == QuestStatus.Active;
        public bool IsCompleted(string questId) => GetStatus(questId) == QuestStatus.Completed;
    }

    /// <summary>
    /// 黑板:按 key 存放游戏数值(int/bool/float/string),供条件判断与事件修改。
    /// 游戏代码在开始对话前把状态写进来,例如 blackboard.SetInt("gold", 100)。
    /// </summary>
    [Serializable]
    public class Blackboard
    {
        readonly Dictionary<string, int> ints = new Dictionary<string, int>();
        readonly Dictionary<string, bool> bools = new Dictionary<string, bool>();
        readonly Dictionary<string, float> floats = new Dictionary<string, float>();
        readonly Dictionary<string, string> strings = new Dictionary<string, string>();

        public int GetInt(string key) => ints.TryGetValue(key ?? "", out var v) ? v : 0;
        public void SetInt(string key, int value) { if (key != null) ints[key] = value; }

        public bool GetBool(string key) => bools.TryGetValue(key ?? "", out var v) && v;
        public void SetBool(string key, bool value) { if (key != null) bools[key] = value; }

        public float GetFloat(string key) => floats.TryGetValue(key ?? "", out var v) ? v : 0f;
        public void SetFloat(string key, float value) { if (key != null) floats[key] = value; }

        public string GetString(string key) => strings.TryGetValue(key ?? "", out var v) ? v : string.Empty;
        public void SetString(string key, string value) { if (key != null) strings[key] = value; }
    }

    /// <summary>
    /// 对话运行上下文:条件和事件通过它读写游戏状态。
    /// 一次对话使用一个实例;也可跨多次对话复用以保留状态。
    /// </summary>
    public class DialogueContext
    {
        public Blackboard Blackboard { get; } = new Blackboard();
        public QuestLog Quests { get; } = new QuestLog();
    }
}
