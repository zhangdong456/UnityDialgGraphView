# DialogueSystem — Unity NPC 对话配置工具集

基于 **Unity 2022 GraphView** 的可视化 NPC 对话编辑工具:在图编辑器里拖拽节点、连线配置对话流程,运行时通过事件驱动的播放器接入你自己的 UI。

## 功能一览

- **开始节点**:每张图唯一,由编辑器自动创建且不可删除,对话必须从这里开始
- **结束节点**:显式标记对话终点,走到这里触发 OnEnd
- **对话节点**:说话者、对话内容、可选语音
- **选择节点**:只管理多个选项,每个选项可挂任意条件(如"金币 ≥ 50 才显示"),条件可按**并/或**组合;说话者和正文请放在前置对话节点
- **状态分支节点**:按条件切出 N 条分支 + 默认出口,分支条件同样支持**并/或**组合
- **等待节点**:配置等待秒数,等待期间隐藏对话界面,用于播放人物动画
- **事件节点**:**每个事件类型是一种独立节点**(接取任务 / 完成任务 / 设置整数 / 你的自定义事件……),右键菜单"事件节点"分组展开选择;每种事件类型可自定义节点颜色(同类型全局生效);详情面板可随时更换事件类型
- **跳转节点**:跳转到另一个对话资产继续执行
- **Excel 导出**:菜单 `Tools → Dialogue System → 导出对话文本到 Excel`,选择文件夹后扫描其中所有对话资产,把对话正文与选项文本导出为 xlsx(自动剔除重复文本)
- 左侧详情面板:点击节点即可查看和编辑它的属性;对话正文会根据内容自动增高
- 条件列表使用自定义类型选择器,可直接添加 `DialogueCondition` 子类并编辑字段;事件节点直接在详情面板选择/更换 `DialogueEvent` 类型
- 编辑器提供未保存状态提示、`Ctrl/Cmd + S` 保存和“聚焦全部”视图按钮
- 条件、事件、节点类型全部通过继承基类扩展,**子类自动出现在编辑器菜单里,无需注册**

## 安装

把本仓库的 `Assets/DialogueSystem` 文件夹整个拷贝到你的 Unity 工程(2022.x)的 `Assets` 目录下即可。无第三方依赖。

## 快速上手

1. **创建对话资产**:Project 窗口右键 → `Create → Dialogue System → Dialogue Graph`
2. **打开图编辑器**:双击资产(或菜单 `Window → Dialogue System → Dialogue Graph`)
3. **搭建对话**:图加载后自动生成唯一的"开始"节点(不可删除),从它的输出端口连到第一个节点;右键空白处创建其他节点,点击节点在左侧栏编辑详情;改完点工具栏"保存"。选择节点前如需显示一句提示,先连接一个对话节点
4. **运行**:把 `ExampleDialogueUI` 挂到场景任意 GameObject 上,拖入对话资产,运行后按空格播放

也可以直接用菜单 `Tools → Dialogue System → 生成示例对话资产` 一键生成两张演示资产(覆盖所有节点类型),边看图边学习。

## 节点说明

| 节点 | 作用 | 输出端口 |
| --- | --- | --- |
| 开始节点 | 对话入口,全图唯一、不可删除 | 下一个 |
| 结束节点 | 对话终点,走到即结束 | 无 |
| 对话节点 | 一个说话者说一段话,正文完整显示 | 下一个 |
| 选择节点 | 提供多个选项,选项按条件过滤;不负责说话者和正文 | 每个选项一个端口 |
| 状态分支节点 | 自上而下判断,命中第一条满足条件的分支 | 每条分支一个端口 + 默认 |
| 等待节点 | 暂停 N 秒(期间隐藏 UI) | 下一个 |
| 事件节点 | 每个事件类型一种独立节点(在"事件节点"分组下选择);执行一个事件 | 下一个 |
| 跳转节点 | 跳到另一张对话图的入口 | 无 |

### 事件节点与类型颜色

- 右键空白处 → "事件节点"分组 → 展开后列出**所有** `DialogueEvent` 子类,每个事件类型都是一种独立节点,节点标题就是事件类型名(如"接取任务")。
- 选中事件节点后,详情面板可以**更换事件类型**(点"事件类型"下拉)、编辑事件字段,并设置**该事件类型的节点颜色**。
- 颜色按事件类型**全局生效**:给"接取任务"设定颜色后,所有接取任务节点都是这个颜色。颜色保存在本机编辑器偏好(EditorPrefs),不写入对话资产,换电脑需要重新设置。
- 未自定义颜色的类型使用**自动分配色**(按类型名哈希生成,同名类型颜色恒定);点"恢复自动"可清除自定义色。

### 条件的并/或组合

选择节点的选项和状态分支节点的分支都有“**条件组合**”下拉:

- **并(All,默认)**:所有条件都满足才生效,与旧版行为一致。
- **或(Any)**:任一条件满足即生效,列表中的空元素会被忽略。

### 导出对话文本到 Excel

菜单 `Tools → Dialogue System → 导出对话文本到 Excel`:

