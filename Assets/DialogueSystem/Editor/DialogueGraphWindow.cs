using System;
using System.Reflection;
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
            var target = EditorUtility.InstanceIDToObject(instanceId) as DialogueGraphAsset;
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
            inspectedGuid = graphView?.GetSelectedNodeData()?.guid;

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

            // 用 IMGUIContainer + EditorGUILayout.PropertyField 逐字段绘制。
            // UIElements 的 PropertyField 在 SerializeReference 数组元素上渲染不可靠
            // (实测会出现空白或只显示折叠框);IMGUI 是 Inspector 的成熟路径,
            // Unity 2022 中 SerializeReference 列表的类型选择器在 IMGUI 下同样可用。
            string guid = inspectedGuid;
            inspectorPanel.Add(new IMGUIContainer(() => DrawNodeInspector(guid)));
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

                // 注意:不要用 SerializedProperty.NextVisible 枚举 SerializeReference 数组元素
                // 的子字段——实测它一个可见子属性都枚举不出来(元素属性的怪癖)。
                // 改为直接反射节点对象的字段,再按字段名取序列化属性绘制。
                int drawn = 0;
                for (var t = data.GetType(); t != null && t != typeof(object); t = t.BaseType)
                {
                    foreach (var field in t.GetFields(BindingFlags.Instance | BindingFlags.Public
                        | BindingFlags.NonPublic | BindingFlags.DeclaredOnly))
                    {
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
            catch (Exception e)
            {
                Debug.LogException(e);
            }
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
        static void DrawAdaptiveTextArea(SerializedProperty property)
        {
            var style = new GUIStyle(EditorStyles.textArea)
            {
                wordWrap = true,
                richText = false,
                padding = new RectOffset(5, 5, 4, 4)
            };

            var label = new GUIContent(property.displayName);
            var previewWidth = Mathf.Max(100f, EditorGUIUtility.currentViewWidth
                - EditorGUIUtility.labelWidth - 30f);
            var text = property.stringValue ?? string.Empty;
            var measuredHeight = style.CalcHeight(new GUIContent(text), previewWidth);
            var minimumHeight = EditorGUIUtility.singleLineHeight * 2f + 10f;
            var height = Mathf.Max(minimumHeight, measuredHeight + 2f);
            var rect = EditorGUILayout.GetControlRect(true, height);
            var textRect = EditorGUI.PrefixLabel(rect, label);

            EditorGUI.BeginChangeCheck();
            var newText = EditorGUI.TextArea(textRect, text, style);
            if (EditorGUI.EndChangeCheck())
                property.stringValue = newText;
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
