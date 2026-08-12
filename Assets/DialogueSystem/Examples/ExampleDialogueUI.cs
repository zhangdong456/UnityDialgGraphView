using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace DialogueSystem.Examples
{
    /// <summary>
    /// 最小可运行示例:演示如何调用 DialoguePlayer 播放一张对话图。
    /// 用法:
    ///   1. 把本脚本挂到场景里任意一个 GameObject 上;
    ///   2. 把对话资产拖到 Dialogue 字段(可先用菜单 Tools → Dialogue System → 生成示例对话资产);
    ///   3. 运行场景,按空格开始对话。
    /// 界面直接用 OnGUI 绘制,不需要额外搭建 UI。正式项目里换成你自己的 UI 即可。
    /// </summary>
    public class ExampleDialogueUI : MonoBehaviour
    {
        [Tooltip("要播放的对话图资产")]
        public DialogueGraphAsset dialogue;

        [Tooltip("开始对话的按键")]
        public KeyCode startKey = KeyCode.Space;

        DialoguePlayer player;
        DialogueContext context;

        // 当前界面状态
        bool panelVisible;
        string speaker = string.Empty;
        string text = string.Empty;
        List<DialoguePlayer.ChoiceInfo> currentChoices;
        Action<int> choiceCallback;
        Action continueCallback;

        void Awake()
        {
            // 示例运行数据:实际项目里请换成你自己的游戏状态来源
            context = new DialogueContext();
            context.Blackboard.SetInt("gold", 100);

            player = new DialoguePlayer();
            player.OnDialogue += HandleDialogue;
            player.OnChoice += HandleChoice;
            player.OnWait += HandleWait;
            player.OnJump += target => Debug.Log($"[对话示例] 跳转到对话资产: {target.name}");
            player.OnEnd += HandleEnd;
        }

        void Update()
        {
            if (dialogue != null && !player.IsPlaying && Input.GetKeyDown(startKey))
                player.Play(dialogue, context);
        }

        void HandleDialogue(string speakerName, string content, Action onContinue)
        {
            speaker = speakerName;
            text = content;
            currentChoices = null;
            choiceCallback = null;
            continueCallback = onContinue;
            panelVisible = true;
        }

        void HandleChoice(List<DialoguePlayer.ChoiceInfo> visibleChoices, Action<int> callback)
        {
            speaker = string.Empty;
            text = string.Empty;
            currentChoices = visibleChoices;
            choiceCallback = callback;
            continueCallback = null;
            panelVisible = true;
        }

        void HandleWait(float seconds, Action onContinue)
        {
            // 等待节点:隐藏对话界面,这段时间可以用来播放人物动画等
            panelVisible = false;
            StartCoroutine(WaitRoutine(seconds, onContinue));
        }

        static IEnumerator WaitRoutine(float seconds, Action onContinue)
        {
            yield return new WaitForSeconds(seconds);
            onContinue();
        }

        void HandleEnd()
        {
            panelVisible = false;
            currentChoices = null;
            choiceCallback = null;
            continueCallback = null;
            Debug.Log("[对话示例] 对话结束");
        }

        void OnGUI()
        {
            if (!panelVisible) return;

            float width = Mathf.Min(620f, Screen.width - 40f);
            var rect = new Rect((Screen.width - width) / 2f, Screen.height - 210f, width, 170f);
            GUI.Box(rect, GUIContent.none);
            GUILayout.BeginArea(rect);
            GUILayout.Space(6);
            if (!string.IsNullOrEmpty(speaker))
                GUILayout.Label($"【{speaker}】");
            if (!string.IsNullOrEmpty(text))
                GUILayout.Label(text);
            if (currentChoices != null)
                GUILayout.Label("请选择:");
            GUILayout.FlexibleSpace();

            if (currentChoices != null)
            {
                foreach (var choice in currentChoices)
                {
                    if (GUILayout.Button(choice.text))
                    {
                        var callback = choiceCallback;
                        currentChoices = null;
                        choiceCallback = null;
                        callback(choice.choiceIndex);
                        break;
                    }
                }
            }
            else if (continueCallback != null)
            {
                if (GUILayout.Button("继续"))
                {
                    var callback = continueCallback;
                    continueCallback = null;
                    callback();
                }
            }

            GUILayout.EndArea();
        }
    }
}
