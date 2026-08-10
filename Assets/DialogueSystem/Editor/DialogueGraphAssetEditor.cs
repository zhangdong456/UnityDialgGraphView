using UnityEditor;
using UnityEngine;

namespace DialogueSystem.Editor
{
    /// <summary>Dialogue Graph 资产的默认 Inspector:只提供打开图编辑器的入口。</summary>
    [CustomEditor(typeof(DialogueGraphAsset))]
    public class DialogueGraphAssetEditor : UnityEditor.Editor
    {
        public override void OnInspectorGUI()
        {
            if (GUILayout.Button("在图编辑器中打开", GUILayout.Height(30)))
                DialogueGraphWindow.OpenWith((DialogueGraphAsset)target);

            EditorGUILayout.HelpBox(
                "对话图的节点与连线都保存在此资产中,请使用图编辑器进行编辑。\n" +
                "也可以直接双击资产打开。",
                MessageType.Info);
        }
    }
}
