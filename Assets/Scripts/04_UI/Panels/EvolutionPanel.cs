using System;
using System.Linq;
using UnityEngine;
using UnityEngine.UI;

/// <summary>复用现有魔物实例与背包。方向只作预览，确认后才消耗材料并提交。</summary>
public sealed class EvolutionPanel : PanelBase
{
    [SerializeField] private Image _monsterIcon;
    [SerializeField] private Text _title, _description;
    [SerializeField] private Button[] _directions;
    [SerializeField] private Button _back;
    [SerializeField] private Button _confirm;
    [SerializeField] private Text _cost;
    [SerializeField] private Text _monsterName;
    [SerializeField] private Text[] _directionMarks;
    [SerializeField] private Image[] _directionCheckmarks;
    private string _uid;
    private int _selected = -1;
    private bool _submitted;
    public static bool IsShowing => UIManager.Instance?.Get<EvolutionPanel>()?.IsOpen==true;
    public override bool IsModal => true;
    public static readonly string[] Names={"激进","慎重","狂妄","保守","胆小","混乱","邪异"};
    public static readonly SevenDimension[] Down={SevenDimension.Reason,SevenDimension.Courage,SevenDimension.Piety,SevenDimension.Tolerance,SevenDimension.Benevolence,SevenDimension.Resolve,SevenDimension.Diligence};
    public static readonly SevenDimension[] Up={SevenDimension.Courage,SevenDimension.Tolerance,SevenDimension.Resolve,SevenDimension.Reason,SevenDimension.Diligence,SevenDimension.Piety,SevenDimension.Benevolence};
    public static int Index(string kind)=>Array.IndexOf(Names,kind);
    public static string Effect(string kind)
    {
        int i=Index(kind);if(i<0)return "";int n=SystemManager.Instance.Narrative?.EvolutionDelta??5;
        return $"冒险者生还后：{SevenDimensionDisplay.GetName(Down[i])} −{n}，{SevenDimensionDisplay.GetName(Up[i])} ＋{n}";
    }
    public static string MonsterName(string original,string kind)=>Index(kind)<0?original:kind+"的"+original;
    public override void OnInit()
    {
        _back.onClick.AddListener(()=>UIManager.Instance.Close(this));
        for(int i=0;i<_directions.Length;i++){int index=i;_directions[i].onClick.AddListener(()=>Choose(index));}
        _confirm.onClick.AddListener(Confirm);
    }
    private void OnEnable() => BackpackChanged.Register(RefreshSelection);
    private void OnDisable() => BackpackChanged.UnRegister(RefreshSelection);
    public override void OnOpen(object data=null)
    {
        base.OnOpen(data);_uid=data as string;_submitted=false;
        var item=Find();
        if(item==null||item.Evolved||item.IsResting||SystemManager.Instance.Narrative?.IsPreparing!=true){UIManager.Instance.Close(this);return;}
        var run=SystemManager.Instance.RunStateManager.run;
        // 只恢复同一实例尚未提交的方向；打开另一只魔物时不沿用旧选择。
        _selected=run.pendingEvolutionUid==_uid?Index(run.pendingEvolutionKind):-1;
        run.pendingEvolutionUid=_uid;
        run.pendingEvolutionKind=_selected>=0?Names[_selected]:null;
        _monsterIcon.sprite=item.Def.GetIcon(false);_monsterIcon.preserveAspect=true;
        if(_monsterName)_monsterName.text=item.Def.DisplayName;
        _title.text="你希望这只魔物更加……";
        for(int i=0;i<_directions.Length;i++)
        {
            // 七个方向始终展示原有的两项变化；排版围绕真实魔物图，不改变进化规则。
            var label=_directions[i].transform.Find("Label")?.GetComponent<Text>()??_directions[i].GetComponentInChildren<Text>();
            label.text=Names[i]+"\n<size=24>"+Effect(Names[i]).Replace("冒险者生还后：","")+"</size>";
        }
        RefreshSelection();
        SystemManager.Instance.Narrative.Save();
    }
    public override void OnRefresh(object data=null) => RefreshSelection();
    PlacedItem Find()=>BackpackBoard.Instance?.Items.FirstOrDefault(v=>v?.Item?.Uid==_uid)?.Item;
    public void Choose(int index)
    {
        if(_submitted||!IsOpen||index<0||index>=Names.Length)return;
        var item=Find();if(item==null||item.Evolved||item.IsResting||SystemManager.Instance.Narrative?.IsPreparing!=true)return;
        _selected=index;
        SystemManager.Instance.RunStateManager.run.pendingEvolutionKind=Names[index];
        RefreshSelection();
        SystemManager.Instance.Narrative.Save();
    }
    private void RefreshSelection()
    {
        if(!IsOpen)return;
        int count=InventoryModel.Instance?.CountId(7)??0;
        _cost.text="消耗：深渊残质 ×1　持有："+count;
        _description.text=_selected<0?"选择一种进化方向":"已选："+Names[_selected]+"　"+Effect(Names[_selected]);
        var item=Find();
        bool available=item!=null&&!item.Evolved&&!item.IsResting&&SystemManager.Instance.Narrative?.IsPreparing==true;
        _confirm.interactable=available&&!_submitted&&_selected>=0&&count>0;
        for(int i=0;i<_directions.Length;i++)
        {
            bool selected=i==_selected;
            var button=_directions[i];button.interactable=available&&!_submitted;
            if(_directionCheckmarks!=null&&i<_directionCheckmarks.Length&&_directionCheckmarks[i])_directionCheckmarks[i].gameObject.SetActive(selected);
            if(_directionMarks!=null&&i<_directionMarks.Length&&_directionMarks[i])_directionMarks[i].text=selected?"●":"○";
            var colors=button.colors;
            colors.normalColor=selected?new Color(1f,.84f,.49f):Color.white;
            colors.selectedColor=colors.normalColor;button.colors=colors;
            var outline=button.GetComponent<Outline>();if(outline)outline.enabled=selected;
        }
    }
    public void Confirm()
    {
        if(_submitted||!IsOpen||_selected<0||_selected>=Names.Length)return;
        var item=Find();
        if(item==null||item.Evolved||item.IsResting||SystemManager.Instance.Narrative?.IsPreparing!=true)return;
        // 此处是唯一扣料入口。缺料、返回、方向切换与读档均不提交进化。
        if(InventoryModel.Instance?.ConsumeId(7)!=true){RefreshSelection();ToastManager.ShowFeedback("背包里没有深渊残质。");return;}
        _submitted=true;_confirm.interactable=false;
        AudioManager.Instance?.PlayCue("evolve");
        item.Evolved=true;item.EvolutionKind=Names[_selected];item.View.Rebuild();
        UIManager.Instance.Close(this);SystemManager.Instance.Narrative.Save();ToastManager.ShowFeedback("魔物进化成功");
    }
    public override bool OnEscapePressed(){UIManager.Instance.Close(this);return true;}
    public override void OnClose()
    {
        var run=SystemManager.Instance.RunStateManager.run;
        run.pendingEvolutionUid=null;run.pendingEvolutionKind=null;
        _selected=-1;base.OnClose();SystemManager.Instance.Narrative?.Save();
    }
}
