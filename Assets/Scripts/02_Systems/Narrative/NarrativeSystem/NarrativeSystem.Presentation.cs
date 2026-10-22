/****************************************************
    文件：NarrativeSystem.Presentation.cs
	作者：DADI
    邮箱: 1581507659@qq.com
    日期：2026/10/22
	功能：27天剧情接线层·演出层：立绘解析、文本台词扩展、幕间转场与显示归档
/*****************************************************/

using System;
using System.Collections.Generic;
using System.Linq;
using Ink.Runtime;
using Newtonsoft.Json;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// 27天剧情的游戏接线层。生命周期由 SystemManager 管理；对话、商店、委托、棋盘收入和结算
/// 均复用原系统，不另建 UI。数值留在 Resources/Narrative/rules.json，原稿 action 标签统一接线。
/// 本类以 partial 拆分为多个文件，同功能代码用 #region 分组。
/// </summary>
public sealed partial class NarrativeSystem
{
    #region 立绘解析

    public string GetPortraitAddress(int adventurerId)
    {
        var actor = ActorIds.FirstOrDefault(p => p.Value == adventurerId).Key;
        if (actor == null) return null;
        string name = _systems.Config.Adventurers[adventurerId].name;
        return _rules.portraits.TryGetValue(name, out var address) ? address : null;
    }

    private Dictionary<string,PortraitCue> _portraitCues;
    private string ResolvePortrait(DialogueLineData line)
    {
        if(_save.activeRevengeActor>0&&ActorIds.TryGetValue(Actor,out int id)&&id==_save.activeRevengeActor)
        {
            _save.portraitAddress=_rules.portraitVariants[Actor]["hurt"];
            return _save.portraitAddress;
        }
        if(_portraitCues==null)_portraitCues=JsonConvert.DeserializeObject<Dictionary<string,PortraitCue>>(Resources.Load<TextAsset>("Narrative/portraitCues").text);
        if(!string.IsNullOrEmpty(line.sourceId)&&_portraitCues.TryGetValue(line.sourceId,out var cue)) {
            if(cue.hide){_save.portraitAddress="Narrative/blank";return _save.portraitAddress;}
            string actor=string.IsNullOrEmpty(cue.actor)?Actor:cue.actor;
            if(actor!=null&&_rules.portraitVariants.TryGetValue(actor,out var variants)) {
                foreach(var path in variants.Values)SpriteLoader.LoadAsync(path);
                string pose=cue.pose=="hurt"&&!cue.injury?"serious":cue.pose;
                if(!variants.TryGetValue(pose??"neutral",out var address))address=variants["neutral"];
                _save.portraitAddress=address;return address;
            }
        }
        var original=Portrait(line.speaker);_save.portraitAddress=original;return original;
    }

    #endregion

    #region 文本与台词扩展


    private string ExpandText(string text)
    {
        if(_rules.textOverrides.TryGetValue(text,out var replacement))text=replacement;
        // 只更新玩家可见的骰制说明；D20_V01等是“第20天”的稳定剧情ID，不能全局替换。
        text=text.Replace("D20检定","D100检定").Replace("D20大失败","D100的1～5大失败");
        int due = text.Contains("27天后") ? _systems.Config.Schedules.Sum(s => s.installmentAmount) :
            _systems.Config.GetInstallmentPayment(Run.currentPhase);
        text = text.Replace("【本局配置金额】", due.ToString());
        var scene = CurrentScene;
        if (scene != null)
        {
            if (text.StartsWith("报酬展示：", StringComparison.Ordinal))
                return "基础报酬：" + scene.reward + "金币";
            if (text.StartsWith("目标需求：", StringComparison.Ordinal) && (text.Contains("待主策配置") || text.Contains("由委托配置显示")))
                return "目标类别：" + DailyCommissionSystem.TypeName(DailyCommissionSystem.TargetType(_systems.Config.Contracts[scene.contractId]));
        }
        return text;
    }
    private string Portrait(string speaker)
    {
        if (_rules.portraits.TryGetValue(speaker, out var address)) return address;
        // 接待菜单、玩家回答和旁白不代表人物离开，继续保留当前访客的立绘。
        return _save.departingVisitor > 0 ? GetPortraitAddress(_save.departingVisitor) ?? "Narrative/blank" : "Narrative/blank";
    }

    #endregion

    #region 幕间转场与显示记录


    /// <summary>原稿四段幕间的首句稳定标识；只在实际新读时演出，重显存档帧不重播。</summary>
    private string _visitorTransitionKey;
    public string ConsumeVisitorTransition(){var key=_visitorTransitionKey;_visitorTransitionKey=null;return key;}

    private void PresentFrameTransition(DialogueData frame)
    {
        if(_restoring)return;
        foreach(var node in frame.nodes)foreach(var line in node.lines)
            switch(line.sourceId) {
                case "DAY03_END.24": case "DAY07_END.32": case "DAY12_END.32": case "DAY19_END.32":
                    PresentationEffects.PlayInterlude(line.sourceId);return;
            }
    }

    /// <summary>只在 TalkPanel 完成实际显示后写入；同一存档帧重显不会重复归档。</summary>
    public void RecordDisplayed(DialogueLine line, string suffix)
    {
        if (!Enabled || _save.departingVisitor < 0 || !TryActor(Actor, out var adv)) return;
        if (_save.displayed.Add(_save.displaySerial + ":" + suffix))
        {
            adv.historyDialogue.Add(line.speakerName + "：" + line.content);
            UIManager.Instance?.Get<CharacterArchivePanel>()?.RefreshRecords();
        }
    }

    #endregion
}
