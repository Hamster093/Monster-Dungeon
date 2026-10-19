using System;
using System.Collections;
using UnityEngine;
using UnityEngine.UI;

/// <summary>所有正式检定共用的结果确认层。演出只读取冻结结果，不重放业务奖励。</summary>
public sealed class DiceCheckPanel : PanelBase
{
    public sealed class OpenData{public D20Service.Presentation request;public Action<CheckResult> completed;}
    [SerializeField] private CanvasGroup _fade;
    [SerializeField] private Text _title,_requirement,_rollText,_outcome,_note;
    [SerializeField] private Text _modifierText,_totalText;
    [SerializeField] private ScrollRect _noteScroll;
    [SerializeField] private Button _continue,_coin;
    [SerializeField] private CanvasGroup _contextStage,_rollStage,_modifierStage,_totalStage,_outcomeStage,_actionsStage;
    private OpenData _data;
    private bool _done,_ready,_sound;
    private Coroutine _reveal;
    public static bool IsShowing=>UIManager.Instance?.Get<DiceCheckPanel>()?.IsOpen==true;
    public override bool IsModal=>true;
    /// <summary>1门槛、2基础骰点、3加权来源、4总点、5结果。仅用于显示与验收。</summary>
    public int RevealStage { get; private set; }
    public bool IsRevealing=>IsOpen&&_data?.request!=null&&!_ready&&!_done;
    public override void OnInit(){_continue.onClick.AddListener(Complete);_coin.onClick.AddListener(Reroll);}
    public override void OnOpen(object data=null)
    {
        base.OnOpen(data);_data=data as OpenData;_done=false;
        if(_data?.request==null)return;
        _data.request.Normalize();
        RunStateManager.Instance.run.pendingDice=_data.request;
        SystemManager.Instance.Narrative?.Save();
        BeginReveal();
    }
    private void BeginReveal()
    {
        if(_reveal!=null)StopCoroutine(_reveal);
        _ready=false;RevealStage=0;
        _sound=!_data.request.audioPresented;_data.request.audioPresented=true;SystemManager.Instance.Narrative?.Save();
        _continue.interactable=false;_coin.interactable=false;_coin.gameObject.SetActive(false);
        foreach(var group in new[]{_contextStage,_rollStage,_modifierStage,_totalStage,_outcomeStage,_actionsStage})
            if(group){group.alpha=0;group.interactable=false;group.blocksRaycasts=false;}
        _fade.alpha=1;
        var p=_data.request;
        _title.text=p.title;_requirement.text=p.target.ToString();
        _rollText.text="0";_outcome.text="";
        if(_modifierText)_modifierText.text=p.guaranteedFailure?"—":Signed(p.modifier);
        if(_totalText)_totalText.text="0";
        // 固定结果仍按原顺序演出，不在结果揭晓前额外标明必成或必败。
        _note.text=p.FixedOutcome?"":CompactDetails(p);
        if(p.charmUsed)_note.text+="\n庇护耳坠 −1";
        if(p.rerolled)_note.text+="\n幸运硬币 −1";
        RefreshScroll();
        _reveal=StartCoroutine(Reveal());
    }
    private IEnumerator Reveal()
    {
        // 点数已在Presentation中确定。这里不调用随机数，也不改变任何业务结果。
        yield return WaitVisible(.05f);
        RevealStage=1;yield return FadeIn(_contextStage,.24f);
        if(_contextStage){_contextStage.interactable=true;_contextStage.blocksRaycasts=true;}
        yield return WaitVisible(.16f);
        RevealStage=2;if(_sound)AudioManager.Instance?.PlayCue("dice");yield return NumberIn(_rollStage,_rollText,_data.request.roll,.43f);yield return WaitVisible(.13f);
        RevealStage=3;yield return FadeIn(_modifierStage,.27f);
        if(_modifierStage){_modifierStage.interactable=true;_modifierStage.blocksRaycasts=true;}
        yield return WaitVisible(.17f);
        RevealStage=4;
        yield return NumberIn(_totalStage,_totalText,_data.request.roll+_data.request.modifier,.4f);
        yield return WaitVisible(.17f);
        RevealStage=5;
        var p=_data.request;var result=p.Result();
        if(_sound)AudioManager.Instance?.PlayCue(result.IsFumble?"fumble":result.Success?"check_success":"check_failure");
        _outcome.text=result.IsCritical?"大成功":result.IsFumble?"大失败":result.Success?"成功":"失败";
        _outcome.color=result.Success?new Color(.95f,.8f,.48f):new Color(.88f,.48f,.41f);
        if(p.context=="settlement"&&p.key.EndsWith(":battle"))
        {
            // 伤势只展示同一判定的既有结果；此处不施加伤势、不执行结算。
            var injury=CombatResolver.Outcome(result,p.modifier);
            _outcome.text+="\n<size=28>"+(injury==InjuryResult.None?"无伤":injury==InjuryResult.Light?"轻伤":"重伤")+"</size>";
        }
        yield return FadeIn(_outcomeStage,.24f);yield return FadeIn(_actionsStage,.14f);
        _coin.gameObject.SetActive(!p.FixedOutcome&&!p.rerolled&&InventoryModel.Instance.CountId(2)>0);
        _coin.interactable=true;_continue.interactable=true;
        if(_actionsStage){_actionsStage.interactable=true;_actionsStage.blocksRaycasts=true;}
        _ready=true;_reveal=null;
    }
    private static bool PauseReveal=>GuideHelpOverlay.BlocksInput;
    private IEnumerator WaitVisible(float duration)
    {
        float elapsed=0;
        do {yield return null;if(!PauseReveal)elapsed+=Time.unscaledDeltaTime;} while(elapsed<duration);
    }
    private IEnumerator FadeIn(CanvasGroup group,float duration)
    {
        float elapsed=0;
        while(elapsed<duration)
        {
            yield return null;if(PauseReveal)continue;
            elapsed+=Time.unscaledDeltaTime;
            if(group)group.alpha=Mathf.SmoothStep(0,1,Mathf.Clamp01(elapsed/duration));
        }
        if(group)group.alpha=1;
    }
    private IEnumerator NumberIn(CanvasGroup group,Text text,int value,float duration)
    {
        float elapsed=0;
        while(elapsed<duration)
        {
            yield return null;if(PauseReveal)continue;
            elapsed+=Time.unscaledDeltaTime;float t=Mathf.Clamp01(elapsed/duration);
            if(group)group.alpha=Mathf.SmoothStep(0,1,Mathf.Min(1,t*3));
            if(text)text.text=Mathf.RoundToInt(Mathf.Lerp(0,value,1-Mathf.Pow(1-t,3))).ToString();
        }
        if(group)group.alpha=1;if(text)text.text=value.ToString();
    }
    private static string Signed(int value)=>value>=0?"＋"+value:"−"+Mathf.Abs(value);
    private static string CompactDetails(D20Service.Presentation p)
    {
        if(string.IsNullOrEmpty(p.details))return "";
        var lines=p.details.Split('\n');var visible=new System.Collections.Generic.List<string>{lines[0]};
        // 这里只压缩现有检定快照的展示，不重新计算属性或思维效果。
        for(int i=1;i<lines.Length;i++)
        {
            string line=lines[i].Trim();
            if(line.StartsWith("依据你的思维—",StringComparison.Ordinal))
            {
                int separator=line.LastIndexOf('：');
                if(separator>=0)visible.Add("思维 "+line.Substring(separator+1).Replace("骰子修正","").TrimEnd('。'));
            }
            else if(line.StartsWith("你的思维—哈耳摩尼亚史官",StringComparison.Ordinal))visible.Add("史官 · "+PlayerBuildState.AttributeName(p.playerAttribute));
            else if(line.StartsWith("思维：",StringComparison.Ordinal)||line.StartsWith("回答修正",StringComparison.Ordinal))visible.Add(line);
        }
        return string.Join("\n",visible);
    }
    private void RefreshScroll()
    {
        if(!_noteScroll)return;
        Canvas.ForceUpdateCanvases();
        _note.rectTransform.SetSizeWithCurrentAnchors(RectTransform.Axis.Vertical,Mathf.Max(_noteScroll.viewport.rect.height,_note.preferredHeight+16));
        _noteScroll.StopMovement();_noteScroll.verticalNormalizedPosition=1;
    }
    private void Reroll()
    {
        if(GuideHelpOverlay.BlocksInput||!_ready||_done||_data.request.FixedOutcome||_data.request.rerolled||!InventoryModel.Instance.ConsumeId(2))return;
        // 重投仍只扣一枚硬币、建立一次结果，然后完整重播同一显示序列。
        _data.request.rerolled=true;_data.request.audioPresented=false;AudioManager.Instance?.PlayCue("item");D20Service.RollPresented(_data.request);
        SystemManager.Instance.Narrative?.Save();BeginReveal();
    }
    public void Complete()
    {
        if(GuideHelpOverlay.BlocksInput||_done||!_ready)return;
        _done=true;var p=_data.request;p.confirmed=true;var callback=_data.completed;
        RunStateManager.Instance.run.pendingDice=null;UIManager.Instance.Close(this);callback?.Invoke(p.Result());SystemManager.Instance.Narrative?.Save();
    }
    public override void OnClose()
    {
        if(_reveal!=null)StopCoroutine(_reveal);_reveal=null;_ready=false;
        base.OnClose();
    }
    // ESC与遮罩不能跳过检定；演出结束并明确确认后才执行剧情/结算。
    public override bool OnEscapePressed()=>true;
}
