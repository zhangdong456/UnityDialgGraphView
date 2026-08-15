#!/usr/bin/env bash
# DialogueSystem 离线一键验证(不开 Unity 编辑器):
#   bash verify.sh [Unity安装根目录]      # 例: bash verify.sh "C:/software/UnityEditor/2022.3.62f3/Editor"
# 不传参数时自动探测常见安装位置。
# 步骤: ① csc 编译 Runtime/Editor/Examples(真实 UnityEngine DLL 引用)
#       ② 最小 UnityEngine 桩 + 真实 Runtime 源码编译执行逻辑断言(并/或条件、单事件节点播放链路)
set -u
cd "$(dirname "$0")"
PROJ="Assets/DialogueSystem"

# ── 定位 Unity 安装根目录 ────────────────────────────────
find_unity() {
  local cands=(
    "/c/software/UnityEditor/2022.3.62f3/Editor"
    /c/software/UnityEditor/*/Editor
    /d/software/UnityEditor/*/Editor
    "/c/Program Files/Unity/Hub/Editor"/*/Editor
    /d/UnityHub/Editor/*/Editor
    /c/UnityHub/Editor/*/Editor
  )
  for d in "${cands[@]}"; do
    if [ -f "$d/Data/MonoBleedingEdge/bin/mono.exe" ]; then
      echo "$d"; return 0
    fi
  done
  return 1
}
UNITY="${1:-}"
if [ -z "$UNITY" ]; then
  UNITY="$(find_unity)" || { echo "FAIL: 未找到 Unity,请传入安装根目录: bash verify.sh <Unity根目录>"; exit 1; }
fi
echo "Unity root: $UNITY"

MONO="$(cygpath -w "$UNITY/Data/MonoBleedingEdge/bin/mono.exe")"
CSC="$(cygpath -w "$UNITY/Data/MonoBleedingEdge/lib/mono/4.5/csc.exe")"
NS="$(cygpath -w "$UNITY/Data/MonoBleedingEdge/lib/mono/4.5/Facades/netstandard.dll")"
REF="-r:$NS"
for f in "$UNITY/Data/Managed/UnityEngine"/*.dll; do REF="$REF -r:$(cygpath -w "$f")"; done

TMP="$(mktemp -d -t hermes-verify-XXXXXX)"
trap 'rm -rf "$TMP"' EXIT
# mono/csc 是 Windows 程序,MSYS /tmp 路径必须转成 Windows 混合格式(C:/...)
TMPW="$(cygpath -m "$TMP")"

RT_SRC=("$PROJ/Runtime"/*.cs "$PROJ/Runtime/Nodes"/*.cs "$PROJ/Runtime/Conditions"/*.cs "$PROJ/Runtime/Events"/*.cs)

# ── ① 三程序集编译 ───────────────────────────────────────
# Editor 程序集引用 Assets/Plugins/NPOI 的 DLL(DialogueTextExporter 依赖)
NPOI_DIR="$PROJ/../Plugins/NPOI"
NPOI_REF=""
if [ -d "$NPOI_DIR" ]; then
  for dll in "$NPOI_DIR"/*.dll; do NPOI_REF="$NPOI_REF -r:$(cygpath -m "$dll")"; done
fi

"$MONO" "$CSC" -nologo -target:library -out:"$TMPW/Runtime.dll" -langversion:latest $REF "${RT_SRC[@]}" \
  || { echo "FAIL: Runtime 编译错误"; exit 1; }
"$MONO" "$CSC" -nologo -target:library -out:"$TMPW/Editor.dll" -langversion:latest -r:"$TMPW/Runtime.dll" $NPOI_REF $REF "$PROJ/Editor"/*.cs \
  || { echo "FAIL: Editor 编译错误"; exit 1; }
"$MONO" "$CSC" -nologo -target:library -out:"$TMPW/Examples.dll" -langversion:latest \
  -r:"$TMPW/Runtime.dll" -r:"$TMPW/Editor.dll" $REF \
  "$PROJ/Examples/ExampleDialogueUI.cs" "$PROJ/Examples/Editor/ExampleDialogueGenerator.cs" \
  || { echo "FAIL: Examples 编译错误"; exit 1; }
echo "[1/2] COMPILE Runtime+Editor+Examples: OK"

# ── ② 逻辑断言(桩 + 真实 Runtime 源码) ──────────────────
cat > "$TMPW/stubs.cs" <<'STUB_EOF'
using System;
namespace UnityEngine
{
    public class Object
    {
        public static bool operator ==(Object a, Object b) => ReferenceEquals(a, b);
        public static bool operator !=(Object a, Object b) => !ReferenceEquals(a, b);
        public override bool Equals(object o) => ReferenceEquals(this, o);
        public override int GetHashCode() => base.GetHashCode();
        public string name = "stub";
    }
    public class ScriptableObject : Object { }
    public struct Vector2
    {
        public float x, y;
        public Vector2(float x, float y) { this.x = x; this.y = y; }
        public static Vector2 operator +(Vector2 a, Vector2 b) => new Vector2(a.x + b.x, a.y + b.y);
    }
    public class AudioClip : Object { }
    public static class Debug
    {
        public static void LogError(object m) => Console.Error.WriteLine(m);
        public static void Log(object m) => Console.WriteLine(m);
    }
    public class TooltipAttribute : Attribute { public TooltipAttribute(string t = "") { } }
    public class HideInInspector : Attribute { }
    public class SerializeField : Attribute { }
    public class SerializeReference : Attribute { }
    public class MinAttribute : Attribute { public MinAttribute(float m) { } }
    public class TextAreaAttribute : Attribute { public TextAreaAttribute(int a, int b) { } }
    public class CreateAssetMenuAttribute : Attribute
    { public string fileName = "a"; public string menuName = ""; public int order = 0; }
}
STUB_EOF

cat > "$TMPW/smoke.cs" <<'SMOKE_EOF'
using System;
using System.Collections.Generic;
using System.Linq;
using DialogueSystem;
static class SmokeTest
{
    static int failures;
    static void Check(string name, bool ok)
    {
        Console.WriteLine((ok ? "PASS " : "FAIL ") + name);
        if (!ok) failures++;
    }
    class SetFlagEvent : DialogueEvent
    {
        public string key; public bool value;
        public override void Execute(DialogueContext ctx) => ctx.Blackboard.SetBool(key, value);
        public override string GetSummary() => $"{key} = {value}";
    }
    static void Main()
    {
        var ctx = new DialogueContext();
        ctx.Blackboard.SetInt("gold", 30);

        // 选择条件:并/或/边界
        var optAnd = new ChoiceOption
        {
            conditions = new List<DialogueCondition>
            {
                new IntCompareCondition { key = "gold", op = CompareOperator.GreaterOrEqual, value = 50 },
                new BoolFlagCondition { key = "flag", expectedValue = true }
            }
        };
        Check("choice AND miss -> hidden", !optAnd.IsVisible(ctx));
        ctx.Blackboard.SetInt("gold", 100); ctx.Blackboard.SetBool("flag", true);
        Check("choice AND ok -> shown", optAnd.IsVisible(ctx));

        ctx.Blackboard.SetInt("gold", 30);
        var optOr = new ChoiceOption
        {
            conditionMode = ConditionCombineMode.Any,
            conditions = new List<DialogueCondition>
            {
                new IntCompareCondition { key = "gold", op = CompareOperator.GreaterOrEqual, value = 50 },
                new BoolFlagCondition { key = "flag", expectedValue = true }
            }
        };
        Check("choice OR one ok -> shown", optOr.IsVisible(ctx));
        ctx.Blackboard.SetBool("flag", false);
        Check("choice OR none -> hidden", !optOr.IsVisible(ctx));
        optOr.conditions.Add(null);
        Check("choice OR null ignored", !optOr.IsVisible(ctx));

        var optEmpty = new ChoiceOption();
        Check("choice empty always shown", optEmpty.IsVisible(ctx) && optEmpty.IsVisible(null));
        var optNoCtx = new ChoiceOption
        { conditions = new List<DialogueCondition> { new BoolFlagCondition { key = "flag" } } };
        Check("choice cond no ctx hidden (legacy)", !optNoCtx.IsVisible(null));

        // 分支:并/或/默认端口
        var branch = new StateBranchNode
        {
            cases = new List<BranchCase>
            {
                new BranchCase
                {
                    label = "or", conditionMode = ConditionCombineMode.Any,
                    conditions = new List<DialogueCondition>
                    { new IntCompareCondition { key = "gold", op = CompareOperator.GreaterOrEqual, value = 50 } }
                },
                new BranchCase
                {
                    label = "and", conditionMode = ConditionCombineMode.All,
                    conditions = new List<DialogueCondition>
                    {
                        new IntCompareCondition { key = "gold", op = CompareOperator.GreaterOrEqual, value = 5 },
                        new BoolFlagCondition { key = "flag", expectedValue = true }
                    }
                }
            }
        };
        ctx.Blackboard.SetInt("gold", 30); ctx.Blackboard.SetBool("flag", true);
        Check("branch or-miss and-hit -> port 1", branch.Evaluate(ctx) == 1);
        ctx.Blackboard.SetInt("gold", 100);
        Check("branch or-hit -> port 0 (top-down)", branch.Evaluate(ctx) == 0);
        ctx.Blackboard.SetInt("gold", 10); ctx.Blackboard.SetBool("flag", false);
        Check("branch all-miss -> default port 2", branch.Evaluate(ctx) == 2);
        Check("branch empty case never matches", !new BranchCase().Matches(ctx));

        // 4. 随机分支节点
        // 4a. 空 cases → 端口 0(不随机)
        Check("random empty cases -> port 0", new RandomBranchNode().Evaluate(ctx) == 0);

        // 4b. 确定性:ResetSeed 后两次同输入序列结果一致
        var rndNode = new RandomBranchNode
        {
            cases = new List<BranchCase>
            {
                new BranchCase { label = "a" },  // 无条件 → 永远参与
                new BranchCase { label = "b" }
            }
        };
        RandomBranchNode.ResetSeed(42);
        var seq1 = new List<int>();
        for (int i = 0; i < 10; i++) seq1.Add(rndNode.Evaluate(ctx));
        RandomBranchNode.ResetSeed(42);
        var seq2 = new List<int>();
        for (int i = 0; i < 10; i++) seq2.Add(rndNode.Evaluate(ctx));
        Check("random deterministic with same seed", seq1.SequenceEqual(seq2));
        Check("random covers both branches", seq1.Any(v => v == 0) && seq1.Any(v => v == 1));

        // 4c. 频率:2000 次两条等概率分支约各占一半(45%~55%)
        RandomBranchNode.ResetSeed(7);
        int c0 = 0, c1 = 0;
        for (int i = 0; i < 2000; i++)
        {
            var r = rndNode.Evaluate(ctx);
            if (r == 0) c0++; else if (r == 1) c1++;
        }
        Check("random ~50/50 over 2000 rolls (45%-55%)",
            c0 >= 900 && c0 <= 1100 && c0 + c1 == 2000);

        // 4d. 条件过滤:gold>=50 满足时分支0参与,不满足时只剩无条件分支1;
        //     全部不满足(有条件但都失败)→ 默认端口
        var rndCond = new RandomBranchNode
        {
            cases = new List<BranchCase>
            {
                new BranchCase
                {
                    label = "rich",
                    conditions = new List<DialogueCondition>
                    { new IntCompareCondition { key = "gold", op = CompareOperator.GreaterOrEqual, value = 50 } }
                },
                new BranchCase { label = "anyone" }
            }
        };
        ctx.Blackboard.SetInt("gold", 100);
        var picked = new HashSet<int>();
        for (int i = 0; i < 100; i++) picked.Add(rndCond.Evaluate(ctx));
        Check("random cond rich+anyone both reachable when gold=100", picked.SetEquals(new[] { 0, 1 }));
        ctx.Blackboard.SetInt("gold", 10);
        picked.Clear();
        for (int i = 0; i < 100; i++) picked.Add(rndCond.Evaluate(ctx));
        Check("random cond only branch 1 when gold=10", picked.SetEquals(new[] { 1 }));

        var rndNone = new RandomBranchNode
        {
            cases = new List<BranchCase>
            {
                new BranchCase
                {
                    label = "impossible",
                    conditions = new List<DialogueCondition>
                    { new IntCompareCondition { key = "gold", op = CompareOperator.GreaterOrEqual, value = 1000000 } }
                }
            }
        };
        RandomBranchNode.ResetSeed(1);
        Check("random all-cond-fail -> default port (= cases.Count)", rndNone.Evaluate(ctx) == 1);

        // SingleEventNode 摘要 + 播放链路
        Check("blank summary", new SingleEventNode().GetSummary() == "(未选择事件类型)");
        Check("event summary",
            new SingleEventNode { eventData = new SetIntEvent { key = "gold", value = 77 } }.GetSummary() == "gold = 77");

        // 5. 节点深拷贝(DialogueNodeCloneUtil,Ctrl+C/V 的核心)
        var srcTalk = new DialogueNode
        {
            guid = "src-1",
            speakerName = "村长",
            dialogueText = "原文"
        };
        var copyTalk = (DialogueNode)DialogueSystem.Editor.DialogueNodeCloneUtil.Clone(srcTalk);
        Check("clone dialogue fields equal",
            copyTalk.speakerName == "村长" && copyTalk.dialogueText == "原文");
        Check("clone has new guid", copyTalk.guid != srcTalk.guid && !string.IsNullOrEmpty(copyTalk.guid));
        copyTalk.dialogueText = "改过的克隆";
        Check("clone independent of source (string field)", srcTalk.dialogueText == "原文");

        var srcEvent = new SingleEventNode
        {
            guid = "src-2",
            eventData = new SetIntEvent { key = "gold", value = 5 }
        };
        var copyEvent = (SingleEventNode)DialogueSystem.Editor.DialogueNodeCloneUtil.Clone(srcEvent);
        Check("clone single-event: eventData deep copied",
            copyEvent.eventData != null && copyEvent.eventData != srcEvent.eventData
            && copyEvent.eventData is SetIntEvent);
        ((SetIntEvent)copyEvent.eventData).value = 999;
        Check("clone single-event: event fields independent",
            ((SetIntEvent)srcEvent.eventData).value == 5);
        var copyEvent2 = DialogueSystem.Editor.DialogueNodeCloneUtil.Clone(copyEvent);
        Check("re-clone for multi-paste: new guid each time", copyEvent2.guid != copyEvent.guid);

        var srcChoice = new ChoiceNode
        {
            guid = "src-3",
            choices =
            {
                new ChoiceOption
                {
                    choiceText = "去商店",
                    conditionMode = ConditionCombineMode.Any,
                    conditions = { new IntCompareCondition { key = "gold", op = CompareOperator.GreaterOrEqual, value = 50 } }
                },
                new ChoiceOption { choiceText = "离开" }
            }
        };
        var copyChoice = (ChoiceNode)DialogueSystem.Editor.DialogueNodeCloneUtil.Clone(srcChoice);
        Check("clone choice: list deep copied",
            copyChoice.choices.Count == 2 && copyChoice.choices[0].choiceText == "去商店"
            && copyChoice.choices[1].choiceText == "离开");
        Check("clone choice: conditionMode preserved",
            copyChoice.choices[0].conditionMode == ConditionCombineMode.Any);
        var srcCond = (IntCompareCondition)srcChoice.choices[0].conditions[0];
        var copyCond = (IntCompareCondition)copyChoice.choices[0].conditions[0];
        Check("clone choice: nested condition deep copied",
            copyCond != srcCond && copyCond.key == "gold" && copyCond.value == 50
            && copyCond.op == CompareOperator.GreaterOrEqual);
        copyChoice.choices[0].choiceText = "改";
        srcCond.value = 500;
        Check("clone choice: modifications do not leak",
            srcChoice.choices[0].choiceText == "去商店" && copyCond.value == 50);

        // 6. 播放链路

        var asset = (DialogueGraphAsset)System.Runtime.Serialization.FormatterServices
            .GetUninitializedObject(typeof(DialogueGraphAsset));
        asset.nodes = new List<DialogueNodeData>();
        asset.links = new List<NodeLink>();
        var start = new StartNode { guid = "start" };
        var evt = new SingleEventNode { guid = "evt", eventData = new SetFlagEvent { key = "opened", value = true } };
        var talk = new DialogueNode { guid = "talk", speakerName = "chief", dialogueText = "hi" };
        var choice = new ChoiceNode
        {
            guid = "choice",
            choices = new List<ChoiceOption>
            {
                new ChoiceOption
                {
                    choiceText = "rich",
                    conditions = new List<DialogueCondition>
                    { new IntCompareCondition { key = "gold", op = CompareOperator.GreaterOrEqual, value = 50 } }
                },
                new ChoiceOption { choiceText = "bye" }
            }
        };
        var end = new EndNode { guid = "end" };
        asset.nodes.AddRange(new DialogueNodeData[] { start, evt, talk, choice, end });
        asset.links.AddRange(new[]
        {
            new NodeLink { fromGuid = "start", fromPort = 0, toGuid = "evt" },
            new NodeLink { fromGuid = "evt", fromPort = 0, toGuid = "talk" },
            new NodeLink { fromGuid = "talk", fromPort = 0, toGuid = "choice" },
            new NodeLink { fromGuid = "choice", fromPort = 0, toGuid = "end" },
            new NodeLink { fromGuid = "choice", fromPort = 1, toGuid = "end" }
        });

        var ctx2 = new DialogueContext(); ctx2.Blackboard.SetInt("gold", 10);
        var player = new DialoguePlayer();
        List<DialoguePlayer.ChoiceInfo> shown = null; Action<int> chooser = null;
        Action cont = null; bool ended = false;
        player.OnDialogue += (s, t, c) => cont = c;
        player.OnChoice += (l, cb) => { shown = l; chooser = cb; };
        player.OnEnd += () => ended = true;
        player.Play(asset, ctx2);

        Check("event executed in playback", ctx2.Blackboard.GetBool("opened"));
        cont();
        Check("one visible choice after filter", shown != null && shown.Count == 1);
        Check("keeps original index 1", shown != null && shown[0].choiceIndex == 1);
        chooser(shown[0].choiceIndex);
        Check("reached End -> OnEnd", ended);

        Console.WriteLine(failures == 0 ? "ALL_PASS" : $"FAILURES={failures}");
        Environment.ExitCode = failures == 0 ? 0 : 1;
    }
}
SMOKE_EOF

"$MONO" "$CSC" -nologo -target:exe -out:"$TMPW/smoke.exe" -langversion:latest -r:"$NS" \
  "$TMPW/stubs.cs" "$TMPW/smoke.cs" "$PROJ/Editor/DialogueNodeCloneUtil.cs" "${RT_SRC[@]}" \
  || { echo "FAIL: 断言程序编译错误"; exit 1; }
"$MONO" "$TMPW/smoke.exe"
RC=$?
[ "$RC" -eq 0 ] || { echo "FAIL: 断言未全部通过 (exit=$RC)"; exit 1; }
echo "[2/2] LOGIC ASSERTIONS: OK"
# ── ③ NPOI Excel 往返冒烟(真实 NPOI DLL 生成 xlsx 再读回断言) ──
# 覆盖 DialogueTextExporter 依赖的 NPOI 2.1.1 API 面(XSSF 写 + 读回);
# NPOI 2.1.1 为旧 API:Boldweight(2.5+ 才是 IsBold),升级 NPOI 后此段会提示改法。
cat > "$TMPW/npoi_smoke.cs" <<'NPOI_EOF'
using System;
using System.IO;
using NPOI.SS.UserModel;
using NPOI.XSSF.UserModel;

static class NpoiSmoke
{
    static int failures;
    static void Check(string name, bool ok)
    {
        Console.WriteLine((ok ? "PASS " : "FAIL ") + "[npoi] " + name);
        if (!ok) failures++;
    }

    static void Main(string[] args)
    {
        var path = System.IO.Path.Combine(args[0], "npoi_smoke.xlsx");
        // 写:表头加粗 + 中文数据行(与 DialogueTextExporter 相同 API 面)
        IWorkbook wb = new XSSFWorkbook();
        var sheet = wb.CreateSheet("对话文本");
        var font = wb.CreateFont();
        font.Boldweight = (short)FontBoldWeight.Bold;
        var style = wb.CreateCellStyle();
        style.SetFont(font);
        var header = sheet.CreateRow(0);
        header.CreateCell(0).SetCellValue("类型");
        header.CreateCell(0).CellStyle = style;
        header.CreateCell(1).SetCellValue("文本内容");
        var data = sheet.CreateRow(1);
        data.CreateCell(0).SetCellValue("对话");
        data.CreateCell(1).SetCellValue("你好,世界!多行\n第二行");
        sheet.SetColumnWidth(1, 80 * 256);
        sheet.CreateFreezePane(0, 1);
        using (var fs = new FileStream(path, FileMode.Create, FileAccess.Write))
            wb.Write(fs);

        // 读回断言
        using (var fs = new FileStream(path, FileMode.Open, FileAccess.Read))
        {
            var read = new XSSFWorkbook(fs);
            var s = read.GetSheet("对话文本");
            Check("sheet 名读回一致", s != null);
            Check("表头读回一致", s.GetRow(0).GetCell(1).StringCellValue == "文本内容");
            Check("中文+换行数据读回一致",
                s.GetRow(1).GetCell(1).StringCellValue == "你好,世界!多行\n第二行");
            Check("表头加粗样式生效",
                s.GetRow(0).GetCell(0).CellStyle.GetFont(read).Boldweight == (short)FontBoldWeight.Bold);
        }
        Console.WriteLine(failures == 0 ? "NPOI_ALL_PASS" : $"NPOI_FAILURES={failures}");
        Environment.ExitCode = failures == 0 ? 0 : 1;
    }
}
NPOI_EOF

NPOI_DIR_M="$(cygpath -m "$NPOI_DIR")"
"$MONO" "$CSC" -nologo -target:exe -out:"$TMPW/npoi_smoke.exe" -langversion:latest \
  -r:"$NS" -r:"$NPOI_DIR_M/NPOI.dll" -r:"$NPOI_DIR_M/NPOI.OOXML.dll" \
  -r:"$NPOI_DIR_M/NPOI.OpenXml4Net.dll" -r:"$NPOI_DIR_M/NPOI.OpenXmlFormats.dll" \
  -r:"$NPOI_DIR_M/ICSharpCode.SharpZipLib.dll" "$TMPW/npoi_smoke.cs" \
  || { echo "FAIL: NPOI 冒烟编译错误"; exit 1; }
MONO_PATH="$NPOI_DIR_M" "$MONO" "$TMPW/npoi_smoke.exe" "$TMPW" \
  || { echo "FAIL: NPOI 往返断言未通过"; exit 1; }
echo "[3/3] NPOI XLSX ROUND-TRIP: OK"

echo "VERIFY: PASS (编译 OK + 断言 OK + NPOI Excel 往返 OK;GUI 交互需在真实编辑器中人工验证)"
