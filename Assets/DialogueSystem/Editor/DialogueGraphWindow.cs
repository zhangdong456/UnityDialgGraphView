using System;
using System.Collections.Generic;
using System.Reflection;
using System.Linq;
using UnityEditor;
using UnityEditor.Callbacks;
using UnityEditor.UIElements;
using UnityEngine;
using UnityEngine.UIElements;

namespace DialogueSystem.Editor
{
    /// <summary>
    /// 对话图编辑器窗口。
    /// 左侧为节点详情面板(选中节点后显示/编辑其属性),
    /// 右侧为 GraphView。打开方式:
    ///   - 双击 Dialogue Graph 资产
    ///   - 菜单 Window → Dialogue System → Dialogue Graph
    ///   - 资产 Inspector 上的"在图编辑器中打开"按钮
    /// </summary>
    public class DialogueGraphWindow : EditorWindow
    {
        DialogueGraphAsset asset;
        DialogueGraphView graphView;
        ScrollView inspectorPanel;
        ObjectField assetField;
        Label statusLabel;
        string inspectedGuid;
        bool keyHandlerRegistered;
        // 详情面板当前绑定的 SerializedObject。
        // 必须存为成员变量:若是局部变量,GC 回收后原生对象被销毁,
        // 绑定它的 PropertyField 会全部渲染为空白。
        SerializedObject inspectedSo;

        [MenuItem("Window/Dialogue System/Dialogue Graph")]
        public static DialogueGraphWindow OpenWindow()
        {
            var window = GetWindow<DialogueGraphWindow>();
            window.titleContent = new GUIContent("Dialogue Graph");
            return window;
        }

        [OnOpenAsset]
        static bool OnOpenAsset(int instanceId, int line)
        {
            // Unity 6 起 InstanceIDToObject 被标记为"过时=报错"(CS0619),必须改用 EntityIdToObject;
            // 2022 又没有新 API,因此按版本条件编译,保证两个版本都能编译通过。
            // EntityId.FromULong 编码与 int 隐式转换不同(实测结果不相等),不能用;
            // int→EntityId 隐式转换只带"未来移除"警告,是当前正确做法,故局部屏蔽 CS0618。
#if UNITY_6000_0_OR_NEWER
#pragma warning disable CS0618
            var target = EditorUtility.EntityIdToObject(instanceId) as DialogueGraphAsset;
#pragma warning restore CS0618
#else
            var target = EditorUtility.InstanceIDToObject(instanceId) as DialogueGraphAsset;
#endif
            if (target == null) return false;
            OpenWith(target);
            return true;
        }

        public static void OpenWith(DialogueGraphAsset target)
        {
            var window = OpenWindow();
            window.Load(target);
        }

        void OnEnable()
        {
            saveChangesMessage = "对话图有未保存的修改,是否保存?";
            ConstructUI();
            if (asset != null) Populate();
        }

        void ConstructUI()
        {
            rootVisualElement.Clear();
            rootVisualElement.style.backgroundColor = new Color(0.055f, 0.06f, 0.08f);

            var toolbar = new Toolbar();
            assetField = new ObjectField("对话资产") { objectType = typeof(DialogueGraphAsset) };
            assetField.SetValueWithoutNotify(asset);
            assetField.RegisterValueChangedCallback(e => Load(e.newValue as DialogueGraphAsset));
            assetField.style.minWidth = 280;
            assetField.style.maxWidth = 420;
            toolbar.Add(assetField);
            toolbar.Add(new ToolbarSpacer { flex = true });
            var saveButton = new ToolbarButton(Save) { text = "保存" };
            saveButton.tooltip = "保存当前对话图 (Ctrl/Cmd + S)";
            toolbar.Add(saveButton);
            var frameButton = new ToolbarButton(FrameAll) { text = "聚焦全部" };
            frameButton.tooltip = "让右侧图视图显示全部节点";
            toolbar.Add(frameButton);
            statusLabel = new Label();
            statusLabel.style.marginLeft = 8;
            statusLabel.style.marginRight = 8;
            statusLabel.style.color = new Color(0.65f, 0.7f, 0.78f);
            toolbar.Add(statusLabel);
            rootVisualElement.Add(toolbar);

            var split = new TwoPaneSplitView(0, 300, TwoPaneSplitViewOrientation.Horizontal);
            split.style.flexGrow = 1;

            inspectorPanel = new ScrollView();
            inspectorPanel.style.minWidth = 220;
            inspectorPanel.style.backgroundColor = new Color(0.085f, 0.095f, 0.125f);
            split.Add(inspectorPanel);

            graphView = new DialogueGraphView(this);

            // Unity 官方已知问题:GraphView 不位于窗口左上角时(如放在 SplitView 右侧),
            // 框选矩形会和鼠标产生偏移。变通方案:给 GraphView 套一层普通 VisualElement 父容器。
            var graphContainer = new VisualElement();
            graphContainer.style.flexGrow = 1;
            graphContainer.Add(graphView);
            split.Add(graphContainer);

            rootVisualElement.Add(split);
            if (!keyHandlerRegistered)
            {
                rootVisualElement.RegisterCallback<KeyDownEvent>(OnRootKeyDown);
                keyHandlerRegistered = true;
            }
            UpdateToolbarStatus();
            RebuildInspector();
        }

