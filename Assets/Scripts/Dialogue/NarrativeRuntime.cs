using System;
using System.Collections.Generic;
using Ink.Runtime;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

/// <summary>
/// Ink 只负责原稿的流程、条件与变量，台词仍交给原来的 DialogueController/TalkPanel。
/// 不使用上一份带 _runtime 字段的转换稿，也不修改 DialogueData 的格式。
/// 本类不依赖场景，便于直接测试整段剧情和存档恢复。
/// </summary>
public sealed class NarrativeRuntime
{
    public sealed class PendingCheckException : Exception { }
    private string _prepared;
    public const string YieldLine = "__NARRATIVE_YIELD__";
    public Story Story { get; private set; }
    public string Speaker { get; set; } = "旁白";
    public string LastAction { get; private set; }
    public string LastLineId { get; private set; }
    public Action<string> OnAction;
    public Action<string,Ink.ErrorType> OnError;
    public Func<string, string, int, bool> OnCheck;
    public Func<bool> ShouldPause;
    public bool Finished => !Story.canContinue && Story.currentChoices.Count == 0;
    private readonly Dictionary<string, string> _featurePaths = new Dictionary<string, string>();
    public readonly Dictionary<string, string> BackgroundTexts = new Dictionary<string, string>();

    public NarrativeRuntime(string source)
    {
        var named = (JObject)((JArray)JObject.Parse(source)["root"]).Last;
        foreach (var property in named.Properties())
        {
            // 功能入口来自第一版接待菜单的固定槽位，并核对原稿所在容器，不能扫描所有【】。
            if (property.Name.EndsWith("_RECEPTION", StringComparison.OrdinalIgnoreCase))
            {
                var body = (JArray)property.Value[0];
                int slot = 0;
                for (int i = 0; i < body.Count; i++)
                    if (body[i] is JObject cp && cp["*"] != null)
                    {
                        string key = slot == 0 ? "open_commission_panel" : slot == 2 ? "open_backpack" : null;
                        if (key != null) _featurePaths[property.Name + ".0." + i] = key;
                        slot++;
                    }
            }
            if (property.Name.StartsWith("LENS_") && property.Name.Length == 7)
            {
                ExtractBackground((JArray)property.Value, property.Name.Substring(5));
                if (named[property.Name + "_DONE"] is JArray done && BackgroundTexts.ContainsKey(property.Name.Substring(5) + ":full"))
                {
                    var hints = new List<string>();
                    foreach (var token in done.Descendants())
                        if (token.Type == JTokenType.String && (token.ToString().StartsWith("^主属性：") || token.ToString().StartsWith("^副属性：") || token.ToString().StartsWith("^继任培养方向："))) hints.Add(token.ToString().Substring(1));
                    BackgroundTexts[property.Name.Substring(5) + ":full"] += "\n\n" + string.Join("\n", hints);
                }
            }
        }
        _prepared=Prepare(source);
        BindStory();
    }
    private void BindStory()
    {
        Story = new Story(_prepared);
        Story.onError+=(message,type)=>OnError?.Invoke(message,type);
        // 这两个函数会扣款/掷骰，因此必须关闭 Ink 的预读执行，避免重复触发。
        Story.BindExternalFunction<string>("game_action", (Action<string>)(tag =>
        {
            if(tag.StartsWith("action:portrait_line id=",StringComparison.Ordinal)) { LastLineId=tag.Substring("action:portrait_line id=".Length); return; }
            LastAction = tag;
            OnAction?.Invoke(tag);
        }), false);
        Story.BindExternalFunction<string, string, int>("game_check", (kind, actor, bonus) =>
            (object)(OnCheck != null && OnCheck(kind, actor, bonus)), false);
    }

    /// <summary>只把原稿的 action 标签接到游戏；保留全部原始容器位置及跳转地址。</summary>
    public static string Prepare(string source)
    {
        var root = JObject.Parse(source);
        var containers = (JObject)((JArray)root["root"]).Last;
        foreach (var property in containers.Properties())
        {
            if (property.Name.EndsWith("_COMMISSION", StringComparison.OrdinalIgnoreCase))
            {
                var body = (JArray)property.Value[0];
                int begin = -1;
                for (int i = 0; i < body.Count; i++)
                {
                    if ((string)(body[i] as JValue) == "^委托名称：") begin = i;
                    if (begin < 0) continue;
                    if ((string)(body[i] as JValue) == "ev") break; // 原稿确认/返回选项保留。
                    body[i] = i == begin ? ActionCall("action:panel kind=commission") : (JToken)"nop";
                }
            }
            // 调查正文属于左侧档案，原稿的扣道具、揭示状态和隐藏条件仍照常执行。
            if (property.Name.StartsWith("LENS_") && (property.Name.Length == 7 || property.Name.EndsWith("_DONE")))
                HideBackgroundText((JArray)property.Value);
        }
        RewriteContainer((JArray)root["root"]);
        MarkPortraitLines((JArray)root["root"]);
        var named = (JObject)((JArray)root["root"]).Last;
        named["HIDDEN_BATTLE"]=new JArray(new JObject{["->"]="HIDDEN_CANCEL"},new JObject{["#f"]=3});
        // 原稿 resolve_check 恒返回 preview_success。实装时接现有游戏检定。
        named["resolve_check"] = new JArray(
            new JObject { ["temp="] = "bonus" }, new JObject { ["temp="] = "actor" },
            new JObject { ["temp="] = "kind" }, "ev", new JObject { ["VAR?"] = "kind" },
            new JObject { ["VAR?"] = "actor" }, new JObject { ["VAR?"] = "bonus" },
            new JObject { ["x()"] = "game_check", ["exArgs"] = 3 }, "/ev", "~ret",
            new JObject { ["#f"] = 1 });
        return root.ToString(Formatting.None);
    }

