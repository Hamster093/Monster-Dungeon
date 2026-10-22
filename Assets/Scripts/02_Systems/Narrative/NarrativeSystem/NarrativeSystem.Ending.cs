/****************************************************
    文件：NarrativeSystem.Ending.cs
	作者：DADI
    邮箱: 1581507659@qq.com
    日期：2026/10/22
	功能：27天剧情接线层·结局与继任：继任条件评估、候选确认、说服掷骰、终局CG
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
    #region 继任条件评估


    private static bool MeetsSuccession(string actor, AdventurerRuntimeData adv)
    {
        int S(SevenDimension stat) => adv.currentSevenStats.TryGetValue(stat, out int v) ? v : 0;
        // 数值来自最新《冒险者属性配置表 v0.1》，不沿用旧配置的占位主副属性。
        switch (actor)
        {
            case "MY": return S(SevenDimension.Tolerance) >= 90 && S(SevenDimension.Resolve) >= 75;
            case "FE": return S(SevenDimension.Courage) >= 80 && S(SevenDimension.Resolve) <= 40;
            case "DW": return S(SevenDimension.Benevolence) >= 80 && S(SevenDimension.Diligence) >= 75;
            case "SA": return S(SevenDimension.Piety) <= 35 && S(SevenDimension.Courage) >= 65;
            case "AU": return S(SevenDimension.Reason) <= 30 && S(SevenDimension.Benevolence) <= 30;
            case "DA": return S(SevenDimension.Diligence) >= 85 && S(SevenDimension.Piety) <= 30;
            case "UN": return S(SevenDimension.Resolve) <= 10 && S(SevenDimension.Reason) <= 5;
            default: return false;
        }
    }

    // 档案、确认框和正式检定只读取这一份评估，最大不足值不会被其他超标属性抵消。
    public SuccessionAssessment AssessSuccession(int actorId)
        => AssessSuccession(actorId, Run.adventurers[actorId].currentSevenStats, EndingExpression);

    public SuccessionAssessment AssessSuccession(int actorId, string attribute)
        => AssessSuccession(actorId, Run.adventurers[actorId].currentSevenStats, attribute);

    /// <summary>读取指定七维快照，供变化前后比较；不会修改人物、掷骰或登记成长。</summary>
    public SuccessionAssessment AssessSuccession(int actorId, IReadOnlyDictionary<SevenDimension, int> stats)
        => AssessSuccession(actorId, stats, EndingExpression);

    private SuccessionAssessment AssessSuccession(int actorId, IReadOnlyDictionary<SevenDimension, int> stats, string attribute)
    {
        if (!IsPlayerAttribute(attribute)) attribute=Run.player.PreferredAttribute;
        string code=ActorCode(actorId);var adv=Run.adventurers[actorId];int gap=0;var parts=new List<string>();
        foreach(var c in _rules.successionRules[code]) {
            int v=stats[c.stat],deficit=Math.Max(0,c.maximum?v-c.value:c.value-v);gap=Math.Max(gap,deficit);
            parts.Add(StatChinese(c.stat)+" "+v+" / "+(c.maximum?"≤":"≥")+c.value+"　"+(deficit==0?"已满足":"尚差"+deficit));
        }
        // 接任只用当前所选表达对应的玩家属性，不套普通信息/影响思维。
        int modifier=Run.player.Active(Run.currentDay)?Run.player.Attribute(attribute)-40:0;
        int target=101-(_rules.successionChanceStart-_rules.successionChanceStep*gap);
        return new SuccessionAssessment{actorId=actorId,actor=code,name=adv.Config.name,gap=gap,dead=adv.isDead,attribute=attribute,progress=string.Join("\n",parts),
            tendency=gap==0?"容易接受":gap<=_rules.successionNearGap?"不感兴趣":"非常厌恶",target=target,modifier=modifier,
            chance=gap==0?100:gap>_rules.successionNearGap?0:D100Service.SuccessPercent(target,modifier)};
    }

    #endregion

    #region 终局状态与候选

    public string EndingStage => _save.stage;
    public string EndingPresentationPhase => _save.endingPresentationPhase;
    // 转场仅登记展示阶段；读取途中档时由面板恢复到可见CG，不再判定或结算。
    public void SetEndingPresentationPhase(string phase){_save.endingPresentationPhase=phase;Save();}
    public int EndingCandidate => _save.endingCandidate;
    public string EndingExpression => string.IsNullOrEmpty(_save.endingExpression) ? Run.player.PreferredAttribute : _save.endingExpression;
    public string EndingActorCode => _save.ending.Split(':').Last();
    public string ActorCode(int id) => ActorIds.First(p=>p.Value==id).Key;
    public string BasePortrait(int id) => _rules.portraitVariants[ActorCode(id)]["neutral"];
    public string EndingCGAddress => _rules.endingCG[EndingActorCode];
    public string EndingActorName => _systems.Config.Adventurers[ActorIds[EndingActorCode]].name;
    private bool InterceptEndingSelection(DialogueData frame)
    {
        if(!frame.nodes.Any(n=>n.options.Any(o=>(o.choiceId??"").StartsWith("END_SELECTION.",StringComparison.Ordinal))))return false;
        if(_save.successionFailed) {
            _save.successionFailed=false;_save.frame=null;_save.stage="playing";_runtime.Story.ChoosePathString("END_SWALLOWED");return true;
        }
        _save.frame=frame;_save.stage="ending_selection";Save();return true;
    }
    public void PreviewEndingCandidate(int id)
    {
        if(_save.stage!="ending_selection"||Run.adventurers[id].isDead)return;
        _save.endingCandidate=id;_save.endingExpression=Run.player.PreferredAttribute;_save.stage="ending_confirm";Save();UIManager.Instance.Open<EndingPresentationPanel>().OnRefresh();
    }
    public void CancelEndingCandidate()
    { if(_save.stage!="ending_confirm")return;_save.stage="ending_selection";_save.endingCandidate=-1;_save.endingExpression=null;Save();UIManager.Instance.Get<EndingPresentationPanel>().OnRefresh(); }
    public bool ConfirmSuccessor(int actorId, string attribute)
    {
        if (_save.stage!="ending_confirm" || _save.endingCandidate!=actorId || !IsPlayerAttribute(attribute) || Run.adventurers[actorId].isDead || Run.pendingDice!=null) return false;
        SelectEndingExpression(attribute);
        ConfirmEndingCandidate();
        return true;
    }
    public void ConfirmEndingCandidate(bool stay=false)
    {
        if(!stay&&_save.stage!="ending_confirm" || stay&&_save.stage!="ending_selection")return;
        string name=stay?null:_systems.Config.Adventurers[_save.endingCandidate].name;
        if(!stay&&Run.adventurers[_save.endingCandidate].isDead){CancelEndingCandidate();return;}
        var choice=_runtime.Story.currentChoices.First(c=>stay?c.text.Contains("不再将经营权"):c.text.Contains(name));
        _save.stage="playing";_save.frame=null;_runtime.Choose(choice.index);
        UIManager.Instance.Close<EndingPresentationPanel>();Save();StartReception();
    }
    public void CompleteEndingCG()
    { if(_save.stage!="ending_cg")return;_save.stage="ending_complete";_save.endingPresentationPhase="complete";Save(); }
    private void OpenEndingPresentation()
    {
        UIManager.Instance.Close<CharacterArchivePanel>();
        if(_save.stage!="ending_cg")UIManager.Instance.Close<TalkPanel>();
        UIManager.Instance.Open<EndingPresentationPanel>();
    }

    #endregion

    #region 表达选择与倾向反馈


    private static bool IsPlayerAttribute(string attribute) => attribute=="perception" || attribute=="knowledge" || attribute=="willpower";
    /// <summary>这里只保存表达选择；预览、切换、返回都不会掷骰、消耗道具或变更人物。</summary>
    public void SelectEndingExpression(string attribute)
    {
        if (_save.stage!="ending_confirm" || !IsPlayerAttribute(attribute) || Run.pendingDice!=null) return;
        var assessment=AssessSuccession(_save.endingCandidate,attribute);
        if (assessment.gap==0 || assessment.gap>_rules.successionNearGap || assessment.dead) return;
        _save.endingExpression=attribute;
        Save();
    }

    /// <summary>只显示文字倾向变化；培养差距属于隐藏规则，不反馈具体数值。</summary>
    public string DescribeSuccessionChange(int actorId, IReadOnlyDictionary<SevenDimension, int> beforeStats)
    {
        var after = AssessSuccession(actorId);
        if (after.dead) return "";
        var before = AssessSuccession(actorId, beforeStats);
        if (before.tendency != after.tendency) return "接任倾向：" + before.tendency + "→" + after.tendency;
        return "";
    }
    /// <summary>兼容旧档展示：只移除旧系统生成的接任差距，保留同一行的属性、报酬和原稿。</summary>
    public static string HidePrivateSuccessionProgress(string text)
    {
        if(string.IsNullOrEmpty(text))return text??"";
        if(!text.Contains("接任差距"))return text;
        text=System.Text.RegularExpressions.Regex.Replace(text,@"接任差距\s*\d+\s*(?:→|->)\s*\d+", "");
        return string.Join("\n",System.Linq.Enumerable.Where(text.Split('\n'),line=>!string.IsNullOrWhiteSpace(line)));
    }

    #endregion

    #region 继任说服掷骰

    private bool ResolveSuccession(string actor)
    {
        var a=AssessSuccession(ActorIds[actor]);if(a.dead)return false;
        if(Run.pendingDice?.context=="narrative"&&Run.pendingDice.confirmed) {
            var p=Run.pendingDice;bool success=p.Result().Success;
            if(!p.FixedOutcome&&Run.player.created)Run.player.Resolve(p.key,success);
            Run.pendingDice=null;_save.checkId=null;_save.successionFailed=!success;return success;
        }

        if(Run.pendingDice==null) {
            bool forced=a.gap>_rules.successionNearGap,accepted=a.gap==0;
            // 必败不是抽签机会，不启动护符或幸运硬币，也不登记成长。
            var p=forced||accepted?new D100Service.Presentation{sides=100,key="succession:"+actor,title=a.name+" · 接任说服",context="narrative",target=accepted?1:101,roll=UnityEngine.Random.Range(1,101),criticalRules=false,guaranteedFailure=forced,guaranteedSuccess=accepted}
                :D100Service.CreatePresentation("succession:"+actor,a.name+" · 接任说服",a.target,a.modifier,"narrative");
            p.rawRoll=p.roll;p.playerDay=Run.currentDay;p.playerKind="succession_persuade";p.playerAttribute=a.attribute;p.checkId=_save.checkId;
            p.annotation=a.Annotation;p.details=accepted?"容易接受":forced?"非常厌恶":PlayerBuildState.AttributeName(a.attribute)+Run.player.Attribute(a.attribute)+" · 属性修正"+Signed(a.modifier);
            p.storyText="你向"+a.name+"提出接任。";p.storySpeaker="旁白";Run.pendingDice=p;
        }
        _save.stage="dice";throw new NarrativeRuntime.PendingCheckException();
    }

    #endregion
}