        void OnRootKeyDown(KeyDownEvent evt)
        {
            if (evt.keyCode != KeyCode.S || (!evt.ctrlKey && !evt.commandKey)) return;
            Save();
            evt.StopPropagation();
        }

        void Load(DialogueGraphAsset newAsset)
        {
            if (asset == newAsset)
            {
                assetField?.SetValueWithoutNotify(asset);
                return;
            }

            if (hasUnsavedChanges && asset != null)
            {
                var result = EditorUtility.DisplayDialogComplex(
                    "对话图有未保存修改",
                    $"是否先保存 {asset.name} 的修改?",
                    "保存", "放弃", "取消");
                if (result == 0)
                    Save();
                else if (result == 2)
                {
                    assetField?.SetValueWithoutNotify(asset);
                    return;
                }
            }

            asset = newAsset;
            assetField?.SetValueWithoutNotify(asset);
            hasUnsavedChanges = false;
            if (asset != null && graphView != null) Populate();
            else graphView?.ClearView();
            titleContent = new GUIContent(asset == null ? "Dialogue Graph" : $"Dialogue Graph - {asset.name}");
            UpdateToolbarStatus();
            RebuildInspector();
        }

        void Populate() => graphView.PopulateFromAsset(asset);

        public void Save()
        {
            if (asset == null || graphView == null) return;
            graphView.SaveToAsset(asset);
            EditorUtility.SetDirty(asset);
            AssetDatabase.SaveAssetIfDirty(asset);
            hasUnsavedChanges = false;
            UpdateToolbarStatus();
            // 刚创建而未入资产的节点现在可以正常显示详情了
            RebuildInspector();
        }

        void OnDisable()
        {
            inspectedSo?.Dispose();
            inspectedSo = null;
        }

        public override void SaveChanges()
        {
            Save();
            base.SaveChanges();
        }

        public void NotifyGraphChanged()
        {
            hasUnsavedChanges = true;
            UpdateToolbarStatus();
        }

        void FrameAll()
        {
            graphView?.FrameAll();
        }

        void Update()
        {
            if (graphView == null) return;
            var guid = graphView.GetSelectedNodeData()?.guid;
            if (guid != inspectedGuid) RebuildInspector();
            UpdateToolbarStatus();
        }

        void UpdateToolbarStatus()
        {
            if (statusLabel == null) return;
            statusLabel.text = asset == null
                ? "未选择资产"
                : (hasUnsavedChanges ? "● 有未保存修改" : "✓ 已保存");
            statusLabel.style.color = hasUnsavedChanges
                ? new Color(1f, 0.72f, 0.32f)
                : new Color(0.45f, 0.82f, 0.58f);
        }

        void RebuildInspector()
        {
            if (inspectorPanel == null) return;
            inspectorPanel.Clear();
            var newGuid = graphView?.GetSelectedNodeData()?.guid;
            if (newGuid != inspectedGuid) foldStates.Clear(); // 切换节点时丢弃上一个节点的折叠记忆
            inspectedGuid = newGuid;

            // 释放上一次绑定的 SerializedObject,避免泄漏原生对象
            inspectedSo?.Dispose();
            inspectedSo = null;

            var header = new Label("节点详情");
            header.style.unityFontStyleAndWeight = FontStyle.Bold;
            header.style.fontSize = 13;
            header.style.paddingTop = 6;
            header.style.paddingLeft = 6;
            header.style.paddingBottom = 4;
            header.style.color = new Color(0.9f, 0.93f, 0.98f);
            inspectorPanel.Add(header);

            if (asset == null)
            {
                inspectorPanel.Add(MakeHint("在工具栏选择,或双击 Project 窗口中的 Dialogue Graph 资产开始编辑。"));
                return;
            }
            if (inspectedGuid == null)
            {
                inspectorPanel.Add(MakeHint("点击图中的一个节点,在这里查看并编辑它的详情。\n对话从\"开始\"节点出发,沿连线依次执行。"));
                return;
            }

            var data = graphView.GetSelectedNodeData();
            if (data == null)
            {
                inspectorPanel.Add(MakeHint("节点数据已被删除,请重新选择一个节点。"));
                return;
            }
            var typeLabel = new Label(DialogueGraphNode.GetDisplayName(data.GetType()));
            typeLabel.style.paddingLeft = 6;
            typeLabel.style.paddingBottom = 6;
            typeLabel.style.color = new Color(0.45f, 0.75f, 1f);
            inspectorPanel.Add(typeLabel);
            if (data is StartNode)
            {
                inspectorPanel.Add(MakeHint("开始节点:对话从这里开始,全图唯一,不可删除。\n把它的输出端口连到第一个要执行的节点。"));
                return;
            }
            if (data is EndNode)
            {
                inspectorPanel.Add(MakeHint("结束节点:对话走到这里结束,无需配置。"));
                return;
            }

            inspectedSo = new SerializedObject(asset);
            var nodeProp = FindNodeProperty(inspectedSo, inspectedGuid);
            if (nodeProp == null)
            {
                inspectorPanel.Add(MakeHint("该节点还未保存进资产,请先点工具栏\"保存\",再编辑它的详情。"));
                return;
            }

            // 用 IMGUIContainer + EditorGUILayout.PropertyField 逐字段绘制普通字段。
            // Unity 2022 对 SerializeReference 数组元素的默认 PropertyField 存在兼容问题,
            // 条件/事件列表已经在 DrawNodeInspector 中走自定义绘制路径。
            string guid = inspectedGuid;
            var inspectorGui = new IMGUIContainer(() => DrawNodeInspector(guid));
            inspectorPanel.Add(inspectorGui);
        }

