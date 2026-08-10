using System;
using System.Collections.Generic;
using UnityEngine;

namespace DialogueSystem
{
    /// <summary>
    /// 运行时对话播放器:从 Start 节点开始遍历对话图,通过事件把内容交给 UI 层。
    /// 交互节点(对话/选择/等待)会暂停,等待 UI 调用 continue 回调;
    /// 自动节点(开始/分支/事件/跳转)立即向后执行,走到 End 节点时对话结束。
    ///
    /// 典型用法:
    ///   var player = new DialoguePlayer();
    ///   player.OnDialogue += (speaker, text, cont) => ui.ShowLine(speaker, text, cont);
    ///   player.OnChoice   += (speaker, text, choices, cb) => ui.ShowChoices(speaker, text, choices, cb);
    ///   player.OnWait     += (seconds, cont) => StartCoroutine(HideAndWait(seconds, cont));
    ///   player.OnEnd      += () => ui.Hide();
    ///   player.Play(dialogueAsset, context);
    /// </summary>
    public class DialoguePlayer
    {
        /// <summary>一个可见选项。choiceIndex 是选项在选择节点中的原始下标。</summary>
        public class ChoiceInfo
        {
            public int choiceIndex;
            public string text;
        }

        /// <summary>自动节点连续执行的安全上限,防止图里出现死循环。</summary>
        const int MaxAutoSteps = 10000;

        public DialogueContext Context { get; private set; }
        public DialogueGraphAsset CurrentAsset { get; private set; }
        public bool IsPlaying { get; private set; }

        /// <summary>普通对话:(说话者, 内容, 继续回调)。UI 点击"继续"时调用回调。</summary>
        public event Action<string, string, Action> OnDialogue;

        /// <summary>选择:(说话者, 内容, 可见选项列表, 选择回调)。回调参数为 ChoiceInfo.choiceIndex。</summary>
        public event Action<string, string, List<ChoiceInfo>, Action<int>> OnChoice;

        /// <summary>等待:(秒数, 继续回调)。等待期间应隐藏对话界面。</summary>
        public event Action<float, Action> OnWait;

        /// <summary>即将跳转到另一个对话资产时触发(随后自动从新资产入口继续)。</summary>
        public event Action<DialogueGraphAsset> OnJump;

        /// <summary>对话结束(走到尽头、无可选选项、跳转目标为空或主动 Stop)。</summary>
        public event Action OnEnd;

        DialogueNodeData current;

        /// <summary>开始播放一张对话图。context 为空时自动新建。</summary>
        public void Play(DialogueGraphAsset asset, DialogueContext context = null)
        {
            if (asset == null) throw new ArgumentNullException(nameof(asset));
            Context = context ?? new DialogueContext();
            CurrentAsset = asset;
            IsPlaying = true;
            current = asset.GetEntryNode();
            Process();
        }

        /// <summary>强制结束对话。</summary>
        public void Stop()
        {
            current = null;
            if (IsPlaying)
            {
                IsPlaying = false;
                OnEnd?.Invoke();
            }
        }

        void Continue(int outputPort)
        {
            if (!IsPlaying || current == null) return;
            current = CurrentAsset.GetNextNode(current.guid, outputPort);
            Process();
        }

        void Process()
        {
            int autoSteps = 0;
            while (IsPlaying && current != null)
            {
                if (++autoSteps > MaxAutoSteps)
                {
                    Debug.LogError("[DialogueSystem] 自动节点连续执行超过上限,对话图中可能存在死循环,已强制结束。");
                    current = null;
                    break;
                }

                switch (current)
                {
                    case StartNode s:
                        // 开始节点:沿输出端口进入第一个节点
                        current = CurrentAsset.GetNextNode(s.guid, 0);
                        break;

                    case EndNode _:
                        // 结束节点:对话到此结束
                        current = null;
                        break;

                    case DialogueNode d:
                        if (OnDialogue != null)
                        {
                            OnDialogue.Invoke(d.speakerName, d.dialogueText, () => Continue(0));
                            return;
                        }
                        current = CurrentAsset.GetNextNode(d.guid, 0);
                        break;

                    case ChoiceNode c:
                    {
                        var visible = new List<ChoiceInfo>();
                        for (int i = 0; i < c.choices.Count; i++)
                            if (c.choices[i] != null && c.choices[i].IsVisible(Context))
                                visible.Add(new ChoiceInfo { choiceIndex = i, text = c.choices[i].choiceText });

                        if (visible.Count == 0 || OnChoice == null)
                        {
                            // 没有任何选项满足条件(或没人监听选择事件),对话结束。
                            current = null;
                            break;
                        }
                        OnChoice.Invoke(c.speakerName, c.dialogueText, visible, index => Continue(index));
                        return;
                    }

                    case WaitNode w:
                        if (OnWait != null)
                        {
                            OnWait.Invoke(w.waitSeconds, () => Continue(0));
                            return;
                        }
                        current = CurrentAsset.GetNextNode(w.guid, 0);
                        break;

                    case EventNode e:
                        if (e.events != null)
                            foreach (var evt in e.events)
                                evt?.Execute(Context);
                        current = CurrentAsset.GetNextNode(e.guid, 0);
                        break;

                    case StateBranchNode b:
                        current = CurrentAsset.GetNextNode(b.guid, b.Evaluate(Context));
                        break;

                    case JumpNode j:
                        if (j.targetDialogue == null)
                        {
                            current = null;
                            break;
                        }
                        OnJump?.Invoke(j.targetDialogue);
                        CurrentAsset = j.targetDialogue;
                        current = CurrentAsset.GetEntryNode();
                        break;

                    default:
                        // 未识别的自定义节点:默认沿 0 号输出端口继续。
                        current = CurrentAsset.GetNextNode(current.guid, 0);
                        break;
                }
            }

            if (IsPlaying)
            {
                IsPlaying = false;
                current = null;
                OnEnd?.Invoke();
            }
        }
    }
}
