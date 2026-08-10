using System;
using System.Collections.Generic;
using System.Text;
using UnityEngine;

namespace DialogueSystem
{
    /// <summary>
    /// 事件节点:按顺序执行一批 DialogueEvent(加任务、完成任务、改数值……),
    /// 然后沿"下一个"端口继续。事件类型可自由扩展,继承 DialogueEvent 即可。
    /// </summary>
    [Serializable]
    public class EventNode : DialogueNodeData
    {
        [SerializeReference]
        public List<DialogueEvent> events = new List<DialogueEvent>();

        public override string GetSummary()
        {
            if (events == null || events.Count == 0) return "(无事件)";
            var sb = new StringBuilder();
            foreach (var e in events)
            {
                if (e == null) continue;
                if (sb.Length > 0) sb.Append(", ");
                sb.Append(e.GetSummary());
                if (sb.Length > 50) { sb.Append("…"); break; }
            }
            return sb.Length == 0 ? "(无事件)" : sb.ToString();
        }
    }
}
