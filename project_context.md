# project_context.md

## 技术栈

- 引擎:Unity 2022.x(编辑器扩展基于 `UnityEditor.Experimental.GraphView` + UIElements/IMGUI)
- 语言:C#(无第三方依赖,无 asmdef)
- 序列化:`ScriptableObject` 资产 + `[SerializeReference]` 多态序列化节点/条件/事件
- 构建:无需构建,把 `Assets/DialogueSystem` 拷入 Unity 工程的 `Assets` 即可

## 目录结构与职责

```
Assets/DialogueSystem/
├── Runtime/                      # 运行时程序集
│   ├── DialogueGraphAsset.cs     # 对话图资产;内含 NodeLink(fromGuid/fromPort/toGuid)
│   ├── DialogueNodeData.cs       # 节点基类(guid、position、GetSummary)
│   ├── DialogueEditorMetadata.cs # 编辑器显示名称/备注特性与类型元数据读取
│   ├── Nodes/                    # StartNode(唯一入口)/ EndNode / DialogueNode / ChoiceNode(ChoiceOption)
│   │                             #   / StateBranchNode(BranchCase) / WaitNode / SingleEventNode / JumpNode
│   ├── Conditions/               # DialogueCondition 基类 + IntCompareCondition / BoolFlagCondition / QuestStateCondition
│   │                             #   + ConditionCombineMode(条件并/或组合方式)
│   ├── Events/                   # DialogueEvent 基类 + AddQuestEvent / CompleteQuestEvent / SetIntEvent(模板)
│   ├── DialogueContext.cs        # DialogueContext(Blackboard + QuestLog)、QuestStatus
│   └── DialoguePlayer.cs         # 事件驱动播放器(OnDialogue/OnChoice/OnWait/OnJump/OnEnd)
├── Editor/
│   ├── DialogueGraphWindow.cs    # 编辑器窗口:工具栏 + 左侧节点详情面板 + 右侧 GraphView
│   ├── DialogueGraphView.cs      # 图的加载/保存/节点创建/开始节点管理(自动创建、防删除、唯一)
│   ├── DialogueGraphNode.cs      # 节点视图:端口构建与刷新、标题与摘要显示
│   ├── DialogueNodeSearchWindow.cs # 创建节点菜单(TypeCache 自动收集子类;"事件节点"分组列出所有 DialogueEvent 子类)
│   ├── DialogueEventColorStore.cs # 事件类型节点颜色的全局存储(EditorPrefs + 类型名哈希自动分配色)
│   ├── DialogueTextExporter.cs  # Tools 菜单:导出文件夹内全部对话资产的文本到 Excel(NPOI,自动去重)
│   └── DialogueGraphAssetEditor.cs # 资产 Inspector 的"打开图编辑器"按钮
└── Examples/
    ├── ExampleDialogueUI.cs      # OnGUI 最小可运行示例(挂 GameObject 按空格播放)
    └── Editor/ExampleDialogueGenerator.cs # 菜单 Tools→Dialogue System→生成示例对话资产
```

## 关键约定

