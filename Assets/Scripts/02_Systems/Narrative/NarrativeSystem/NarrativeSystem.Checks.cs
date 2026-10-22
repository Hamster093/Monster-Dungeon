/****************************************************
    文件：NarrativeSystem.Checks.cs
	作者：DADI
    邮箱: 1581507659@qq.com
    日期：2026/10/22
	功能：27天剧情接线层·掷骰检定：D100上下文、难度、玩家选项装饰与思维修正
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
    #region 掷骰执行与恢复

    private void RestorePendingDice()
    {
        var request=Run.pendingDice;if(request==null||DiceCheckPanel.IsShowing)return;
        if(request.context=="narrative")D100Service.Show(request,result=>{
            Run.pendingDice=request;request.confirmed=true;_save.stage="playing";_save.frame=null;Save();
            UIManager.Instance.Get<TalkPanel>().Dialogue.StartNarrative();
        });
        else if(request.context=="settlement")_systems.DailyCommission.ResolveAllChecksAsync(FinishDayChecks);
    }
    private bool Check(string kind, string actor, int bonus)
    {
        // 共享合作节点指定了说服对象，不能借用进入该节点前的访客。
        if(CheckRules.TryGetValue(_save.checkId??"",out var caller)&&!string.IsNullOrEmpty(caller.actor))actor=caller.actor;
        if (!TryActor(actor, out var adv) || adv.isDead) return false;
        if (kind == "succession") return ResolveSuccession(actor);
        if (Run.pendingDice?.context == "narrative" && Run.pendingDice.confirmed)
        {
            var request=Run.pendingDice; var result=request.Result();
            if (request.playerDay>=1 && request.playerDay<=27 && Run.player.created && kind!="personal")
            {
                var player = Run.player;
                var snapshot = player.pendingThoughtCheck;
                if (!player.resolvedChecks.Contains(request.key))
                {
                    if(request.trainingEligible&&result.Success&&player.TrainInformation(request.checkId+":"+request.trainingReception,request.trainingReception,request.playerAttribute,true))
                    {ToastManager.Show(PlayerBuildState.AttributeName(request.playerAttribute)+"＋1","player-growth:"+request.key);if(!_restoring)AudioManager.Instance?.PlayCue("stat_up");}
                    player.influenceKey=request.playerKind=="influence" && result.Success ? request.key : null;
                    player.influenceActor=adv.configId;
                    if (snapshot != null && snapshot.key == request.key)
                    {
                        player.influenceBonus=0;
                        player.influenceCakeDefault=snapshot.kind=="influence" && snapshot.thought==1 && result.Success;
                        player.ResolveThoughtCheck(snapshot,result.Success);
                        if (snapshot.thought==2 && !result.Success && (snapshot.kind=="information" || snapshot.kind=="influence"))
                            ToastManager.Show("思维“地牢精神病人”生效", "thought-opportunity:"+request.key);
                    }
                    else
                    {
                        // 迁移前已掷出的检定原样落定，保留当时上下文，不套新思维或重新掷骰。
                        player.influenceCakeDefault=false;
                        player.influenceBonus=request.playerThought==1?request.playerLevel:0;
                        player.Resolve(request.key,result.Success);
                    }
                }
                player.pendingThoughtCheck=null;
                TopBarRefreshEvent.Trigger();
            }
            Run.pendingDice=null; _save.checkId=null; return result.Success;
        }
        if (Run.pendingDice==null)
        {
            if (!CheckRules.TryGetValue(_save.checkId??"",out var rule) || rule.kind!=kind)
                throw new InvalidOperationException("剧情检定没有对应节点配置："+_save.checkId+" / "+kind);
            var context=BuildCheckContext(kind,actor,rule.attribute,bonus,rule.player);
            var request=D100Service.CreatePresentation("story:"+_save.displaySerial,adv.Config.name+" · "+(context.player?PlayerBuildState.AttributeName(context.attribute)+"检定":"角色决定"),context.target,context.modifier,"narrative",context.critical);
            request.checkId=rule.checkId;
            request.trainingEligible=context.player&&kind=="information"&&
                (rule.checkId.StartsWith("MYD1_TOPICS.",StringComparison.Ordinal)||rule.checkId.Contains("_TOPICS."));
            request.trainingReception=Run.currentDay+":"+adv.configId+":"+CurrentScene?.id;
            if (context.player)
            {
                request.playerDay=Run.currentDay; request.playerKind=kind; request.playerAttribute=context.attribute;
                request.playerThought=context.thought; request.playerLevel=0;
                Run.player.pendingThoughtCheck=new PlayerBuildState.ThoughtCheckSnapshot {
                    key=request.key,kind=kind,attribute=context.attribute,scene=CurrentScene?.id,
                    actorId=adv.configId,day=Run.currentDay,thought=context.thought,hadSwitchOpportunity=context.hadSwitchOpportunity
                };
                request.details=PlayerBuildState.AttributeName(context.attribute)+Run.player.Attribute(context.attribute)+"（修正"+Signed(context.attributeBonus)+"） · 回答/情境"+Signed(context.modifier-context.attributeBonus-context.thoughtBonus)
                    +(string.IsNullOrEmpty(context.thoughtNote)?"":"\n"+context.thoughtNote)
                    +"\n单次成功率 "+D100Service.SuccessPercent(context.target,context.modifier,context.critical)+"%";
                request.annotation=CheckLabel(context);
                var line=UIManager.Instance.Get<TalkPanel>()?.CurrentDisplayedLine;
                request.storyText=line?.content; request.storySpeaker=line?.speakerName;
            }
            Run.pendingDice=request;
        }
        _save.stage="dice"; throw new NarrativeRuntime.PendingCheckException();
    }

    #endregion

    #region 检定配置加载

    private Dictionary<string,PlayerChoiceRule> _playerChoices;
    private Dictionary<string,PlayerCheckRule> _checkRules;
    private void LoadPlayerNodes()
    {
        if(_playerChoices!=null)return;
        var data=JsonConvert.DeserializeObject<PlayerNodeConfig>(Resources.Load<TextAsset>("Narrative/playerChecks").text);
        _playerChoices=data.choices.ToDictionary(r=>r.choiceId);
        _checkRules=data.checks.ToDictionary(r=>r.checkId);
    }
    private Dictionary<string,PlayerChoiceRule> PlayerChoices { get { LoadPlayerNodes(); return _playerChoices; } }
    private Dictionary<string,PlayerCheckRule> CheckRules { get { LoadPlayerNodes(); return _checkRules; } }

    #endregion

    #region 检定上下文与难度

    /// <summary>预览与执行都走此公式；预览只传0，不提前计算本次回答的隐藏修正。</summary>
    private PlayerCheckContext BuildCheckContext(string kind,string actor,string attribute,int bonus,bool isPlayer=true)
    {
        var b=D100Service.Balance; TryActor(actor,out var adv);
        var c=new PlayerCheckContext {kind=kind,attribute=attribute,
            target=kind=="information"?b.informationTarget:kind=="influence"?b.influenceTarget:kind=="cooperate"?b.cooperateTarget:b.personalTarget,
            modifier=Mathf.Clamp(bonus*b.dialogueBonus,-b.dialogueBonusCap,b.dialogueBonusCap),player=isPlayer&&Run.player.Active(Run.currentDay)};
        if(kind=="revenge"||kind=="revenge_gift")
        {
            bool heavy=(string)_runtime.Get("revenge_severity")=="heavy";
            int chance=(heavy?b.revengeHeavyChance:b.revengeLightChance)+(kind=="revenge_gift"?b.revengeGiftBonus:0)+bonus*b.dialogueBonus;
            c.target=101-Mathf.Clamp(chance,0,100); c.modifier=0; c.critical=false;
        }
        if(kind=="cooperate")
        {
            SevenDimension x=actor=="FE"?SevenDimension.Courage:actor=="AU"?SevenDimension.Reason:SevenDimension.Resolve;
            SevenDimension y=actor=="AU"?SevenDimension.Benevolence:actor=="FE"?SevenDimension.Resolve:SevenDimension.Reason;
            c.modifier+=Mathf.Clamp((adv.currentSevenStats[x]+adv.currentSevenStats[y]-100)/b.coopStatDivisor,-b.coopStatCap,b.coopStatCap);
        }
        if(c.player)
        {
            var player=Run.player;
            c.thought=player.ThoughtActive(Run.currentDay,Run.currentPhase)?player.thought:-1;
            if(kind=="influence" && c.thought==0 && adv.validInfo.Count>=2)
            {
                string preferred=player.PreferredAttribute;
                if(player.Attribute(preferred)>player.Attribute(c.attribute))c.attribute=preferred;
                c.thoughtNote="你的思维—哈耳摩尼亚史官提醒你，你应该采用“"+PlayerBuildState.AttributeName(c.attribute)+"”来进行判断。";
            }
            else if(kind=="influence" && c.thought==1)
            {
                c.thoughtBonus=10;
                c.thoughtNote="依据你的思维—戚风蛋糕谈判术：骰子修正＋10。";
            }
            if((kind=="information" || kind=="influence") && c.thought==2)
            {
                c.hadSwitchOpportunity=player.HasSwitchOpportunity(adv.configId,Run.currentDay,CurrentScene?.id);
                if(c.hadSwitchOpportunity && c.attribute!=player.switchOpportunityAttribute)
                {
                    c.thoughtBonus=10;
                    c.thoughtNote="依据你的思维—地牢精神病人：骰子修正＋10。";
                }
            }
            c.attributeBonus=player.Attribute(c.attribute)-40;
            c.modifier+=c.attributeBonus+c.thoughtBonus;
        }
        return c;
    }
    public PlayerCheckContext PreviewPlayerCheck(string checkId)
    {
        var rule=CheckRules[checkId]; return BuildCheckContext(rule.kind,string.IsNullOrEmpty(rule.actor)?Actor:rule.actor,rule.attribute,0,rule.player);
    }
    private static string Signed(int value)=>(value>=0?"+":"")+value;
    public static string Difficulty(int chance)=>chance<=24?"极难":chance<=44?"困难":chance<=69?"一般":chance<=84?"轻松":"极易";
    private static string CheckLabel(PlayerCheckContext c)=>"（检定—"+PlayerBuildState.AttributeName(c.attribute)+"　难度："+Difficulty(D100Service.SuccessPercent(c.target,c.modifier,c.critical))+"）";

    #endregion

    #region 玩家选项装饰与提交

    /// <summary>只格式化显示，不运行剧情、读随机数或登记成长。</summary>
    private void DecoratePlayerOptions(DialogueData frame)
    {
        foreach(var node in frame.nodes)
        {
            node.options.RemoveAll(o=>o.callbackKey=="open_special_commission");
            foreach(var option in node.options)
            {
                if(string.IsNullOrEmpty(option.choiceId)&&option.param>=0&&option.param<_runtime.Story.currentChoices.Count)option.choiceId=_runtime.Story.currentChoices[option.param].sourcePath;
                option.displayText=option.text; option.checkAttribute=option.checkKind=null;
                option.requiredItemId=RevengeOptionItem(option.choiceId);
                if(option.requiredItemId>0)
                {
                    string name=option.requiredItemId==5?"致歉礼盒":"碎骨卷轴";
                    option.displayText=option.text+"（需要："+name+"）";
                    continue;
                }
                if(string.IsNullOrEmpty(option.choiceId)||!PlayerChoices.TryGetValue(option.choiceId,out var rule))continue;
                if(!string.IsNullOrEmpty(rule.label)){option.text=rule.label;option.displayText=rule.label;}
                if(string.IsNullOrEmpty(rule.checkId)||!Run.player.created)continue;
                var context=PreviewPlayerCheck(rule.checkId);
                option.checkAttribute=context.attribute; option.checkKind=context.kind;
                option.displayText=option.text+"\n<size=22><color=#6A4D27>"+CheckLabel(context)+"</color></size>";
            }
        }
    }
    private void CommitPlayerChoice(DialogueOption option)
    {
        var player=Run.player;
        if(!player.Active(Run.currentDay))return;
        player.influenceKey=null;player.influenceBonus=0;player.influenceCakeDefault=false;
        if(string.IsNullOrEmpty(option.choiceId)&&option.param>=0&&option.param<_runtime.Story.currentChoices.Count)option.choiceId=_runtime.Story.currentChoices[option.param].sourcePath;
        if(player.pendingChoiceCredit){player.Credit(player.pendingChoiceId);player.pendingChoiceCredit=false;}
        if(string.IsNullOrEmpty(option.choiceId)||!PlayerChoices.TryGetValue(option.choiceId,out var rule))return;
        player.pendingChoiceId=option.choiceId; player.pendingAttribute=rule.attribute;
        player.pendingChoiceCredit=rule.credit&&!string.IsNullOrEmpty(rule.checkId);
        if(rule.credit&&string.IsNullOrEmpty(rule.checkId))player.Credit(option.choiceId);
        TopBarRefreshEvent.Trigger();
    }
    // 旧档在一个可选择的停点补选：不在台词中段或尚未建立的检定前插入新状态。
    private bool OfferPlayerAtOptions(DialogueData frame)
    {
        if (Run.player.created || Run.currentDay < 1 || Run.currentDay > 27 || _save.stage != "playing" ||
            Run.pendingDice != null || _save.pendingRoute != null || !frame.nodes.Any(n => n.options.Count > 0)) return false;
        if (!PlayerBuildPanel.IsChoosing) UIManager.Instance.Open<PlayerBuildPanel>(new Action(StartReception));
        return true;
    }
    public bool CreatePlayer(int aptitude, int thought = -1)
    {
        if (Run.currentDay > 27 || !Run.player.Create(aptitude, Run.currentDay < 2 ? -1 : thought)) return false;
        Save(); TopBarRefreshEvent.Trigger(); return true;
    }

    /// <summary>包括“不采用”在内均视为本阶段已确认；关闭预览与返回不能调用本接口。</summary>
    public bool ConfirmThought(int thought)
    {
        if (thought < -1 || thought > 2 || !NeedsThoughtSelection) return false;
        Run.player.thought = thought;
        Run.player.thoughtConfirmedPhase = Run.currentPhase;
        Run.player.EndReceptionThoughts();
        Save(); TopBarRefreshEvent.Trigger(); return true;
    }

    #endregion
}
