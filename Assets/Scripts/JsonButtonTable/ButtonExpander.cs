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
    /// <summary>把 $ref 展开成完整按钮，其它字段不动。</summary>
    public static string Expand(string json)
    {
        var root = JObject.Parse(json);
        if (root["buttons"] is not JObject buttons) return json;

        if (root["nodes"] is JArray nodes)
        {
            foreach (var token in nodes)
            {
                if (token is not JObject node) continue;
                if (node["options"] is not JArray options) continue;

                for (int i = 0; i < options.Count; i++)
                {
                    if (options[i] is not JObject opt) continue;
                    var refToken = opt["$ref"];
                    if (refToken == null) continue;

                    var btn = buttons[(string)refToken] as JObject;
                    if (btn == null) continue;

                    var merged = new JObject();
                    foreach (var p in btn.Properties()) merged[p.Name] = p.Value;
                    foreach (var p in opt.Properties())
                    {
                        if (p.Name == "$ref") continue;
                        merged[p.Name] = p.Value;   // targetNodeId 覆盖
                    }
                    options[i] = merged;
                }
            }
        }
        return root.ToString(Newtonsoft.Json.Formatting.None);
    }
}
