using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEditor.Experimental.GraphView;
using UnityEngine;

namespace DialogueSystem.Editor
{
    /// <summary>
    /// 右键创建节点的搜索窗口。
    /// 通过 TypeCache 收集所有 DialogueNodeData 的非抽象子类,
    /// 用户自定义的节点类型会自动出现在菜单里。
    /// 开始节点全图唯一,由编辑器自动创建,不出现在菜单中。
    ///
    /// 菜单结构:
    ///   level 0: 创建对话节点
    ///   level 1: 常规节点(对话/选择/状态分支/等待/跳转/结束) + "事件节点"分组
    ///   level 2: 事件节点分组下,每个 DialogueEvent 子类一个条目
    ///            (选中即创建该事件类型的 SingleEventNode);
    ///            也有一个"空白事件节点"条目,创建后再选事件类型。
    /// </summary>
    public class DialogueNodeSearchWindow : ScriptableObject, ISearchWindowProvider
    {
        DialogueGraphView graphView;

        /// <summary>空白单事件节点的菜单条目名。</summary>
        public const string BlankEventEntryName = "空白事件节点(创建后再选类型)";

        public void Init(DialogueGraphView graphView) => this.graphView = graphView;

        public List<SearchTreeEntry> CreateSearchTree(SearchWindowContext context)
        {
            var tree = new List<SearchTreeEntry>
            {
                new SearchTreeGroupEntry(new GUIContent("创建对话节点"), 0)
            };

            // 事件类型(TypeCache 自动收集,自定义事件子类自动出现在分组里)
            var eventTypes = TypeCache.GetTypesDerivedFrom<DialogueEvent>()
                .Where(t => !t.IsAbstract && !t.ContainsGenericParameters && t.GetConstructor(Type.EmptyTypes) != null)
                .OrderBy(t => DialogueTypeMetadata.GetDisplayName(t), StringComparer.OrdinalIgnoreCase)
                .ToList();

            // 常规节点:SingleEventNode 不直接出现在顶层,它通过事件分组创建
            var nodeTypes = TypeCache.GetTypesDerivedFrom<DialogueNodeData>()
                .Where(t => !t.IsAbstract && t != typeof(StartNode) && t != typeof(SingleEventNode))
                .OrderBy(DialogueGraphNode.GetDisplayName, StringComparer.OrdinalIgnoreCase);

            foreach (var type in nodeTypes)
            {
                tree.Add(new SearchTreeEntry(new GUIContent(
                    DialogueGraphNode.GetDisplayName(type),
                    DialogueTypeMetadata.GetDescription(type)))
                {
                    level = 1,
                    userData = type
                });
            }

            // "事件节点"分组:展开后列出所有事件类型,每一种都是一个独立节点
            tree.Add(new SearchTreeGroupEntry(new GUIContent("事件节点"), 1));
            tree.Add(new SearchTreeEntry(new GUIContent(BlankEventEntryName,
                "创建一个未选择事件类型的空白事件节点,稍后在详情面板选择类型"))
            {
                level = 2,
                userData = typeof(SingleEventNode)
            });
            foreach (var type in eventTypes)
            {
                tree.Add(new SearchTreeEntry(new GUIContent(
                    DialogueTypeMetadata.GetDisplayName(type),
                    DialogueTypeMetadata.GetDescription(type)))
                {
                    level = 2,
                    userData = type
                });
            }
            return tree;
        }

        public bool OnSelectEntry(SearchTreeEntry entry, SearchWindowContext context)
        {
            var position = graphView.GetGraphMousePosition(context.screenMousePosition);

            if (entry.userData is Type selectedType && typeof(DialogueEvent).IsAssignableFrom(selectedType))
            {
                // 事件分组里的具体事件类型:创建对应类型的单事件节点
                graphView.CreateSingleEventNode(selectedType, position);
                return true;
            }

            graphView.CreateNode((Type)entry.userData, position);
            return true;
        }
    }
}
