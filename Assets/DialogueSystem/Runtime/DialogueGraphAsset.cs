using System;
using System.Collections.Generic;
using UnityEngine;

namespace DialogueSystem
{
    /// <summary>
    /// 两个节点之间的一条连线。
    /// fromPort 是输出端口序号:单输出节点恒为 0;选择节点为选项下标;
    /// 状态分支节点为分支下标,等于分支数量时表示"默认"出口。
    /// </summary>
    [Serializable]
    public class NodeLink
    {
        public string fromGuid;
        public int fromPort;
        public string toGuid;
    }

    /// <summary>
    /// 对话图资产。整张图的节点与连线都序列化在此资产内。
    /// 通过 Assets 右键菜单 Create → Dialogue System → Dialogue Graph 创建。
    /// </summary>
    [CreateAssetMenu(fileName = "New Dialogue", menuName = "Dialogue System/Dialogue Graph", order = 0)]
    public class DialogueGraphAsset : ScriptableObject
    {
        [HideInInspector] public string entryNodeGuid;

        [SerializeReference, HideInInspector]
        public List<DialogueNodeData> nodes = new List<DialogueNodeData>();

        [HideInInspector] public List<NodeLink> links = new List<NodeLink>();

        public DialogueNodeData FindNode(string guid)
        {
            for (int i = 0; i < nodes.Count; i++)
                if (nodes[i] != null && nodes[i].guid == guid)
                    return nodes[i];
            return null;
        }

        /// <summary>沿某个输出端口找到下一个节点,没有连线时返回 null。</summary>
        public DialogueNodeData GetNextNode(string fromGuid, int fromPort)
        {
            for (int i = 0; i < links.Count; i++)
                if (links[i].fromGuid == fromGuid && links[i].fromPort == fromPort)
                    return FindNode(links[i].toGuid);
            return null;
        }

        /// <summary>图中的 Start 节点,没有时返回 null。</summary>
        public StartNode GetStartNode()
        {
            for (int i = 0; i < nodes.Count; i++)
                if (nodes[i] is StartNode start)
                    return start;
            return null;
        }

        /// <summary>
        /// 入口节点:对话必须从 Start 节点开始。
        /// 兼容旧资产:没有 Start 节点时退化为原 entryNodeGuid,再退化为第一个节点。
        /// </summary>
        public DialogueNodeData GetEntryNode()
        {
            var start = GetStartNode();
            if (start != null) return start;
            var entry = FindNode(entryNodeGuid);
            if (entry != null) return entry;
            return nodes.Count > 0 ? nodes[0] : null;
        }
    }
}