- 命名空间:运行时 `DialogueSystem`,编辑器 `DialogueSystem.Editor`,示例 `DialogueSystem.Examples`
- 入口固定为 StartNode:全图唯一、编辑器自动创建、不可删除、不出现在创建菜单;`DialogueGraphAsset.GetEntryNode()` 优先返回 StartNode,旧资产无 StartNode 时退化为原 `entryNodeGuid`(打开图时会自动补一个 StartNode 并连到原入口)
- **事件节点模型(2026-08-15 重构)**:每个 `DialogueEvent` 子类都是一种独立的图节点(`SingleEventNode` 容器,内含一个 `[SerializeReference] DialogueEvent eventData`)。右键创建菜单的"事件节点"是分组,展开列出所有事件子类 + 空白事件节点;详情面板可随时更换事件类型。原多事件列表节点 `EventNode` 已删除(无旧资产需要兼容)。事件节点标题显示事件类型名(如"接取任务")
- **事件类型颜色**:按事件类型全局自定义,存储在 `EditorPrefs`(键前缀 `DialogueSystem.EventNodeColor.`,Editor/DialogueEventColorStore.cs),不写入对话资产。给"接取任务"设色后,所有接取任务节点都是该颜色;未自定义的类型用类型全名 FNV-1a 哈希生成的稳定自动色(同名类型颜色恒定)。换电脑需重新设置
- **条件并/或**:`ChoiceOption` 与 `BranchCase` 均有 `conditionMode`(`ConditionCombineMode.All`=并/`Any`=或),默认 All 与旧行为一致。或模式忽略列表中的空元素;空条件列表仍表示"无条件显示"(选项)/"永不命中"(分支,兜底走默认端口)
- 端口序号约定:单输出节点恒为 0;选择节点 = 选项下标;分支节点 = 分支下标,`cases.Count` 为"默认"出口
- `ChoiceNode` 只保存 `ChoiceOption` 列表;说话者和正文归 `DialogueNode`,运行时 `DialoguePlayer.OnChoice` 签名为 `(choices, callback)`
- 自定义节点/条件/事件可使用 `[DialogueEditorName("显示名称", "备注")]`;编辑器通过 `DialogueTypeMetadata` 读取名称和描述,`GetSummary()` 显示实例摘要
- 扩展方式:继承 `DialogueNodeData` / `DialogueCondition` / `DialogueEvent`,TypeCache 与 SerializeReference 选择器自动发现,无需注册
- 状态读写统一走 `DialogueContext`(`Blackboard.SetInt/GetInt`、`Quests.AddQuest/CompleteQuest/GetStatus`)
- 编辑器保存是手动的:工具栏"保存"按钮或关窗提示;`hasUnsavedChanges` 标记脏状态
- 注释与 UI 文案使用中文;类/方法命名英文 PascalCase

## 已完成功能清单

- 对话资产与图数据结构(nodes/links/入口) — `Runtime/DialogueGraphAsset.cs`
- 八种内置节点(开始/结束/对话/选择/状态分支/等待/单事件/跳转) — `Runtime/Nodes/*.cs`
- 事件节点 = 每个 DialogueEvent 子类一种独立节点(SingleEventNode),详情面板可更换类型,事件分组创建菜单 — `Runtime/Nodes/SingleEventNode.cs` + `Editor/DialogueNodeSearchWindow.cs`
- 事件类型全局颜色自定义(EditorPrefs 存储 + 哈希自动分配色 + "恢复自动"按钮) — `Editor/DialogueEventColorStore.cs` + `DialogueGraphWindow.DrawEventColorField`
- Tools→Dialogue System→导出对话文本到 Excel(选文件夹扫描全部 Dialogue Graph 资产,导出对话正文+选项文本,按全文去重,NPOI 生成 xlsx) — `Editor/DialogueTextExporter.cs`(依赖 `Assets/Plugins/NPOI` 2.1.1,仅编辑器;Runtime 零第三方依赖)
- 条件并/或组合(选项条件与分支条件均可选 All/Any,默认 All) — `Runtime/Conditions/ConditionCombineMode.cs`
- 条件系统(选项显示条件、分支条件) — `Runtime/Conditions/*.cs`
- 事件系统(含加任务/完成任务/改数值模板) — `Runtime/Events/*.cs`
- 运行上下文(黑板 + 任务记录) — `Runtime/DialogueContext.cs`
- 对话播放器(交互节点等回调,自动节点立即执行,带死循环保护) — `Runtime/DialoguePlayer.cs`
- GraphView 编辑器(创建/连线/删除/保存/开始节点管理/左侧详情面板/自适应正文/节点样式/多态列表选择器) — `Editor/*.cs`
- 示例 UI 与示例资产生成器 — `Examples/`
- 使用文档 — `README.md` + `Assets/DialogueSystem/使用手册.md`

## 待办与下一步