        /// <summary>IMGUI 绘制选中节点的所有可见字段(说话者、内容、选项/分支/事件列表等)。</summary>
        void DrawNodeInspector(string guid)
        {
            if (inspectedSo == null || asset == null) return;
            try
            {
                inspectedSo.Update();

                var nodeProp = FindNodeProperty(inspectedSo, guid);
                if (nodeProp == null)
                {
                    EditorGUILayout.LabelField("节点数据未找到,请先保存。");
                    return;
                }

                var data = graphView.FindNodeView(guid)?.Data;
                if (data == null) return;

                // 这些列表不能直接交给 PropertyField:
                // Unity 2022 对 SerializeReference 列表的默认绘制会出现“E”、
                // Element 0/1 不可点击、类型选择器无法弹出等问题。
                // 先使用自己的列表绘制器,再绘制普通字段。
                var customFields = new HashSet<string>();
                var hasCustomFields = false;
                if (data is ChoiceNode)
                {
                    DrawChoiceOptions(nodeProp.FindPropertyRelative("choices"), guid);
                    customFields.Add("choices");
                    hasCustomFields = true;
                }
                else if (data is SingleEventNode)
                {
                    DrawSingleEventInspector(nodeProp, (SingleEventNode)data);
                    customFields.Add("eventData");
                    hasCustomFields = true;
                }
                else if (data is StateBranchNode)
                {
                    DrawBranchCases(nodeProp.FindPropertyRelative("cases"), guid);
                    customFields.Add("cases");
                    hasCustomFields = true;
                }
                else if (data is RandomBranchNode)
                {
                    DrawBranchCases(nodeProp.FindPropertyRelative("cases"), guid);
                    customFields.Add("cases");
                    hasCustomFields = true;
                }

                // 注意:不要用 SerializedProperty.NextVisible 枚举 SerializeReference 数组元素
                // 的子字段——实测它一个可见子属性都枚举不出来(元素属性的怪癖)。
                // 改为直接反射节点对象的字段,再按字段名取序列化属性绘制。
                int drawn = hasCustomFields ? 1 : 0;
                for (var t = data.GetType(); t != null && t != typeof(object); t = t.BaseType)
                {
                    foreach (var field in t.GetFields(BindingFlags.Instance | BindingFlags.Public
                        | BindingFlags.NonPublic | BindingFlags.DeclaredOnly))
                    {
                        if (customFields.Contains(field.Name)) continue;
                        if (field.IsNotSerialized) continue;
                        if (field.GetCustomAttribute<HideInInspector>() != null) continue;
                        if (!field.IsPublic && field.GetCustomAttribute<SerializeField>() == null) continue;

                        var prop = nodeProp.FindPropertyRelative(field.Name);
                        if (prop == null) continue;

                        if (data is DialogueNode && field.Name == nameof(DialogueNode.dialogueText))
                            DrawAdaptiveTextArea(prop);
                        else
                            EditorGUILayout.PropertyField(prop, true);
                        drawn++;
                    }
                }
                if (drawn == 0)
                    EditorGUILayout.LabelField("该节点没有可编辑的字段。");

                // 有修改时写回资产、标脏并刷新图上的端口与摘要
                if (inspectedSo.ApplyModifiedProperties())
                {
                    EditorUtility.SetDirty(asset);
                    NotifyGraphChanged();
                    graphView?.RefreshAllNodes();
                }
            }
            catch (ExitGUIException)
            {
                // ExitGUIException 是 IMGUI 的正常控制流:拾色器/上下文菜单等控件
                // 在状态切换瞬间由 GUIUtility.ExitGUI 抛出,用于中断当帧 GUI。
                // 必须继续上抛交给 Unity 处理;吞掉它会报错并造成 GUI 状态错乱。
                throw;
            }
            catch (Exception e)
            {
                Debug.LogException(e);
            }
        }

