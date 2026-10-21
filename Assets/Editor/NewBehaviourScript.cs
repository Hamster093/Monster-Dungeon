using UnityEngine;
using UnityEditor;
using System.IO;

public class ScriptAutoSorter : EditorWindow
{
    [MenuItem("Tools/一键重构Scripts文件夹")]
    public static void SortScripts()
    {
        string root = "Assets/Scripts";

        // 1. 自动创建目录骨架
        string[] folders = {
            "01_Core",
            "02_Systems/Gameplay", "02_Systems/Narrative", "02_Systems/Services", "02_Systems/DailySettlement",
            "03_Board",
            "04_UI/Core", "04_UI/Panels", "04_UI/Components", "04_UI/Components/Toast", "04_UI/Effects",
            "05_Data/State", "05_Data/Items", "05_Data/Archive", "05_Data/Config",
            "06_Events",
            "07_Utils/Audio", "99_Test"
        };

        foreach (var folder in folders)
        {
            string fullPath = $"{root}/{folder}";
            if (!AssetDatabase.IsValidFolder(fullPath))
            {
                string[] parts = folder.Split('/');
                string current = root;
                foreach (var part in parts)
                {
                    if (!AssetDatabase.IsValidFolder($"{current}/{part}"))
                        AssetDatabase.CreateFolder(current, part);
                    current = $"{current}/{part}";
                }
            }
        }

        // 2. 查找所有脚本并安全移动
        string[] guids = AssetDatabase.FindAssets("t:Script", new[] { root });
        int count = 0;

        foreach (string guid in guids)
        {
            string path = AssetDatabase.GUIDToAssetPath(guid);
            string fileName = Path.GetFileNameWithoutExtension(path);
            string targetFolder = "";

            // --- 核心文件映射 ---
            if (fileName == "GameBootstrap" || fileName == "DontDestroy" || fileName == "StartGame" || fileName == "StartUIPanelManager" || fileName == "SystemManager")
                targetFolder = "01_Core";

            // --- 系统层 ---
            else if (path.Contains("/03System/")) targetFolder = "02_Systems/Gameplay";
            else if (path.Contains("/Dialogue/")) targetFolder = "02_Systems/Narrative";
            else if (path.Contains("/04Service/")) targetFolder = "02_Systems/Services";
            else if (path.Contains("/DailySettlement/")) targetFolder = "02_Systems/DailySettlement";

            // --- 棋盘 ---
            else if (path.Contains("/qipan/")) targetFolder = "03_Board";

            // --- UI层 ---
            else if (fileName == "UiManager") targetFolder = "04_UI/Core";
            else if (path.Contains("/Toast/")) targetFolder = "04_UI/Components/Toast";
            else if (fileName == "EndingCredits" || fileName == "GuideHelpOverlay" || fileName == "PresentationEffects") targetFolder = "04_UI/Effects";
            else if (fileName == "GuideTopicAnchor" || fileName == "UIHoverHint" || fileName == "TypewriterText" || fileName == "RadarChart" || fileName == "ScreenFader" || fileName == "ITypewriterEffect") targetFolder = "04_UI/Components";
            else if (path.Contains("/02UI/Panel/") || path.Contains("/02UI/TaklPanle/") || fileName.EndsWith("Panel")) targetFolder = "04_UI/Panels";

            // --- 数据层 ---
            else if (path.Contains("/Back/Item/")) targetFolder = "05_Data/Items";
            else if (path.Contains("/Mode/")) targetFolder = "05_Data/State";
            else if (path.Contains("/06Config/SaveAndLoad/")) targetFolder = "05_Data/Archive";
            else if (path.Contains("/06Config/Event/")) targetFolder = "06_Events"; // 先处理 Config 下的事件
            else if (path.Contains("/06Config/")) targetFolder = "05_Data/Config";

            // --- 事件与工具 ---
            else if (path.Contains("/Event/")) targetFolder = "06_Events";
            else if (path.Contains("/Audio/")) targetFolder = "07_Utils/Audio";
            else if (path.Contains("/Test/")) targetFolder = "99_Test";

            // 执行安全移动
            if (!string.IsNullOrEmpty(targetFolder))
            {
                string targetPath = $"{root}/{targetFolder}/{Path.GetFileName(path)}";
                if (path != targetPath)
                {
                    string error = AssetDatabase.MoveAsset(path, targetPath);
                    if (string.IsNullOrEmpty(error))
                    {
                        count++;
                    }
                    else
                    {
                        Debug.LogError($"移动失败: {fileName} -> {targetPath} 原因: {error}");
                    }
                }
            }
        }

        AssetDatabase.Refresh();
        Debug.Log($"<color=green>整理完成！共安全移动了 {count} 个文件，所有预制体引用均未损坏。</color>");
        EditorUtility.DisplayDialog("整理完成", $"安全移动了 {count} 个脚本！\n没有任何引用丢失，去看看你的 Project 窗口吧！", "OK");
    }
}