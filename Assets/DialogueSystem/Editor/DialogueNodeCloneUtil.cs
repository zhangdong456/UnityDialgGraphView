using System;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using UnityEngine;

namespace DialogueSystem.Editor
{
    /// <summary>
    /// 对话节点的深拷贝工具(编辑器复制/粘贴用)。
    /// 节点树里含 [SerializeReference] 多态对象(事件/条件)与普通可序列化字段,
    /// 这里用"逐字段反射 + 集合递归"的方式完整克隆,不依赖 UnityEditor 的序列化 API,
    /// 因此可以被离线验证直接编译与断言。
    /// </summary>
    public static class DialogueNodeCloneUtil
    {
        /// <summary>
        /// 深拷贝一个节点(含所有嵌套的事件/条件/子对象/列表)。
        /// 返回的新实例与原节点完全独立,并生成新的 guid。
        /// </summary>
        public static DialogueNodeData Clone(DialogueNodeData source)
        {
            var clone = (DialogueNodeData)Activator.CreateInstance(source.GetType());
            CopyFields(source, clone, source.GetType());
            clone.guid = Guid.NewGuid().ToString("N");
            return clone;
        }

        static void CopyFields(object source, object target, Type declaredType)
        {
            for (var t = declaredType; t != null && t != typeof(object); t = t.BaseType)
            {
                foreach (var f in t.GetFields(BindingFlags.Instance | BindingFlags.Public
                    | BindingFlags.NonPublic | BindingFlags.DeclaredOnly))
                {
                    if (f.IsInitOnly) continue;                 // readonly 跳过
                    if (f.IsLiteral) continue;                  // const 跳过
                    var value = f.GetValue(source);
                    f.SetValue(target, DeepCloneValue(value, f.FieldType));
                }
            }
        }

        static object DeepCloneValue(object value, Type fieldType)
        {
            if (value == null) return null;

            var type = value.GetType();

            // 字符串不可变,直接共享
            if (type == typeof(string)) return value;

            // UnityEngine.Object 引用(如 JumpNode.targetDialogue、DialogueNode.voiceClip):共享引用不克隆
            if (value is UnityEngine.Object) return value;

            // [SerializeReference] 多态基元:按运行时类型逐字段克隆(事件/条件都是这一类)
            if (type.IsClass && !type.IsArray && !typeof(IEnumerable).IsAssignableFrom(type))
            {
                var clone = Activator.CreateInstance(type);
                CopyFields(value, clone, type);
                return clone;
            }

            // 数组(节点数据里目前没有,留作兜底)
            if (type.IsArray)
            {
                var arr = (Array)value;
                var copy = Array.CreateInstance(type.GetElementType(), arr.Length);
                for (int i = 0; i < arr.Length; i++)
                    copy.SetValue(DeepCloneValue(arr.GetValue(i), type.GetElementType()), i);
                return copy;
            }

            // List<T>
            if (type.IsGenericType && type.GetGenericTypeDefinition() == typeof(List<>))
            {
                var copy = (IList)Activator.CreateInstance(type);
                foreach (var item in (IEnumerable)value)
                    copy.Add(DeepCloneValue(item, type.GetGenericArguments()[0]));
                return copy;
            }

            // 其他可枚举集合:按 List 近似重建(当前数据模型未用到)
            if (value is IEnumerable enumerable && !(value is IDictionary))
            {
                var itemType = type.IsGenericType ? type.GetGenericArguments()[0] : typeof(object);
                var listType = typeof(List<>).MakeGenericType(itemType);
                var copy = (IList)Activator.CreateInstance(listType);
                foreach (var item in enumerable)
                    copy.Add(DeepCloneValue(item, itemType));
                return copy;
            }

            // 枚举/基元/结构体:直接返回
            return value;
        }
    }
}