- 已在真实 Unity 2022 编辑器中验证:编译通过、节点详情可编辑、Start/End 流程可用
- 可选增强:对话文本本地化、节点复制粘贴、小地图、示例场景

## 已知坑点与注意事项

- `[SerializeReference]` 数据在重命名类/命名空间后会丢失引用,需 `MovedFromAttribute` 过渡
- 状态分支节点中"空条件分支"永不命中,兜底必须连"默认"端口
- 选择节点只保存选项与条件;运行时若无选项满足条件,对话直接结束
- `DialoguePlayer` 的对话/选择/等待回调采用一次性会话校验,旧 UI 回调或重复点击不会推进新对话
- GraphView 输入端口支持多连接,允许多个分支汇合到同一个节点;输出端口仍按节点规则限制
- 左侧面板(节点详情)用 `IMGUIContainer` 绘制;普通字段使用 `EditorGUILayout.PropertyField`,Choice/Branch/Event 的多态列表使用自定义 `[SerializeReference]` 列表绘制器,避免 Unity 2022 默认控件出现“E”和不可点击的 Element 0/1
- 枚举节点字段不能用 `SerializedProperty.NextVisible`——对 SerializeReference 数组元素它枚举不出任何可见子属性;正确做法是反射节点对象的字段(跳过 `[HideInInspector]`/未序列化字段),再 `FindPropertyRelative(字段名)` 取属性绘制
- 详情面板用的 `SerializedObject` 存为窗口成员变量,切换节点时 Dispose 重建;每次重绘时 `Update()`/`ApplyModifiedProperties()`,修改后标脏资产并刷新图节点
- GraphView 不位于窗口左上角时(如 SplitView 右侧),框选矩形与鼠标有偏移——Unity 官方已知问题且不修复,变通方案是给 GraphView 套一层普通 `VisualElement` 父容器
- 用 `[SerializeReference]` 的文件必须 `using UnityEngine;`;`[OnOpenAsset]` 在 `UnityEditor.Callbacks` 命名空间
- 图编辑器改动需手动保存;节点增删/移动只改内存,点"保存"才写入资产;新建节点保存前详情面板会提示"请先保存"
- 离线验证:根目录 `verify.sh` 一键脚本(`bash verify.sh [Unity根目录]`,不传参自动探测常见安装位置)——① csc 编译 Runtime/Editor/Examples(引用真实 `Data\Managed\UnityEngine\*.dll`,Editor 额外引用 `Assets/Plugins/NPOI` 5 个 DLL);② 最小 UnityEngine 桩 + 真实 Runtime 源码编译执行 17 条逻辑断言(条件并/或、单事件节点播放链路);③ NPOI xlsx 往返冒烟(生成→读回断言 sheet/表头/中文换行/加粗)。临时产物在系统 Temp 的 `hermes-verify-*` 目录生成并 trap 自动清理。注意:mono/csc 是 Windows 程序,传给它的路径必须 `cygpath -m` 转 Windows 混合格式;MSYS `/tmp` 不可直接用。`ScriptableObject.CreateInstance`/`Object==` 是 internal call,mono 下必须 stub
- 事件类型颜色存 EditorPrefs(本机偏好),不随对话资产迁移;新机器上未自定义类型自动回落到哈希自动色
- IMGUI 拾色器(EditorGUILayout.ColorField)点击瞬间会抛 `ExitGUIException` 中断当帧 GUI,这是正常控制流;`DrawNodeInspector` 的 catch 已单独放行(`catch (ExitGUIException) { throw; }`),不要再吞掉它,否则报错且 GUI 状态错乱
- NPOI 是 2.1.1 旧版 API:加粗用 `IFont.Boldweight = (short)FontBoldWeight.Bold`(2.5+ 才有 `IsBold`);升级 NPOI 时 DialogueTextExporter 与 verify.sh 第③步需同步改

生成时间:2026-08-10(最后更新:2026-08-15 事件节点重构 + 条件并/或 + Excel 导出 + 拾色器 ExitGUIException 修复)