        void DrawChoiceOptions(SerializedProperty list, string guid)
        {
            if (list == null || !list.isArray)
            {
                EditorGUILayout.HelpBox("Choices 数据不可用。", MessageType.Error);
                return;
            }

            EditorGUILayout.BeginVertical("box");
            EditorGUILayout.BeginHorizontal();
            EditorGUILayout.LabelField($"Choices ({list.arraySize})", EditorStyles.boldLabel);
            if (GUILayout.Button("+", GUILayout.Width(24)))
            {
                Undo.RecordObject(asset, "添加选择");
                list.arraySize++;
                var item = list.GetArrayElementAtIndex(list.arraySize - 1);
                item.FindPropertyRelative("choiceText").stringValue = string.Empty;
                var conditions = item.FindPropertyRelative("conditions");
                if (conditions != null) conditions.arraySize = 0;
                GUI.changed = true;
            }
            EditorGUILayout.EndHorizontal();

            int removeIndex = -1;
            for (int i = 0; i < list.arraySize; i++)
            {
                var item = list.GetArrayElementAtIndex(i);
                EditorGUILayout.BeginVertical("helpBox");
                var (open, deleteClicked) = DrawItemHeader($"{guid}:choice:{i}",
                    itemPalette[i % itemPalette.Length],
                    $"选择 {i}", item.FindPropertyRelative("choiceText").stringValue, defaultOpen: true);
                if (deleteClicked)
                    removeIndex = i;
                else if (open)
                {
                    EditorGUILayout.PropertyField(item.FindPropertyRelative("choiceText"),
                        new GUIContent("Choice Text"));
                    EditorGUILayout.PropertyField(item.FindPropertyRelative("conditionMode"),
                        new GUIContent("条件组合", "并:所有条件都满足才显示该选项;或:任一满足即显示"));
                    DrawManagedReferenceList(item.FindPropertyRelative("conditions"),
                        typeof(DialogueCondition), "条件", $"{guid}:choice:{i}:cond");
                }
                EditorGUILayout.EndVertical();
            }

            if (removeIndex >= 0)
            {
                Undo.RecordObject(asset, "删除选择");
                ShiftFoldStates($"{guid}:choice", removeIndex);
                list.DeleteArrayElementAtIndex(removeIndex);
                GUI.changed = true;
            }
            EditorGUILayout.EndVertical();
        }

        void DrawBranchCases(SerializedProperty list, string guid)
        {
            if (list == null || !list.isArray)
            {
                EditorGUILayout.HelpBox("Cases 数据不可用。", MessageType.Error);
                return;
            }

            EditorGUILayout.BeginVertical("box");
            EditorGUILayout.BeginHorizontal();
            EditorGUILayout.LabelField($"Cases ({list.arraySize})", EditorStyles.boldLabel);
            if (GUILayout.Button("+", GUILayout.Width(24)))
            {
                Undo.RecordObject(asset, "添加分支");
                list.arraySize++;
                var item = list.GetArrayElementAtIndex(list.arraySize - 1);
                item.FindPropertyRelative("label").stringValue = string.Empty;
                var conditions = item.FindPropertyRelative("conditions");
                if (conditions != null) conditions.arraySize = 0;
                GUI.changed = true;
            }
            EditorGUILayout.EndHorizontal();

            int removeIndex = -1;
            for (int i = 0; i < list.arraySize; i++)
            {
                var item = list.GetArrayElementAtIndex(i);
                EditorGUILayout.BeginVertical("helpBox");
                var (open, deleteClicked) = DrawItemHeader($"{guid}:case:{i}",
                    itemPalette[i % itemPalette.Length],
                    $"分支 {i}", item.FindPropertyRelative("label").stringValue, defaultOpen: true);
                if (deleteClicked)
                    removeIndex = i;
                else if (open)
                {
                    EditorGUILayout.PropertyField(item.FindPropertyRelative("label"),
                        new GUIContent("Label"));
                    EditorGUILayout.PropertyField(item.FindPropertyRelative("conditionMode"),
                        new GUIContent("条件组合", "并:所有条件都满足才命中该分支;或:任一满足即命中"));
                    DrawManagedReferenceList(item.FindPropertyRelative("conditions"),
                        typeof(DialogueCondition), "条件", $"{guid}:case:{i}:cond");
                }
                EditorGUILayout.EndVertical();
            }

            if (removeIndex >= 0)
            {
                Undo.RecordObject(asset, "删除分支");
                ShiftFoldStates($"{guid}:case", removeIndex);
                list.DeleteArrayElementAtIndex(removeIndex);
                GUI.changed = true;
            }
            EditorGUILayout.EndVertical();
        }

