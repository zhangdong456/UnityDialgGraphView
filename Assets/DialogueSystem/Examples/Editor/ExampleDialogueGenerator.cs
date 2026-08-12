using UnityEditor;
using UnityEngine;

namespace DialogueSystem.Examples
{
    /// <summary>
    /// 一键生成两张示例对话资产,演示所有节点类型、条件与事件的配置方式。
    /// 生成后用图编辑器打开即可查看,也可以参考本文件学习如何用代码构建对话图。
    /// </summary>
    public static class ExampleDialogueGenerator
    {
        const string Folder = "Assets/DialogueSystem/Examples";
        const string MainPath = Folder + "/Example_MainDialogue.asset";
        const string ShopPath = Folder + "/Example_ShopDialogue.asset";

        [MenuItem("Tools/Dialogue System/生成示例对话资产")]
        public static void Generate()
        {
            var shop = BuildShopDialogue();
            var main = BuildMainDialogue(shop);

            SaveAsset(shop, ShopPath);
            SaveAsset(main, MainPath);

            AssetDatabase.SaveAssets();
            Selection.activeObject = main;
            Debug.Log($"[对话示例] 示例资产已生成:\n{MainPath}\n{ShopPath}\n双击打开查看,或挂到 ExampleDialogueUI 上运行。");
        }

        /// <summary>主对话:开始 → 对话 → 选择(带金币条件)→ 事件/等待/分支/跳转,最后到结束。</summary>
        static DialogueGraphAsset BuildMainDialogue(DialogueGraphAsset shop)
        {
            var asset = ScriptableObject.CreateInstance<DialogueGraphAsset>();

            var start = Node(new Vector2(-300, 0), new StartNode());

            var hello = Node(new Vector2(0, 0), new DialogueNode
            {
                speakerName = "村长",
                dialogueText = "欢迎来到村庄!有什么可以帮你的吗?"
            });

            var choicePrompt = Node(new Vector2(300, 0), new DialogueNode
            {
                speakerName = "村长",
                dialogueText = "你想做点什么?"
            });

            var choice = Node(new Vector2(600, 0), new ChoiceNode
            {
                choices =
                {
                    new ChoiceOption { choiceText = "接取任务" },
                    new ChoiceOption
                    {
                        choiceText = "去商店(需要金币 ≥ 50)",
                        conditions = { new IntCompareCondition { key = "gold", op = CompareOperator.GreaterOrEqual, value = 50 } }
                    },
                    new ChoiceOption { choiceText = "离开" }
                }
            });

            var addQuest = Node(new Vector2(600, -160), new EventNode
            {
                events = { new AddQuestEvent { questId = "dragon_quest" } }
            });

            var wait = Node(new Vector2(900, -160), new WaitNode { waitSeconds = 1.5f });

            var branch = Node(new Vector2(1200, -160), new StateBranchNode
            {
                cases =
                {
                    new BranchCase
                    {
                        label = "金币 ≥ 100",
                        conditions = { new IntCompareCondition { key = "gold", op = CompareOperator.GreaterOrEqual, value = 100 } }
                    },
                    new BranchCase
                    {
                        label = "任务已完成",
                        conditions = { new QuestStateCondition { questId = "dragon_quest", requiredStatus = QuestStatus.Completed } }
                    }
                }
            });

            var rich = Node(new Vector2(1500, -260), new DialogueNode
            {
                speakerName = "村长",
                dialogueText = "出手阔绰啊!这是给你的奖励。"
            });

            var questDone = Node(new Vector2(1500, -120), new DialogueNode
            {
                speakerName = "村长",
                dialogueText = "勇士,谢谢你完成了任务!"
            });

            var fallback = Node(new Vector2(1500, 20), new DialogueNode
            {
                speakerName = "村长",
                dialogueText = "继续加油吧。"
            });

            var jumpToShop = Node(new Vector2(600, 40), new JumpNode { targetDialogue = shop });

            var bye = Node(new Vector2(600, 200), new DialogueNode
            {
                speakerName = "村长",
                dialogueText = "慢走。"
            });

            var end = Node(new Vector2(900, 200), new EndNode());

            asset.nodes.AddRange(new DialogueNodeData[]
                { start, hello, choicePrompt, choice, addQuest, wait, branch, rich, questDone, fallback, jumpToShop, bye, end });
            asset.entryNodeGuid = start.guid;

            Link(asset, start, 0, hello);
            Link(asset, hello, 0, choicePrompt);
            Link(asset, choicePrompt, 0, choice);
            Link(asset, choice, 0, addQuest);       // 选项0:接取任务
            Link(asset, choice, 1, jumpToShop);     // 选项1:去商店(需金币≥50)
            Link(asset, choice, 2, bye);            // 选项2:离开
            Link(asset, addQuest, 0, wait);
            Link(asset, wait, 0, branch);
            Link(asset, branch, 0, rich);           // 分支0:金币≥100
            Link(asset, branch, 1, questDone);      // 分支1:任务已完成
            Link(asset, branch, 2, fallback);       // 默认出口(端口下标 = 分支数)
            Link(asset, bye, 0, end);               // 显式走到结束节点

            return asset;
        }

        /// <summary>商店对话:演示被跳转的目标资产,以及修改金币的事件。</summary>
        static DialogueGraphAsset BuildShopDialogue()
        {
            var asset = ScriptableObject.CreateInstance<DialogueGraphAsset>();

            var start = Node(new Vector2(-300, 0), new StartNode());

            var welcome = Node(new Vector2(0, 0), new DialogueNode
            {
                speakerName = "商人",
                dialogueText = "欢迎光临!有钱就是好顾客。"
            });

            var spend = Node(new Vector2(300, 0), new EventNode
            {
                events = { new SetIntEvent { key = "gold", value = 0 } }
            });

            var thanks = Node(new Vector2(600, 0), new DialogueNode
            {
                speakerName = "商人",
                dialogueText = "感谢惠顾!(示例事件已把金币清零)"
            });

            var end = Node(new Vector2(900, 0), new EndNode());

            asset.nodes.AddRange(new DialogueNodeData[] { start, welcome, spend, thanks, end });
            asset.entryNodeGuid = start.guid;

            Link(asset, start, 0, welcome);
            Link(asset, welcome, 0, spend);
            Link(asset, spend, 0, thanks);
            Link(asset, thanks, 0, end);

            return asset;
        }

        static T Node<T>(Vector2 position, T node) where T : DialogueNodeData
        {
            node.guid = GUID.Generate().ToString();
            node.position = position;
            return node;
        }

        static void Link(DialogueGraphAsset asset, DialogueNodeData from, int fromPort, DialogueNodeData to)
        {
            asset.links.Add(new NodeLink { fromGuid = from.guid, fromPort = fromPort, toGuid = to.guid });
        }

        static void SaveAsset(DialogueGraphAsset asset, string path)
        {
            var existing = AssetDatabase.LoadAssetAtPath<DialogueGraphAsset>(path);
            if (existing != null) AssetDatabase.DeleteAsset(path);
            AssetDatabase.CreateAsset(asset, path);
        }
    }
}
