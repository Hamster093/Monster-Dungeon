/****************************************************
    文件：ButtonExtractor.cs
	作者：DADI
    邮箱: 1581507659@qq.com
    日期：2026/10/12 16:51:02
	功能：一键转换器 转换json文件中对话选项的文本为按钮引用
*****************************************************/

using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;
using Newtonsoft.Json.Linq;

public static class ButtonExtractor
{
    [MenuItem("Tools/提取对话按钮")]
    static void Extract()
    {
        string src = EditorUtility.OpenFilePanel("选择原始 JSON", "", "json");
        if (string.IsNullOrEmpty(src)) return;

        string text = File.ReadAllText(src);
        JObject root = JObject.Parse(text);

        var textToKey = new Dictionary<string, string>();
        var buttons = new JObject();
        foreach (var kv in ButtonTable.All)
        {
            textToKey[kv.Value.text] = kv.Key;
            var b = new JObject { ["text"] = kv.Value.text };
            if (!string.IsNullOrEmpty(kv.Value.callbackKey))
                b["callbackKey"] = kv.Value.callbackKey;
            buttons[kv.Key] = b;
        }

        var nodes = root["nodes"] as JArray;
        int converted = 0;

        if (nodes != null)
        {
            for (int n = 0; n < nodes.Count; n++)
            {
                var node = nodes[n] as JObject;
                if (node == null) continue;

                var options = node["options"] as JArray;
                if (options == null) continue;

                var pending = new List<KeyValuePair<int, JObject>>();

                for (int i = 0; i < options.Count; i++)
                {
                    var opt = options[i] as JObject;
                    if (opt == null) continue;

                    string t = opt["text"] != null ? (string)opt["text"] : null;
                    if (t == null) continue;

                    string key;
                    if (!textToKey.TryGetValue(t, out key)) continue;

                    var newOpt = new JObject { ["$ref"] = key };
                    foreach (var p in opt.Properties())
                    {
                        if (p.Name == "text" || p.Name == "callbackKey") continue;
                        newOpt[p.Name] = p.Value;
                    }
                    pending.Add(new KeyValuePair<int, JObject>(i, newOpt));
                }

                foreach (var pair in pending)
                {
                    options[pair.Key] = pair.Value;
                    converted++;
                }
            }
        }

        root["buttons"] = buttons;

        string dst = Path.Combine(
            Path.GetDirectoryName(src),
            Path.GetFileNameWithoutExtension(src) + "_converted.json");
        File.WriteAllText(dst, root.ToString(Newtonsoft.Json.Formatting.Indented));
        AssetDatabase.Refresh();
        Debug.Log($"✅ 已转换 {converted} 个选项\n输出: {dst}");
    }
}