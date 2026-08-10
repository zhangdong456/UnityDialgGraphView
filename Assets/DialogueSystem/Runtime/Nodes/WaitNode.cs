using System;
using UnityEngine;

namespace DialogueSystem
{
    /// <summary>
    /// 等待节点:暂停指定秒数后继续。
    /// 等待期间 UI 层应隐藏对话界面,用于播放人物动画等表现。
    /// </summary>
    [Serializable]
    public class WaitNode : DialogueNodeData
    {
        [Min(0f)]
        public float waitSeconds = 1f;

        public override string GetSummary() => $"等待 {waitSeconds:0.##} 秒";
    }
}