        /// <summary>
        /// 单事件节点(SingleEventNode)的详情面板:
        /// 事件类型选择/更换 + 事件字段编辑 + 事件类型全局颜色设置。
        /// </summary>
        void DrawSingleEventInspector(SerializedProperty nodeProp, SingleEventNode node)
        {
            var eventProp = nodeProp.FindPropertyRelative("eventData");
            var currentType = node.eventData?.GetType();

            EditorGUILayout.BeginVertical("box");

            // ── 事件类型选择 / 更换 ──────────────────────────────
            EditorGUILayout.BeginHorizontal();
            EditorGUILayout.LabelField("事件类型", EditorStyles.boldLabel,
                GUILayout.Width(EditorGUIUtility.labelWidth - 4));
            var buttonText = currentType == null
                ? "选择事件类型…"
                : GetManagedTypeDisplayName(currentType);
            if (GUILayout.Button(buttonText, EditorStyles.popup))
                ShowSingleEventTypeMenu(eventProp);
            EditorGUILayout.EndHorizontal();
            if (currentType != null)
            {
                var desc = DialogueTypeMetadata.GetDescription(currentType);
                if (!string.IsNullOrEmpty(desc))
                    EditorGUILayout.HelpBox(desc, MessageType.None);
            }

            EditorGUILayout.Space(2);

            // ── 事件类型颜色(全局,按类型生效) ────────────────────
            DrawEventColorField(currentType);

            EditorGUILayout.Space(4);

            // ── 事件字段 ────────────────────────────────────────
            if (currentType == null)
            {
                EditorGUILayout.HelpBox(
                    "还没有选择事件类型。点击上方按钮选择,或删除此节点后从右键菜单的\"事件节点\"分组创建具体类型。",
                    MessageType.Info);
            }
            else
            {
                DrawManagedReferenceFields(eventProp, currentType);
            }
            EditorGUILayout.EndVertical();
        }

        /// <summary>事件类型选择/更换菜单(单事件节点用)。</summary>
        void ShowSingleEventTypeMenu(SerializedProperty eventProp)
        {
            var menu = new GenericMenu();
            var types = GetManagedReferenceTypes(typeof(DialogueEvent)).ToList();
            if (types.Count == 0)
            {
                menu.AddDisabledItem(new GUIContent("没有可用类型"));
                menu.ShowAsContext();
                return;
            }

            var currentType = eventProp.managedReferenceValue?.GetType();
            foreach (var type in types)
            {
                var capturedType = type;
                bool active = currentType == capturedType;
                var content = new GUIContent(GetManagedTypeDisplayName(capturedType));
                if (active)
                    menu.AddDisabledItem(content, true);
                else
                    menu.AddItem(content, false, () =>
                    {
                        try
                        {
                            Undo.RecordObject(asset, "更换事件类型");
                            var so = eventProp.serializedObject;
                            so.Update();
                            eventProp.managedReferenceValue = Activator.CreateInstance(capturedType);
                            so.ApplyModifiedProperties();
                            EditorUtility.SetDirty(asset);
                            NotifyGraphChanged();
                            graphView?.RefreshAllNodes();
                            Repaint();
                        }
                        catch (Exception e)
                        {
                            Debug.LogException(e);
                        }
                    });
            }
            menu.ShowAsContext();
        }

        /// <summary>
        /// 事件类型颜色设置行:显示当前颜色 + 拾色器 + 恢复自动色按钮。
        /// 颜色按事件类型全局存储(EditorPrefs),对同类型所有节点生效。
        /// </summary>
        void DrawEventColorField(Type eventType)
        {
            if (eventType == null) return;

            EditorGUILayout.BeginHorizontal();
            var hasCustom = DialogueEventColorStore.HasCustomColor(eventType);
            var label = hasCustom ? "节点颜色(全局)" : "节点颜色(自动)";
            var color = DialogueEventColorStore.GetColor(eventType);
            var newColor = EditorGUILayout.ColorField(
                new GUIContent(label, "该颜色对同事件类型的所有节点全局生效(保存在本机编辑器偏好)"),
                color);
            if (!ColorsApproxEqual(newColor, color))
                DialogueEventColorStore.SetColor(eventType, newColor);
            if (hasCustom && GUILayout.Button("恢复自动", GUILayout.Width(64)))
            {
                DialogueEventColorStore.ClearColor(eventType);
                graphView?.RefreshAllNodes();
                Repaint();
            }
            EditorGUILayout.EndHorizontal();

            if (GUI.changed)
                graphView?.RefreshAllNodes();
        }

