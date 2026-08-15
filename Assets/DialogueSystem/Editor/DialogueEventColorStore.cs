using System;
using UnityEditor;
using UnityEngine;

namespace DialogueSystem.Editor
{
    /// <summary>
    /// 事件类型节点颜色的全局存储。
    /// 每种 DialogueEvent 子类(事件节点)一个颜色,按类型全局生效:
    /// 给"接取任务"设定颜色后,所有接取任务节点都使用该颜色。
    /// 颜色保存在本机 EditorPrefs(编辑器偏好),不写入对话资产;
    /// 换电脑需要重新设置。
    /// 未自定义的类型使用根据类型名生成的稳定自动色(同名类型颜色不变)。
    /// </summary>
    public static class DialogueEventColorStore
    {
        const string KeyPrefix = "DialogueSystem.EventNodeColor.";

        /// <summary>该事件类型是否已有用户自定义颜色。</summary>
        public static bool HasCustomColor(Type eventType)
        {
            return eventType != null && EditorPrefs.HasKey(KeyPrefix + eventType.FullName);
        }

        /// <summary>
        /// 事件节点的显示颜色:自定义色 > 自动分配色。
        /// eventType 为空(节点还没选事件类型)返回通用灰蓝。
        /// </summary>
        public static Color GetColor(Type eventType)
        {
            if (eventType == null) return new Color(0.55f, 0.62f, 0.72f);
            var stored = EditorPrefs.GetString(KeyPrefix + eventType.FullName, string.Empty);
            if (TryParse(stored, out var custom)) return custom;
            return GenerateAutoColor(eventType);
        }

        /// <summary>为事件类型设置自定义颜色(全局生效)。</summary>
        public static void SetColor(Type eventType, Color color)
        {
            if (eventType == null) return;
            EditorPrefs.SetString(KeyPrefix + eventType.FullName, Format(color));
        }

        /// <summary>清除自定义颜色,恢复该类型的自动分配色。</summary>
        public static void ClearColor(Type eventType)
        {
            if (eventType == null) return;
            EditorPrefs.DeleteKey(KeyPrefix + eventType.FullName);
        }

        /// <summary>根据类型全名生成稳定的自动颜色:同一类型在任何时候颜色一致。</summary>
        public static Color GenerateAutoColor(Type eventType)
        {
            if (eventType == null) return new Color(0.55f, 0.62f, 0.72f);

            // FNV-1a 哈希:稳定、分布均匀,类型名不变则颜色不变
            var name = eventType.FullName ?? eventType.Name;
            unchecked
            {
                uint hash = 2166136261;
                foreach (var c in name) hash = (hash ^ c) * 16777619;
                // 混入质数避免相近类型落到同一色档
                hash = (hash ^ 0x9E3779B9) * 2654435761;
                var hue = (hash % 360u) / 360f;
                return Color.HSVToRGB(hue, 0.55f, 0.85f);
            }
        }

        static string Format(Color c) =>
            $"{c.r:0.######}|{c.g:0.######}|{c.b:0.######}|{c.a:0.######}";

        static bool TryParse(string text, out Color color)
        {
            color = default;
            if (string.IsNullOrEmpty(text)) return false;
            var parts = text.Split('|');
            if (parts.Length != 4) return false;
            return float.TryParse(parts[0], out color.r) && float.TryParse(parts[1], out color.g)
                && float.TryParse(parts[2], out color.b) && float.TryParse(parts[3], out color.a);
        }
    }
}
