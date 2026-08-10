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
    /// 图上一个节点的可视化表现:负责构建端口、显示标题与摘要、刷新端口。
    /// 端口规则:
    ///   开始节点 → 无输入端口,一个输出端口
    ///   结束节点 → 无输出端口
    ///   对话/等待/事件/自定义节点 → 一个输出端口
    ///   选择节点 → 每个选项一个输出端口
    ///   状态分支节点 → 每条分支一个输出端口 + 末尾"默认"端口
    ///   跳转节点 → 无输出端口
    /// </summary>
    public class DialogueGraphNode : Node
    {
        public DialogueNodeData Data { get; }
        public Port InputPort { get; private set; }

        readonly DialogueGraphView owner;
        readonly List<Port> outputPorts = new List<Port>();
        readonly Label summaryLabel;

        public DialogueGraphNode(DialogueNodeData data, DialogueGraphView owner)
        {
            Data = data;
            this.owner = owner;
            viewDataKey = data.guid;

            // 开始节点是图的入口,没有输入端口
            if (!(data is StartNode))
            {
                InputPort = InstantiatePort(Orientation.Horizontal, Direction.Input, Port.Capacity.Multi, typeof(bool));
                InputPort.portName = "输入";
                inputContainer.Add(InputPort);
            }

            summaryLabel = new Label();
            summaryLabel.style.fontSize = 10;
            summaryLabel.style.color = new Color(0.75f, 0.75f, 0.75f);
            summaryLabel.style.whiteSpace = WhiteSpace.Normal;
            summaryLabel.style.maxWidth = 180;
            extensionContainer.Add(summaryLabel);

            RefreshNode();
        }

        public static string GetDisplayName(Type type)
        {
            if (typeof(StartNode).IsAssignableFrom(type)) return "开始";
            if (typeof(EndNode).IsAssignableFrom(type)) return "结束";
            if (typeof(DialogueNode).IsAssignableFrom(type)) return "对话节点";
            if (typeof(ChoiceNode).IsAssignableFrom(type)) return "选择节点";
            if (typeof(StateBranchNode).IsAssignableFrom(type)) return "状态分支节点";
            if (typeof(WaitNode).IsAssignableFrom(type)) return "等待节点";
            if (typeof(EventNode).IsAssignableFrom(type)) return "事件节点";
            if (typeof(JumpNode).IsAssignableFrom(type)) return "跳转节点";
            return ObjectNames.NicifyVariableName(type.Name);
        }

        List<string> GetOutputPortNames()
        {
            switch (Data)
            {
                case JumpNode _:
                case EndNode _:
                    return new List<string>();
                case ChoiceNode c:
                    return c.choices
                        .Select((ch, i) => $"选项{i}: {Truncate(ch?.choiceText, 10)}")
                        .ToList();
                case StateBranchNode b:
                {
                    var names = b.cases
                        .Select((cs, i) => $"分支{i}: {Truncate(cs?.label, 10)}")
                        .ToList();
                    names.Add("默认");
                    return names;
                }
                default:
                    // 对话/等待/事件/自定义节点:单输出
                    return new List<string> { "下一个" };
            }
        }

        /// <summary>根据当前数据重建端口、标题与摘要,尽量保留原有连线。</summary>
        public void RefreshNode()
        {
            // 记录旧输出端口上的连线,端口下标仍有效时重建后恢复
            var savedEdges = new List<Tuple<int, Port>>();
            for (int i = 0; i < outputPorts.Count; i++)
                foreach (var edge in outputPorts[i].connections.ToList())
                    savedEdges.Add(Tuple.Create(i, edge.input));
            owner.DeleteElements(outputPorts.SelectMany(p => p.connections).Cast<GraphElement>().ToList());

            outputContainer.Clear();
            outputPorts.Clear();
            foreach (var name in GetOutputPortNames())
            {
                var port = InstantiatePort(Orientation.Horizontal, Direction.Output, Port.Capacity.Single, typeof(bool));
                port.portName = name;
                outputContainer.Add(port);
                outputPorts.Add(port);
            }

            foreach (var saved in savedEdges)
            {
                if (saved.Item1 >= outputPorts.Count) continue;
                var edge = outputPorts[saved.Item1].ConnectTo(saved.Item2);
                owner.AddElement(edge);
            }

            title = (Data is StartNode ? "▶ " : "") + GetDisplayName(Data.GetType());
            summaryLabel.text = Truncate(Data.GetSummary(), 60);

            RefreshExpandedState();
            RefreshPorts();
        }

        public Port GetOutputPort(int index) =>
            index >= 0 && index < outputPorts.Count ? outputPorts[index] : null;

        public int GetOutputPortIndex(Port port) => outputPorts.IndexOf(port);

        static string Truncate(string text, int max)
        {
            if (string.IsNullOrEmpty(text)) return string.Empty;
            return text.Length <= max ? text : text.Substring(0, max) + "…";
        }
    }
}