        static bool ColorsApproxEqual(Color a, Color b) =>
            Mathf.Abs(a.r - b.r) < 0.002f && Mathf.Abs(a.g - b.g) < 0.002f
            && Mathf.Abs(a.b - b.b) < 0.002f && Mathf.Abs(a.a - b.a) < 0.002f;

        void DrawManagedReferenceList(SerializedProperty list, Type baseType, string label, string keyPrefix)
        {
            if (list == null || !list.isArray)
            {
                EditorGUILayout.HelpBox($"{label} 数据不可用。", MessageType.Error);
                return;
            }

            EditorGUILayout.BeginVertical("box");
            EditorGUILayout.BeginHorizontal();
            EditorGUILayout.LabelField($"{label} ({list.arraySize})", EditorStyles.boldLabel);
            if (GUILayout.Button($"添加{label}", GUILayout.Width(72)))
                ShowManagedReferenceMenu(list, baseType, -1);
            EditorGUILayout.EndHorizontal();

            int removeIndex = -1;
            for (int i = 0; i < list.arraySize; i++)
            {
                var element = list.GetArrayElementAtIndex(i);
                var managedValue = element.managedReferenceValue;
                var typeName = managedValue == null
                    ? "未选择类型"
                    : GetManagedTypeDisplayName(managedValue.GetType());
                var summary = GetManagedReferenceSummary(managedValue);
                var subtitle = string.IsNullOrEmpty(summary) ? typeName : $"{typeName} - {summary}";

                EditorGUILayout.BeginVertical("helpBox");
                // 条件默认折叠:平时只保留一行"序号+类型+摘要",点标题行才展开编辑,
                // 避免条件多时详情面板被完全铺开(选择节点与分支节点共用此绘制)。
                var (open, deleteClicked) = DrawItemHeader($"{keyPrefix}:{i}",
                    itemPalette[i % itemPalette.Length], $"{label} {i}", subtitle, defaultOpen: false);
                if (deleteClicked)
                    removeIndex = i;
                else if (open)
                {
                    if (managedValue == null)
                    {
                        EditorGUILayout.HelpBox("还没有选择类型,请点击下面的按钮。", MessageType.Info);
                        if (GUILayout.Button($"选择{label}类型"))
                            ShowManagedReferenceMenu(list, baseType, i);
                    }
                    else
                    {
                        DrawManagedReferenceFields(element, managedValue.GetType());
                    }
                }
                EditorGUILayout.EndVertical();
            }

            if (removeIndex >= 0)
            {
                Undo.RecordObject(asset, $"删除{label}");
                ShiftFoldStates(keyPrefix, removeIndex);
                list.DeleteArrayElementAtIndex(removeIndex);
                GUI.changed = true;
            }
            EditorGUILayout.EndVertical();
        }

        // ── 列表条目的折叠状态与配色(选项/分支/条件详情共用) ─────
        // IMGUI 每帧重绘,局部变量无法保存折叠状态,必须用静态字典记忆。
        // 键格式:{guid}:choice:{i} / {guid}:case:{i} / {guid}:choice:{i}:cond:{j}。
        static readonly Dictionary<string, bool> foldStates = new Dictionary<string, bool>();

        // 每个条目循环取一个醒目颜色画色带,条目多时靠颜色即可一眼区分每一块
        static readonly Color[] itemPalette =
        {
            new Color(0.30f, 0.72f, 0.95f), // 蓝
            new Color(0.98f, 0.66f, 0.28f), // 橙
            new Color(0.56f, 0.82f, 0.36f), // 绿
            new Color(0.82f, 0.52f, 0.94f), // 紫
            new Color(0.96f, 0.44f, 0.56f), // 玫红
            new Color(0.90f, 0.80f, 0.34f), // 黄
            new Color(0.42f, 0.80f, 0.76f), // 青
            new Color(0.72f, 0.62f, 0.55f), // 棕
        };

        // 惰性初始化:new GUIStyle 必须在 OnGUI 上下文中调用,
        // 类加载时(如注册菜单)创建会在部分版本抛"can only be called from inside OnGUI"。
        static GUIStyle subtitleStyle;

        static GUIStyle SubtitleStyle => subtitleStyle ??= new GUIStyle(EditorStyles.miniLabel)
        {
            clipping = TextClipping.Clip
        };

        static bool GetFoldState(string key, bool defaultValue) =>
            foldStates.TryGetValue(key, out var value) ? value : defaultValue;

        static void SetFoldState(string key, bool value) => foldStates[key] = value;

