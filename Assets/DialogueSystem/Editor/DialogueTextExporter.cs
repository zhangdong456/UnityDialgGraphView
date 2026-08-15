using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using NPOI.SS.UserModel;
using NPOI.XSSF.UserModel;
using UnityEditor;
using UnityEngine;

namespace DialogueSystem.Editor
{
    /// <summary>
    /// 把某个文件夹内所有对话资产的文本导出为 Excel(.xlsx):
    /// 对话节点的正文 + 选择节点每个选项的文本;相同文本自动去重(按去除首尾空白后的全文比较)。
    /// 菜单: Tools → Dialogue System → 导出对话文本到 Excel。
    /// 依赖 Assets/Plugins/NPOI(仅编辑器使用;Runtime 不依赖任何第三方库)。
    /// </summary>
    public static class DialogueTextExporter
    {
        class RowData
        {
            public string type;     // 对话 / 选项
            public string speaker;  // 对话节点的说话者(选项为空)
            public string text;     // 文本内容(已去重)
            public string source;   // 来源资产名
        }

        [MenuItem("Tools/Dialogue System/导出对话文本到 Excel")]
        public static void Export()
        {
            // 1. 选择文件夹(必须是工程 Assets 内的路径,AssetDatabase 才能检索)
            var absFolder = EditorUtility.OpenFolderPanel("选择包含对话资产的文件夹", "Assets", "");
            if (string.IsNullOrEmpty(absFolder)) return;

            var folder = FileUtil.GetProjectRelativePath(absFolder.Replace('\\', '/'));
            if (string.IsNullOrEmpty(folder)
                || !(folder == "Assets" || folder.StartsWith("Assets/", StringComparison.OrdinalIgnoreCase)))
            {
                EditorUtility.DisplayDialog("无法导出",
                    "请选择工程 Assets 目录内的文件夹。\n当前选择:" + absFolder, "确定");
                return;
            }

            // 2. 检索文件夹(含子文件夹)内全部对话资产
            var guids = AssetDatabase.FindAssets("t:DialogueGraphAsset", new[] { folder });
            if (guids.Length == 0)
            {
                EditorUtility.DisplayDialog("没有对话资产",
                    "文件夹 " + folder + " 内没有找到 Dialogue Graph 资产。", "确定");
                return;
            }

            // 3. 提取文本并去重
            var rows = new List<RowData>();
            var seen = new HashSet<string>();
            int totalTexts = 0;
            foreach (var guid in guids)
            {
                var path = AssetDatabase.GUIDToAssetPath(guid);
                var asset = AssetDatabase.LoadAssetAtPath<DialogueGraphAsset>(path);
                if (asset == null || asset.nodes == null) continue;

                foreach (var node in asset.nodes)
                {
                    if (node is DialogueNode d)
                    {
                        totalTexts++;
                        AddRow(rows, seen, "对话", d.speakerName, d.dialogueText, asset.name);
                    }
                    else if (node is ChoiceNode c && c.choices != null)
                    {
                        foreach (var option in c.choices)
                        {
                            totalTexts++;
                            AddRow(rows, seen, "选项", null, option == null ? null : option.choiceText, asset.name);
                        }
                    }
                }
            }

            if (rows.Count == 0)
            {
                EditorUtility.DisplayDialog("没有可导出的文本",
                    "扫描了 " + guids.Length + " 个对话资产,但没有找到任何对话正文或选项文本。", "确定");
                return;
            }

            // 4. 选择保存位置
            var projectRoot = Directory.GetParent(Application.dataPath).FullName;
            var defaultName = "DialogueTexts_" + DateTime.Now.ToString("yyyyMMdd_HHmm");
            var savePath = EditorUtility.SaveFilePanel("保存 Excel 文件", projectRoot, defaultName, "xlsx");
            if (string.IsNullOrEmpty(savePath)) return;

            try
            {
                WriteExcel(savePath, rows);
            }
            catch (Exception e)
            {
                Debug.LogError("[DialogueSystem] 导出 Excel 失败:" + e);
                EditorUtility.DisplayDialog("导出失败", e.Message, "确定");
                return;
            }

            // 保存到 Assets 内时刷新资产数据库,让文件立刻可见
            if (savePath.Replace('\\', '/').StartsWith(Application.dataPath.Replace('\\', '/'), StringComparison.OrdinalIgnoreCase))
                AssetDatabase.Refresh();

            Debug.Log("[DialogueSystem] 对话文本导出完成:\n" + savePath
                + "\n扫描资产 " + guids.Length + " 个,文本 " + totalTexts + " 条,去重后导出 " + rows.Count
                + " 条(剔除重复 " + (totalTexts - rows.Count) + " 条)。");
            EditorUtility.DisplayDialog("导出完成",
                "扫描资产 " + guids.Length + " 个\n文本 " + totalTexts + " 条\n去重后导出 " + rows.Count
                + " 条(剔除重复 " + (totalTexts - rows.Count) + " 条)\n\n" + savePath, "确定");
        }

        /// <summary>空白文本跳过;按去空白后的全文去重;通过则加入导出行。</summary>
        static void AddRow(List<RowData> rows, HashSet<string> seen,
            string type, string speaker, string text, string source)
        {
            var trimmed = text == null ? string.Empty : text.Trim();
            if (trimmed.Length == 0) return;
            if (!seen.Add(trimmed)) return;
            rows.Add(new RowData
            {
                type = type,
                speaker = speaker ?? string.Empty,
                text = trimmed,
                source = source ?? string.Empty
            });
        }

        static void WriteExcel(string path, List<RowData> rows)
        {
            IWorkbook workbook = new XSSFWorkbook();
            var sheet = workbook.CreateSheet("对话文本");

            // 表头加粗(本工程 NPOI 2.1.1 的 API:short Boldweight = FontBoldWeight.Bold;
            // 2.5+ 版本才有 IsBold。若日后升级 NPOI 报错,改回 headerFont.IsBold = true 即可)
            var headerFont = workbook.CreateFont();
            headerFont.Boldweight = (short)NPOI.SS.UserModel.FontBoldWeight.Bold;
            var headerStyle = workbook.CreateCellStyle();
            headerStyle.SetFont(headerFont);
            var header = sheet.CreateRow(0);
            var titles = new[] { "序号", "类型", "说话者", "文本内容", "来源资产" };
            for (int i = 0; i < titles.Length; i++)
            {
                var cell = header.CreateCell(i);
                cell.SetCellValue(titles[i]);
                cell.CellStyle = headerStyle;
            }

            // 数据行(首列文本格式,避免大序号被当数字)
            for (int r = 0; r < rows.Count; r++)
            {
                var row = sheet.CreateRow(r + 1);
                row.CreateCell(0).SetCellValue(r + 1);
                row.CreateCell(1).SetCellValue(rows[r].type);
                row.CreateCell(2).SetCellValue(rows[r].speaker);
                row.CreateCell(3).SetCellValue(rows[r].text);
                row.CreateCell(4).SetCellValue(rows[r].source);
            }

            // 列宽(单位 1/256 字符宽)
            sheet.SetColumnWidth(0, 8 * 256);
            sheet.SetColumnWidth(1, 10 * 256);
            sheet.SetColumnWidth(2, 18 * 256);
            sheet.SetColumnWidth(3, 80 * 256);
            sheet.SetColumnWidth(4, 24 * 256);
            sheet.CreateFreezePane(0, 1); // 首行冻结

            using (var fs = new FileStream(path, FileMode.Create, FileAccess.Write))
            {
                workbook.Write(fs);
            }
        }
    }
}
