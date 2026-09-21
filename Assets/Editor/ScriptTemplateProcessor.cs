/****************************************************
    文件：ScriptTemplateProcessor.cs
	作者：DADI
    邮箱: 1581507659@qq.com
    日期：2026.9.21
	功能：创建脚本时自动替换占位符
*****************************************************/

using System.IO;
using UnityEditor;
using UnityEngine;

public class ScriptTemplateProcessor : UnityEditor.AssetModificationProcessor
{
    // Unity 创建资源时自动回调
    public static void OnWillCreateAsset(string path)
    {
        // 仅处理 .cs.meta 文件（Unity 先创建 meta，再创建实际文件）
        if (!path.EndsWith(".meta")) return;

        string csPath = path.Replace(".meta", "");
        if (!File.Exists(csPath)) return;

        string content = File.ReadAllText(csPath);

        // 替换占位符
        content = content.Replace("#DATE#", System.DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss"));
        content = content.Replace("#AUTHOR#", "DADI");
        content = content.Replace("#EMAIL#", "1581507659@qq.com");

        File.WriteAllText(csPath, content);
        AssetDatabase.ImportAsset(csPath);
    }
}