        /// <summary>删除列表元素后,把折叠状态键中大于删除下标的序号整体前移,避免记忆串位到别的元素。</summary>
        static void ShiftFoldStates(string listPrefix, int removedIndex)
        {
            var shifted = new Dictionary<string, bool>();
            foreach (var kv in foldStates)
            {
                if (!kv.Key.StartsWith(listPrefix + ":", StringComparison.Ordinal))
                {
                    shifted[kv.Key] = kv.Value;
                    continue;
                }
                var rest = kv.Key.Substring(listPrefix.Length + 1);
                var separator = rest.IndexOf(':');
                var numberPart = separator < 0 ? rest : rest.Substring(0, separator);
                if (!int.TryParse(numberPart, out var index) || index < removedIndex)
                {
                    shifted[kv.Key] = kv.Value;
                    continue;
                }
                if (index == removedIndex) continue; // 被删条目自身的状态直接丢弃
                var suffix = separator < 0 ? string.Empty : rest.Substring(separator);
                shifted[$"{listPrefix}:{index - 1}{suffix}"] = kv.Value;
            }
            foldStates.Clear();
            foreach (var kv in shifted) foldStates[kv.Key] = kv.Value;
        }

        /// <summary>
        /// 列表条目的可折叠标题行:半透明色带背景 + 左侧实心色条 + 折叠箭头 + 标题/副标题 + 删除按钮。
        /// 返回(是否展开, 是否点击了删除)。副标题用于折叠时快速识别内容(选项文本/分支标签/条件摘要)。
        /// </summary>
        (bool open, bool deleteClicked) DrawItemHeader(string key, Color accent, string title,
            string subtitle, bool defaultOpen)
        {
            var open = GetFoldState(key, defaultOpen);
            var rect = EditorGUILayout.GetControlRect(false, EditorGUIUtility.singleLineHeight + 6);

            // 色带略超出内容区,与 helpBox 边框对齐成通栏效果
            var band = new Rect(rect.x - 2, rect.y - 1, rect.width + 4, rect.height + 2);
            EditorGUI.DrawRect(band, new Color(accent.r, accent.g, accent.b, 0.22f));
            EditorGUI.DrawRect(new Rect(band.x, band.y, 3, band.height), accent);

            var delRect = new Rect(band.xMax - 46, rect.y + 2, 42, rect.height - 4);
            var deleteClicked = GUI.Button(delRect, "删除", EditorStyles.miniButton);

            var arrowRect = new Rect(band.x + 6, rect.y, 14, rect.height);
            open = EditorGUI.Foldout(arrowRect, open, GUIContent.none);

            var titleWidth = EditorStyles.boldLabel.CalcSize(new GUIContent(title)).x;
            var titleRect = new Rect(arrowRect.xMax + 2, rect.y, titleWidth + 2, rect.height);
            GUI.Label(titleRect, title, EditorStyles.boldLabel);

            if (!string.IsNullOrEmpty(subtitle))
            {
                var subtitleRect = new Rect(titleRect.xMax, rect.y + 2,
                    Mathf.Max(20f, delRect.x - 6 - titleRect.xMax), rect.height);
                GUI.Label(subtitleRect, subtitle.Replace('\n', ' '), SubtitleStyle);
            }

            // 点击色带空白处也可切换折叠(箭头与删除按钮会先消费各自的事件,不会重复切换)
            var evt = Event.current;
            if (evt != null && evt.type == EventType.MouseDown && evt.button == 0
                && band.Contains(evt.mousePosition))
            {
                open = !open;
                evt.Use();
            }

            SetFoldState(key, open);
            return (open, deleteClicked);
        }

        void DrawManagedReferenceFields(SerializedProperty element, Type managedType)
        {
            for (var type = managedType; type != null && type != typeof(object); type = type.BaseType)
            {
                foreach (var field in type.GetFields(BindingFlags.Instance | BindingFlags.Public
                    | BindingFlags.NonPublic | BindingFlags.DeclaredOnly))
                {
                    if (!IsInspectableField(field)) continue;
                    var property = element.FindPropertyRelative(field.Name);
                    if (property == null) continue;
                    EditorGUILayout.PropertyField(property, true);
                }
            }
        }

        void ShowManagedReferenceMenu(SerializedProperty list, Type baseType, int targetIndex)
        {
            var menu = new GenericMenu();
            var types = GetManagedReferenceTypes(baseType).ToList();
            if (types.Count == 0)
            {
                menu.AddDisabledItem(new GUIContent("没有可用类型"));
                menu.ShowAsContext();
                return;
            }

            foreach (var type in types)
            {
                var capturedType = type;
                menu.AddItem(new GUIContent(
                    GetManagedTypeDisplayName(capturedType),
                    DialogueTypeMetadata.GetDescription(capturedType)), false, () =>
                {
                    try
                    {
                        Undo.RecordObject(asset, targetIndex < 0 ? "添加多态元素" : "选择多态类型");
                        var serializedObject = list.serializedObject;
                        serializedObject.Update();
                        var index = targetIndex < 0 ? list.arraySize : targetIndex;
                        if (targetIndex < 0) list.arraySize++;
                        var element = list.GetArrayElementAtIndex(index);
                        element.managedReferenceValue = Activator.CreateInstance(capturedType);
                        serializedObject.ApplyModifiedProperties();
                        EditorUtility.SetDirty(asset);
                        NotifyGraphChanged();
                        graphView?.RefreshAllNodes();
                        Repaint();
                    }
                    catch (Exception e)
                    {
                        Debug.LogException(e);
                    }
                });
            }
            menu.ShowAsContext();
        }