    // 在原字符串位置内标记演出句，不改变外层数字跳转；选项求值和剧情标签不参与。
    private static void MarkPortraitLines(JArray container,string path="",int depth=0,bool tag=false)
    {
        for(int i=0;i<container.Count-1;i++) {
            string point=string.IsNullOrEmpty(path)?i.ToString():path+"."+i;
            if(container[i] is JArray child){MarkPortraitLines(child,point,depth,tag);continue;}
            if(container[i].Type!=JTokenType.String)continue;
            string s=container[i].ToString();
            if(s=="ev")depth++; else if(s=="/ev")depth=Math.Max(0,depth-1); else if(s=="#")tag=true;else if(s=="/#")tag=false;
            else if(depth==0&&!tag&&s.StartsWith("^",StringComparison.Ordinal)&&s!="^"+YieldLine&&!s.StartsWith("^action:",StringComparison.Ordinal))
                container[i]=new JArray("ev","str","^action:portrait_line id="+point,"/str",new JObject{["x()"]="game_action",["exArgs"]=1},"pop","/ev",s,null);
        }
        if(container.Last is JObject meta)foreach(var p in meta.Properties())if(p.Value is JArray named)MarkPortraitLines(named,string.IsNullOrEmpty(path)?p.Name:path+"."+p.Name);
    }

    private static JArray ActionCall(string tag) => new JArray("ev", "str", "^" + tag, "/str", 
        new JObject { ["x()"] = "game_action", ["exArgs"] = 1 }, "pop", "/ev", "^" + YieldLine, "\n", null);

    private void ExtractBackground(JArray container, string actor)
    {
        var paragraphs = new List<string>();
        string level = null;
        foreach (var token in container)
        {
            if (token is JArray child) ExtractBackground(child, actor);
            if (token is JObject meta)
                foreach (var p in meta.Properties()) if (p.Value is JArray branch) ExtractBackground(branch, actor);
            if (token.Type != JTokenType.String) continue;
            string text = token.ToString();
            if (text.StartsWith("^往事记录："))
            {
                level = text.Contains("部分缺失") ? "partial" : "full";
                paragraphs.Clear();
            }
            if (level != null && text.StartsWith("^") && !text.StartsWith("^action:"))
            {
                paragraphs.Add(text.Substring(1));
                BackgroundTexts[actor + ":" + level] = string.Join("\n\n", paragraphs);
            }
        }
    }

    private static void HideBackgroundText(JArray container)
    {
        bool tag = false;
        for (int i = 0; i < container.Count - 1; i++)
        {
            if (container[i] is JArray child) { HideBackgroundText(child); continue; }
            if (container[i].Type != JTokenType.String) continue;
            string text = container[i].ToString();
            if (text == "#") tag = true;
            else if (text == "/#") tag = false;
            else if (!tag && text.StartsWith("^")) container[i] = "nop";
        }
        if (container.Last is JObject meta)
            foreach (var p in meta.Properties()) if (p.Value is JArray branch) HideBackgroundText(branch);
    }

