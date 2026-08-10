using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEditor.Experimental.GraphView;
using UnityEngine;
using UnityEngine.UIElements;

namespace DialogueSystem.Editor
{
    /// <summary>
    /// 对话图的 GraphView:负责节点/连线的创建、展示与把图保存回资产。
    /// </summary>
    public class DialogueGraphView : GraphView
    {
        readonly DialogueGraphWindow window;
        readonly DialogueNodeSearchWindow searchWindow;

        public DialogueGraphView(DialogueGraphWindow window)
        {
            this.window = window;
            style.flexGrow = 1;

            SetupZoom(ContentZoomer.DefaultMinScale, ContentZoomer.DefaultMaxScale);
            this.AddManipulator(new ContentDragger());
            this.AddManipulator(new SelectionDragger());
            this.AddManipulator(new RectangleSelector());

            var grid = new GridBackground();
            Insert(0, grid);
            grid.StretchToParentSize();

            searchWindow = ScriptableObject.CreateInstance<DialogueNodeSearchWindow>();
            searchWindow.Init(this);
            nodeCreationRequest = ctx =>
                SearchWindow.Open(new SearchWindowContext(ctx.screenMousePosition), searchWindow);

            graphViewChanged = change =>
            {
                // 开始节点是固定入口,不允许删除
                change.elementsToRemove?.RemoveAll(e => e is DialogueGraphNode n && n.Data is StartNode);
                window.NotifyGraphChanged();
                return change;
            };
        }

        public override List<Port> GetCompatiblePorts(Port startPort, NodeAdapter nodeAdapter)
        {
            return ports.ToList()
                .Where(p => p.direction != startPort.direction && p.node != startPort.node)
                .ToList();
        }

        /// <summary>在指定位置创建一个新节点(类型来自创建菜单,含用户自定义节点)。</summary>
        public DialogueGraphNode CreateNode(Type nodeType, Vector2 position)
        {
            // 开始节点全图唯一,由 PopulateFromAsset 自动创建
            if (nodeType == typeof(StartNode) && FindStartNodeView() != null)
                return null;

            var data = (DialogueNodeData)Activator.CreateInstance(nodeType);
            data.guid = GUID.Generate().ToString();
            data.position = position;
            var view = CreateNodeView(data);
            window.NotifyGraphChanged();
            return view;
        }

        DialogueGraphNode CreateNodeView(DialogueNodeData data)
        {
            var view = new DialogueGraphNode(data, this);
            view.SetPosition(new Rect(data.position, Vector2.zero));
            AddElement(view);
            return view;
        }

        /// <summary>把资产里的图加载到视图。</summary>
        public void PopulateFromAsset(DialogueGraphAsset asset)
        {
            DeleteElements(graphElements.ToList());

            foreach (var data in asset.nodes)
                if (data != null)
                    CreateNodeView(data);

            EnsureStartNode(asset);

            foreach (var link in asset.links)
            {
                var from = FindNodeView(link.fromGuid);
                var to = FindNodeView(link.toGuid);
                if (from == null || to == null || to.InputPort == null) continue;
                var outPort = from.GetOutputPort(link.fromPort);
                if (outPort == null) continue;
                AddElement(outPort.ConnectTo(to.InputPort));
            }

            RefreshAllNodes();
        }

        /// <summary>
        /// 保证图中存在唯一的开始节点。
        /// 旧资产没有开始节点时自动补一个,并把它连到原来的入口节点。
        /// </summary>
        void EnsureStartNode(DialogueGraphAsset asset)
        {
            if (FindStartNodeView() != null) return;

            var legacyEntry = asset.FindNode(asset.entryNodeGuid);
            var start = new StartNode
            {
                guid = GUID.Generate().ToString(),
                position = legacyEntry != null
                    ? legacyEntry.position + new Vector2(-260, 0)
                    : Vector2.zero
            };
            var startView = CreateNodeView(start);

            if (legacyEntry != null && !(legacyEntry is StartNode))
            {
                var to = FindNodeView(legacyEntry.guid);
                if (to?.InputPort != null)
                    AddElement(startView.GetOutputPort(0).ConnectTo(to.InputPort));
            }
            window.NotifyGraphChanged();
        }

        /// <summary>把当前视图写回资产。</summary>
        public void SaveToAsset(DialogueGraphAsset asset)
        {
            asset.nodes.Clear();
            foreach (var nodeView in nodes.ToList().Cast<DialogueGraphNode>())
            {
                nodeView.Data.position = nodeView.GetPosition().position;
                asset.nodes.Add(nodeView.Data);
            }

            asset.links.Clear();
            foreach (var edge in edges.ToList())
            {
                var from = (DialogueGraphNode)edge.output.node;
                var to = (DialogueGraphNode)edge.input.node;
                asset.links.Add(new NodeLink
                {
                    fromGuid = from.Data.guid,
                    fromPort = from.GetOutputPortIndex(edge.output),
                    toGuid = to.Data.guid
                });
            }

            // 入口固定为开始节点;没有开始节点时退化为第一个节点(兼容旧资产)
            var startGuid = FindStartNodeView()?.Data.guid;
            if (startGuid == null && asset.nodes.Count > 0)
                startGuid = asset.nodes[0].guid;
            asset.entryNodeGuid = startGuid;
        }

        public DialogueGraphNode FindNodeView(string guid) =>
            nodes.ToList().Cast<DialogueGraphNode>().FirstOrDefault(n => n.Data.guid == guid);

        public DialogueGraphNode FindStartNodeView() =>
            nodes.ToList().Cast<DialogueGraphNode>().FirstOrDefault(n => n.Data is StartNode);

        public void RefreshAllNodes()
        {
            foreach (var n in nodes.ToList().Cast<DialogueGraphNode>())
                n.RefreshNode();
        }

        public DialogueNodeData GetSelectedNodeData() =>
            selection.OfType<DialogueGraphNode>().FirstOrDefault()?.Data;

        /// <summary>把屏幕坐标转换为图内容坐标(用于新建节点定位)。</summary>
        public Vector2 GetGraphMousePosition(Vector2 screenMousePosition)
        {
            var windowMouse = screenMousePosition - window.position.position;
            return contentViewContainer.WorldToLocal(windowMouse);
        }
    }
}
