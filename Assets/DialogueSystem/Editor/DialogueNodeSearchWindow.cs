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
    /// </summary>
    public class DialogueNodeSearchWindow : ScriptableObject, ISearchWindowProvider
    {
        DialogueGraphView graphView;

        public void Init(DialogueGraphView graphView) => this.graphView = graphView;

        public List<SearchTreeEntry> CreateSearchTree(SearchWindowContext context)
        {
            var tree = new List<SearchTreeEntry>
            {
                new SearchTreeGroupEntry(new GUIContent("创建对话节点"), 0)
            };

            var types = TypeCache.GetTypesDerivedFrom<DialogueNodeData>()
                .Where(t => !t.IsAbstract && t != typeof(StartNode))
                .OrderBy(DialogueGraphNode.GetDisplayName);

            foreach (var type in types)
            {
                tree.Add(new SearchTreeEntry(new GUIContent(DialogueGraphNode.GetDisplayName(type)))
                {
                    level = 1,
                    userData = type
                });
            }
            return tree;
        }

        public bool OnSelectEntry(SearchTreeEntry entry, SearchWindowContext context)
        {
            var position = graphView.GetGraphMousePosition(context.screenMousePosition);
            graphView.CreateNode((Type)entry.userData, position);
            return true;
        }
    }
}
