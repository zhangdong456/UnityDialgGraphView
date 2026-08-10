using System;
using UnityEngine;

namespace DialogueSystem
{
    /// <summary>普通对话节点:一个说话者说一段话,然后走向"下一个"端口。</summary>
    [Serializable]
    public class DialogueNode : DialogueNodeData
    {
        public string speakerName;

        [TextArea(2, 6)]
        public string dialogueText;

        [Tooltip("可选:这段对话的语音")]
        public AudioClip voiceClip;

        public override string GetSummary()
        {
            var text = string.IsNullOrEmpty(dialogueText) ? "(空对话)" : dialogueText;
            return string.IsNullOrEmpty(speakerName) ? text : $"{speakerName}: {text}";
        }
    }
}
