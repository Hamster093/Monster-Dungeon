/****************************************************
    文件：ButtonExpander.cs
	作者：DADI
    邮箱: 1581507659@qq.com
    日期：2026/10/12 16:36:21
	功能：加载 JSON 时，把 $ref 展开成完整按钮，其它字段不动。
*****************************************************/

using Newtonsoft.Json.Linq;

public static class ButtonExpander
{
    public static string Expand(string json)
    {
        JObject root;
        try { root = JObject.Parse(json); }
        catch (System.Exception e)
        {
            UnityEngine.Debug.LogError($"[Expander] 解析失败: {e}");
            return json;
        }

        var buttons = root["buttons"] as JObject;
        var templates = root["templates"] as JObject;
        var nodes = root["nodes"] as JArray;

        if (nodes != null)
        {
            // 第 1 遍：展开模板（use + params → lines + options）
            if (templates != null)
            {
                foreach (var tok in nodes)
                {
                    var node = tok as JObject;
                    if (node == null || node["use"] == null) continue;

                    string tplKey = (string)node["use"];
                    var tpl = templates[tplKey] as JObject;
                    if (tpl == null)
                    {
                        UnityEngine.Debug.LogWarning($"[Expander] 找不到模板: {tplKey}");
                        continue;
                    }

                    var paramObj = node["params"] as JObject;
                    var expanded = SubstituteParams(tpl, paramObj) as JObject;

                    node.Remove("use");
                    node.Remove("params");
                    foreach (var p in expanded.Properties())
                    {
                        if (node[p.Name] != null) continue;   // 节点已有 → 保留
                        node[p.Name] = p.Value;
                    }
                }
            }

            // 第 2 遍：展开 $ref 按钮
            if (buttons != null)
            {
                foreach (var tok in nodes)
                {
                    var node = tok as JObject;
                    if (node == null) continue;
                    var opts = node["options"] as JArray;
                    if (opts == null) continue;

                    for (int i = 0; i < opts.Count; i++)
                    {
                        var opt = opts[i] as JObject;
                        if (opt == null) continue;

                        var refTok = opt["$ref"];
                        if (refTok == null) continue;

                        var btn = buttons[(string)refTok] as JObject;
                        if (btn == null) continue;

                        var merged = new JObject();
                        foreach (var p in btn.Properties()) merged[p.Name] = p.Value;
                        foreach (var p in opt.Properties())
                        {
                            if (p.Name == "$ref") continue;
                            merged[p.Name] = p.Value;
                        }
                        opts[i] = merged;
                    }
                }
            }
        }

        root.Remove("buttons");
        root.Remove("templates");
        return root.ToString(Newtonsoft.Json.Formatting.None);
    }

    // 递归替换 "$param" 为实际值
    static JToken SubstituteParams(JToken token, JObject paramObj)
    {
        if (token is JValue v && v.Type == JTokenType.String)
        {
            string s = (string)v;
            if (!string.IsNullOrEmpty(s) && s[0] == '$' && paramObj != null && paramObj[s] != null)
                return paramObj[s].DeepClone();
            return v.DeepClone();
        }
        if (token is JObject obj)
        {
            var result = new JObject();
            foreach (var p in obj.Properties())
                result[p.Name] = SubstituteParams(p.Value, paramObj);
            return result;
        }
        if (token is JArray arr)
        {
            var result = new JArray();
            foreach (var item in arr) result.Add(SubstituteParams(item, paramObj));
            return result;
        }
        return token.DeepClone();
    }
}