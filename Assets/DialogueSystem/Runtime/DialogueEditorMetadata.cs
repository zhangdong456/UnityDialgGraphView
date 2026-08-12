using System;
using System.Reflection;

namespace DialogueSystem
{
    /// <summary>
    /// 为节点、条件或事件提供编辑器显示名称和鼠标提示。
    /// 不改变类名、序列化类型名或运行时逻辑。
    /// </summary>
    [AttributeUsage(AttributeTargets.Class, Inherited = false, AllowMultiple = false)]
    public sealed class DialogueEditorNameAttribute : Attribute
    {
        public string DisplayName { get; }
        public string Description { get; }

        public DialogueEditorNameAttribute(string displayName)
        {
            DisplayName = displayName;
        }

        public DialogueEditorNameAttribute(string displayName, string description)
        {
            DisplayName = displayName;
            Description = description;
        }
    }

    /// <summary>读取编辑器显示元数据的运行时安全工具,不依赖 UnityEditor。</summary>
    public static class DialogueTypeMetadata
    {
        public static string GetDisplayName(Type type)
        {
            if (type == null) return string.Empty;
            var attribute = type.GetCustomAttribute<DialogueEditorNameAttribute>();
            return attribute == null || string.IsNullOrWhiteSpace(attribute.DisplayName)
                ? type.Name
                : attribute.DisplayName;
        }

        public static string GetDescription(Type type)
        {
            if (type == null) return string.Empty;
            var attribute = type.GetCustomAttribute<DialogueEditorNameAttribute>();
            return attribute?.Description ?? string.Empty;
        }
    }
}