        static IEnumerable<Type> GetManagedReferenceTypes(Type baseType)
        {
            IEnumerable<Type> types;
            if (baseType == typeof(DialogueCondition))
                types = TypeCache.GetTypesDerivedFrom<DialogueCondition>();
            else if (baseType == typeof(DialogueEvent))
                types = TypeCache.GetTypesDerivedFrom<DialogueEvent>();
            else
                types = Enumerable.Empty<Type>();

            return types.Where(t => !t.IsAbstract && !t.ContainsGenericParameters)
                .OrderBy(t => t.FullName);
        }

        static string GetManagedTypeDisplayName(Type type)
        {
            var customName = DialogueTypeMetadata.GetDisplayName(type);
            return customName == type.Name ? ObjectNames.NicifyVariableName(type.Name) : customName;
        }

        static string GetManagedReferenceSummary(object value)
        {
            if (value is DialogueCondition condition) return condition.GetSummary();
            if (value is DialogueEvent dialogueEvent) return dialogueEvent.GetSummary();
            return string.Empty;
        }

        static bool IsInspectableField(FieldInfo field)
        {
            if (field.IsNotSerialized) return false;
            if (field.GetCustomAttribute<HideInInspector>() != null) return false;
            return field.IsPublic || field.GetCustomAttribute<SerializeField>() != null;
        }

        static Label MakeHint(string text)
        {
            var label = new Label(text);
            label.style.whiteSpace = WhiteSpace.Normal;
            label.style.paddingLeft = 6;
            label.style.color = new Color(0.7f, 0.7f, 0.7f);
            return label;
        }

        /// <summary>
        /// 对话正文使用不设上限的自适应文本框。
        /// TextAreaAttribute 的 maxLines 会把长文本截在固定高度,不适合编辑长对话;
        /// 这里保留原来的最小两行高度,并按换行和自动换行后的真实高度扩展。
        /// </summary>
        void DrawAdaptiveTextArea(SerializedProperty property)
        {
            var style = new GUIStyle(EditorStyles.textArea)
            {
                wordWrap = true,
                richText = false,
                padding = new RectOffset(5, 5, 4, 4)
            };

            var text = property.stringValue ?? string.Empty;
            var label = new GUIContent(property.displayName);
            // currentViewWidth 在嵌套 IMGUIContainer 中有时拿到的是整个窗口宽度,
            // 会导致 CalcHeight 低估。优先使用左侧详情面板的实际内容宽度。
            var panelWidth = inspectorPanel == null ? 0f : inspectorPanel.resolvedStyle.width;
            if (panelWidth <= 0f && inspectorPanel != null)
                panelWidth = inspectorPanel.contentRect.width;
            if (panelWidth <= 0f) panelWidth = EditorGUIUtility.currentViewWidth;
            var fieldWidth = Mathf.Max(80f, panelWidth - EditorGUIUtility.labelWidth - 18f);
            var measuredHeight = style.CalcHeight(
                new GUIContent(string.IsNullOrEmpty(text) ? " " : text), fieldWidth);
            var minimumHeight = EditorGUIUtility.singleLineHeight * 2f + 10f;
            var height = Mathf.Max(minimumHeight, measuredHeight + 2f);
            var rect = EditorGUILayout.GetControlRect(false, height);
            var labelRect = new Rect(rect.x, rect.y, EditorGUIUtility.labelWidth,
                EditorGUIUtility.singleLineHeight);
            var textRect = new Rect(rect.x + EditorGUIUtility.labelWidth, rect.y,
                Mathf.Max(80f, rect.width - EditorGUIUtility.labelWidth), rect.height);
            EditorGUI.LabelField(labelRect, label);

            EditorGUI.BeginChangeCheck();
            var newText = EditorGUI.TextArea(textRect, text, style);
            if (EditorGUI.EndChangeCheck())
            {
                property.stringValue = newText;
                GUI.changed = true;
            }
        }

        static SerializedProperty FindNodeProperty(SerializedObject so, string guid)
        {
            var nodes = so.FindProperty("nodes");
            if (nodes == null || !nodes.isArray) return null;
            for (int i = 0; i < nodes.arraySize; i++)
            {
                var element = nodes.GetArrayElementAtIndex(i);
                var guidProp = element.FindPropertyRelative("guid");
                if (guidProp != null && guidProp.stringValue == guid)
                    return element.Copy();
            }
            return null;
        }
    }
}