    private static void RewriteContainer(JArray container, string path = "")
    {
        for (int i = 0; i < container.Count - 1; i++)
        {
            string point = string.IsNullOrEmpty(path) ? i.ToString() : path + "." + i;
            if (container[i] is JObject function && (string)function["f()"] == "resolve_check")
            {
                // 在原调用位置内建立节点上下文，保留外围所有数字跳转地址。
                container[i] = new JArray("str", "^action:check_context node=" + point, "/str",
                    new JObject { ["x()"] = "game_action", ["exArgs"] = 1 }, "pop", function.DeepClone(), null);
                continue;
            }
            if (container[i] is JArray child) { RewriteContainer(child, point); continue; }
            if (container[i].Type != JTokenType.String || (string)container[i] != "#") continue;
            int end = i + 1;
            while (end < container.Count && (string)(container[end] as JValue) != "/#") end++;
            if (end >= container.Count) throw new FormatException("剧情标签缺少 /#。");
            if (!((string)container[i + 1]).StartsWith("^action:", StringComparison.Ordinal)) continue;
            var call = new JArray("ev", "str");
            for (int k = i + 1; k < end; k++) call.Add(container[k].DeepClone());
            call.Add("/str");
            call.Add(new JObject { ["x()"] = "game_action", ["exArgs"] = 1 });
            call.Add("pop"); call.Add("/ev");
            // 给界面一个确切的停靠点：先处理动作，再读取下一句/判断。
            call.Add("^" + YieldLine); call.Add("\n"); call.Add((JToken)null);
            container[i] = call;
            // 不能删掉这些位置，原稿有大量以数字位置为目标的跳转。
            for (int k = i + 1; k <= end; k++) container[k] = "nop";
            i = end;
        }
        if (container.Last is JObject meta)
            foreach (var property in meta.Properties())
                if (property.Value is JArray named) RewriteContainer(named, string.IsNullOrEmpty(path) ? property.Name : path + "." + property.Name);
    }

    /// <summary>一次只取一条可见台词，动作不会提前跨过整段对话执行。</summary>
    public DialogueData Read()
    {
        var node = new DialogueNode { id = "ink", nextNodeId = "" };
        int guard = 0;
        while (Story.canContinue && guard++ < 10000)
        {
            // 检定是一个明确停点：暂停前保存 Ink 检定前快照；确认后重读同一次结果。
            // 重新构造 Story 也清理中断 Continue 的内部临时栈，不改写第三方 Ink 源码。
            string checkpoint=Story.state.ToJson();string text;
            try{text=Story.Continue().Trim();}
            catch(PendingCheckException){BindStory();Story.state.LoadJson(checkpoint);return null;}
            if (ShouldPause?.Invoke() == true) break;
            text = text.Replace(YieldLine, "").Trim();
            if (text.Length == 0) continue;
            // 试读字样是原稿调试标记；只去掉按钮/台词中的这个前缀。
            int colon = text.IndexOfAny(new[] { '：', ':' });
            if (colon < 0)
            {
                int dash = text.IndexOf('-');
                if (dash > 0 && dash < 18 && IsSpeaker(text.Substring(0, dash))) colon = dash;
            }
            if (colon >= 0 && colon < 25 && IsSpeaker(text.Substring(0, colon)))
            {
                Speaker = text.Substring(0, colon).Trim();
                text = text.Substring(colon + 1).Trim();
            }
            if (text.Length == 0) continue; // 原稿把说话人与正文分在两行。
            node.lines.Add(new DialogueLineData { speaker = Speaker, content = text, sourceId=LastLineId });
            break;
        }
        if (guard >= 10000) throw new InvalidOperationException("剧情连续执行过长，请检查原稿循环。");
        if (!Story.canContinue)
            foreach (var choice in Story.currentChoices)
                node.options.Add(new DialogueOption
                {
                    text = choice.text.Replace("试读：", "").Trim(), param = choice.index,
                    choiceId = choice.sourcePath,
                    targetNodeId = "ink",
                    callbackKey = _featurePaths.TryGetValue(choice.sourcePath, out var feature) ? feature : null,
                    tag = "narrative"
                });
        if (node.options.Exists(o => o.callbackKey == "open_commission_panel"))
        {
            Speaker = "旁白";
            foreach (var line in node.lines) line.speaker = "旁白";
        }
        return new DialogueData { dialogueId = "story27", startNodeId = node.id,
            nodes = new List<DialogueNode> { node } };
    }

    private static bool IsSpeaker(string name)
    {
        switch (name.Trim())
        {
            case "旁白": case "系统": case "系统告知": case "你": case "？？？": case "???":
            case "轻柔的声音": case "粗重的声音": case "Myth": case "Ferdia":
            case "Clot": case "Lach": case "Atros": case "Clot·Lach·Atros":
            case "Sarah": case "Audrey": case "Da": case "Nobody": return true;
            default: return false;
        }
    }

    public void Choose(int index) => Story.ChooseChoiceIndex(index);
    public void Set(string variable, object value) => Story.variablesState[variable] = value;
    public object Get(string variable) => Story.variablesState[variable];
    public void SetMember(string variable, string actor, bool included)
    {
        var list = new InkList((InkList)Get(variable));
        var item = new InkListItem("PEOPLE", actor);
        if (included) list.AddItem(actor, Story); else list.Remove(item);
        Set(variable, list);
    }
    public bool HasMember(string variable, string actor) =>
        ((InkList)Get(variable)).ContainsKey(new InkListItem("PEOPLE", actor));
}
