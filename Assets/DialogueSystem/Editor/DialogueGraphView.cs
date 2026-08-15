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
        bool isPopulating;

        // 复制粘贴(Ctrl+C/V)与 Ctrl+S 保存的内部状态
        DialogueNodeData clipboard;      // 上次复制出的节点深拷贝(独立于源节点)
        Vector2 lastMouseGraphPos;       // 最近一次鼠标在图内容坐标中的位置(粘贴落点)

        public DialogueGraphView(DialogueGraphWindow window)
        {
            this.window = window;
            style.flexGrow = 1;
            style.backgroundColor = new Color(0.075f, 0.085f, 0.11f);

            // 允许更远的总览,同时允许更近的细看,方便阅读节点正文与端口名称。
            SetupZoom(0.08f, 3.0f);
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

            // 记录鼠标位置(面板坐标 → 图内容坐标,与节点 position 同空间),供 Ctrl+V 粘贴定位
            RegisterCallback<MouseMoveEvent>(evt =>
                lastMouseGraphPos = contentViewContainer.WorldToLocal(evt.mousePosition), TrickleDown.TrickleDown);

            // TrickleDown 阶段拦截快捷键:先于 GraphView 内建键盘处理(如 Delete),
            // 且不会在节点标题输入等场景误触(当前节点无内联文本输入)。
            RegisterCallback<KeyDownEvent>(OnGraphKeyDown, TrickleDown.TrickleDown);

            graphViewChanged = change =>
            {
                // 开始节点是固定入口,不允许删除
                change.elementsToRemove?.RemoveAll(e => e is DialogueGraphNode n && n.Data is StartNode);
                if (!isPopulating)
                    window.NotifyGraphChanged();
                return change;
            };
        }

        /// <summary>GraphView 内快捷键:Ctrl/Cmd+S 保存,Ctrl/Cmd+C 复制选中节点,Ctrl/Cmd+V 粘贴到鼠标处。</summary>
        void OnGraphKeyDown(KeyDownEvent evt)
        {
            bool ctrl = evt.ctrlKey || evt.commandKey;
            if (!ctrl) return;

            switch (evt.keyCode)
            {
                case KeyCode.S:
                    window.Save();
                    evt.StopImmediatePropagation();
                    break;
                case KeyCode.C:
                    CopySelectedNode();
                    evt.StopImmediatePropagation();
                    break;
                case KeyCode.V:
                    PasteClipboardAtMouse();
                    evt.StopImmediatePropagation();
                    break;
            }
        }

        /// <summary>
        /// 复制当前选中的节点(深拷贝,含事件/条件/所有字段)。
        /// 开始节点全图唯一,禁止复制;多选时取第一个可复制的节点。
        /// </summary>
        public void CopySelectedNode()
        {
            foreach (var element in selection)
            {
                if (!(element is DialogueGraphNode nodeView)) continue;
                if (nodeView.Data is StartNode)
                {
                    Debug.LogWarning("[DialogueSystem] 开始节点是全图唯一入口,无法复制。");
                    return;
                }
                clipboard = DialogueNodeCloneUtil.Clone(nodeView.Data);
                return; // 一次只复制一个
            }
        }

        /// <summary>把剪贴板节点粘贴到鼠标最后所在位置;剪贴板为空或其中的开始节点引用失效时不粘贴。</summary>
        public void PasteClipboardAtMouse()
        {
            if (clipboard == null) return;
            if (clipboard is StartNode) return; // 防御:开始节点永不粘贴

            var data = DialogueNodeCloneUtil.Clone(clipboard); // 再次克隆,支持多次粘贴互不影响
            data.position = lastMouseGraphPos;
            CreateNodeView(data);
            window.NotifyGraphChanged();
        }

        public override List<Port> GetCompatiblePorts(Port startPort, NodeAdapter nodeAdapter)
        {
            return ports.ToList()
                .Where(p => p.direction != startPort.direction && p.node != startPort.node)
                .Where(p => p.capacity != Port.Capacity.Single || p.connections.Count() == 0)
                .ToList();
        }

        /// <summary>在指定位置创建一个新节点(类型来自创建菜单,含用户自定义节点)。</summary>
        public DialogueGraphNode CreateNode(Type nodeType, Vector2 position)
        {
            // 开始节点全图唯一,由 PopulateFromAsset 自动创建
            if (nodeType == typeof(StartNode) && FindStartNodeView() != null)
                return null;

            DialogueNodeData data;
            try
            {
                data = (DialogueNodeData)Activator.CreateInstance(nodeType);
            }
            catch (Exception e)
            {
                Debug.LogError($"[DialogueSystem] 无法创建节点类型 {nodeType.Name},请确保它有无参构造函数。\n{e}");
                return null;
            }
            data.guid = GUID.Generate().ToString();
            data.position = position;
            var view = CreateNodeView(data);
            window.NotifyGraphChanged();
            return view;
        }

        /// <summary>
        /// 在指定位置创建一个单事件节点,并让它直接持有指定类型的事件实例。
        /// 右键菜单"事件节点"分组里的每个事件类型都走这里。
        /// </summary>
        public DialogueGraphNode CreateSingleEventNode(Type eventType, Vector2 position)
        {
            if (eventType == null || !typeof(DialogueEvent).IsAssignableFrom(eventType))
            {
                // 空白事件节点:不选类型,创建后再到详情面板选择
                return CreateNode(typeof(SingleEventNode), position);
            }

            var view = CreateNode(typeof(SingleEventNode), position);
            if (view != null)
            {
                try
                {
                    ((SingleEventNode)view.Data).eventData = (DialogueEvent)Activator.CreateInstance(eventType);
                    view.RefreshNode();
                }
                catch (Exception e)
                {
                    Debug.LogError($"[DialogueSystem] 无法创建事件类型 {eventType.Name},请确保它有无参构造函数。\n{e}");
                }
            }
            return view;
        }

        DialogueGraphNode CreateNodeView(DialogueNodeData data)
        {
            var view = new DialogueGraphNode(data, this);
            view.SetPosition(new Rect(data.position, Vector2.zero));
            AddElement(view);
            return view;
        }

        public void ClearView()
        {
            isPopulating = true;
            try
            {
                DeleteElements(graphElements.ToList());
            }
            finally
            {
                isPopulating = false;
            }
        }

        /// <summary>把资产里的图加载到视图。</summary>
        public void PopulateFromAsset(DialogueGraphAsset asset)
        {
            if (asset == null) return;

            isPopulating = true;
            try
            {
                DeleteElements(graphElements.ToList());

                if (asset.nodes != null)
                    foreach (var data in asset.nodes)
                        if (data != null)
                            CreateNodeView(data);

                var addedStart = EnsureStartNode(asset);

                if (asset.links != null)
                    foreach (var link in asset.links)
                    {
                        if (link == null) continue;
                        var from = FindNodeView(link.fromGuid);
                        var to = FindNodeView(link.toGuid);
                        if (from == null || to == null || to.InputPort == null) continue;
                        var outPort = from.GetOutputPort(link.fromPort);
                        if (outPort == null) continue;
                        AddElement(outPort.ConnectTo(to.InputPort));
                    }

                RefreshAllNodes();

                // 旧资产自动补 Start 节点,需要用户明确保存才写回资产。
                if (addedStart)
                    window.NotifyGraphChanged();
            }
            finally
            {
                isPopulating = false;
            }
        }

        /// <summary>
        /// 保证图中存在唯一的开始节点。
        /// 旧资产没有开始节点时自动补一个,并把它连到原来的入口节点。
        /// </summary>
        bool EnsureStartNode(DialogueGraphAsset asset)
        {
            if (FindStartNodeView() != null) return false;

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
            return true;
        }

        /// <summary>把当前视图写回资产。</summary>
        public void SaveToAsset(DialogueGraphAsset asset)
        {
            if (asset == null) return;
            if (asset.nodes == null) asset.nodes = new List<DialogueNodeData>();
            if (asset.links == null) asset.links = new List<NodeLink>();
            asset.nodes.Clear();
            foreach (var nodeView in nodes.ToList().Cast<DialogueGraphNode>())
            {
                nodeView.Data.position = nodeView.GetPosition().position;
                asset.nodes.Add(nodeView.Data);
            }

            asset.links.Clear();
            foreach (var edge in edges.ToList())
            {
                var from = edge.output?.node as DialogueGraphNode;
                var to = edge.input?.node as DialogueGraphNode;
                if (from == null || to == null) continue;
                var fromPort = from.GetOutputPortIndex(edge.output);
                if (fromPort < 0) continue;
                asset.links.Add(new NodeLink
                {
                    fromGuid = from.Data.guid,
                    fromPort = fromPort,
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