1. 选择一个工程 `Assets` 内的文件夹(含子文件夹扫描)。
2. 导出其中**所有**对话资产的对话节点正文 + 选择节点每个选项的文本。
3. **相同文本自动去重**(按去除首尾空白后的全文比较;空白文本跳过)。
4. 生成 xlsx(列:序号/类型/说话者/文本内容/来源资产;表头加粗、首行冻结),完成弹窗显示扫描/导出/去重统计。
5. 依赖 `Assets/Plugins/NPOI`(编辑器专用;运行时程序集零第三方依赖)。

## 扩展指南

### 自定义条件(如"金币大于多少")

```csharp
[Serializable]
public class GoldCondition : DialogueCondition
{
    public int amount;
    public override bool Evaluate(DialogueContext context)
        => context.Blackboard.GetInt("gold") >= amount;
}
```

继承 `DialogueCondition` 后,编译成功时会自动出现在选择节点某个选项的 `Conditions` 列表、状态分支节点某个分支的 `Conditions` 列表里。一个选择/分支内的多个条件默认 AND,可在"条件组合"里切换为 OR。

### 自定义事件(会自动成为一种图节点)

```csharp
[Serializable]
public class AddQuestEvent : DialogueEvent   // 内置模板,可直接参考
{
    public string questId;
    public override void Execute(DialogueContext context)
        => context.Quests.AddQuest(questId);
}
```

继承 `DialogueEvent` 后,它就会自动出现在右键菜单的"**事件节点**"分组下,成为一种独立的图节点(节点标题 = `[DialogueEditorName]` 的显示名);同时也可作为事件字段类型在详情面板中选用。内置 `AddQuestEvent` / `CompleteQuestEvent` / `SetIntEvent` 三个模板,可直接照抄改写,在 `Execute` 里对接你自己的任务/背包系统。完整的逐步配置说明见 [`Assets/DialogueSystem/使用手册.md`](Assets/DialogueSystem/使用手册.md)。

### 自定义编辑器名称和备注

自定义节点、条件或事件可以使用 `DialogueEditorName` 自定义编辑器显示名称和鼠标提示,不会再直接显示 C# 类名:

```csharp
[DialogueEditorName("增加金币", "把玩家金币增加指定数量")]
public class AddGoldEvent : DialogueEvent
{
    public int amount;
    public override string GetSummary() => $"增加金币 {amount}";
}
```

该名称会用于 GraphView 节点标题、创建节点菜单、条件类型菜单和事件类型菜单。`GetSummary()` 用于显示当前实例的具体配置摘要。

### 自定义节点

继承 `DialogueNodeData` 即可,会自动出现在图编辑器的创建菜单中(默认一个输入 + 一个输出端口,运行时默认沿 0 号端口继续)。如需自定义端口规则或运行行为,扩展 `DialogueGraphNode.GetOutputPortNames` 与 `DialoguePlayer.Process`。

## 运行时 API

```csharp
var context = new DialogueContext();
context.Blackboard.SetInt("gold", 100);      // 条件判断用的数值
context.Quests.AddQuest("dragon_quest");      // 任务状态

var player = new DialoguePlayer();
player.OnDialogue += (speaker, text, cont) => { /* 显示对话,点继续时调 cont() */ };
player.OnChoice   += (choices, cb) => { /* 显示选项,选完调 cb(choiceIndex) */ };
player.OnWait     += (seconds, cont) => { /* 隐藏 UI,seconds 秒后调 cont() */ };
player.OnJump     += asset => { /* 即将跳转到另一张图(随后自动继续) */ };
player.OnEnd      += () => { /* 对话结束 */ };
player.Play(dialogueAsset, context);
```

完整可运行的示例见 `Assets/DialogueSystem/Examples/ExampleDialogueUI.cs`。

## 目录结构

```
Assets/DialogueSystem/
├── Runtime/            # 运行时(随构建打包)
│   ├── DialogueGraphAsset.cs   # 对话图资产(节点+连线)
│   ├── DialogueNodeData.cs     # 节点基类
│   ├── Nodes/                  # 八种内置节点(开始/结束/对话/选择/分支/等待/单事件/跳转)
│   ├── Conditions/             # 条件基类 + 内置条件 + 并/或组合枚举
│   ├── Events/                 # 事件基类 + 内置事件模板(每种自动成为图节点)
│   ├── DialogueContext.cs      # 运行上下文(黑板 + 任务记录)
│   └── DialoguePlayer.cs       # 事件驱动的对话播放器
├── Editor/             # 图编辑器(GraphView,含事件类型颜色存储)
└── Examples/           # 示例 UI + 示例资产生成器
```

## 注意事项

- 对话图**不会自动保存**,关闭窗口时若提示保存请选择保存,或随时点工具栏"保存"。
- 选择节点若运行时没有任何选项满足条件,对话直接结束(默认出口请用状态分支节点实现)。
- 状态分支节点中"条件为空的分支"永不命中,兜底请连"默认"端口。
- 节点/条件/事件均基于 `[SerializeReference]` 序列化:**重命名类名或命名空间会导致已配置的数据丢失**,改名请使用 `UnityEngine.Scripting.APIUpdating.MovedFromAttribute`。
