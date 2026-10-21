using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;

/// <summary>自身类型、阶段思维和日常状态共用原面板；浏览不会暂停右侧剧情。</summary>
public sealed class PlayerBuildPanel : PanelBase
{
    [Serializable] public sealed class Entry { public string name, subtitle, description, brief, selected, effect; }
    [Serializable] public sealed class Copy
    {
        public string typeTitle, typeIntro, typeConfirmed, thoughtTitle, thoughtIntro, phaseTitle, phaseIntro;
        public Entry[] types, thoughts;
    }
    [SerializeField] private GameObject _choiceRoot, _statusRoot;
    [SerializeField] private Button[] _types, _thoughts;
    [SerializeField] private Text[] _typeValues, _thoughtDescriptions;
    [SerializeField] private Button _confirm, _back, _keep, _none;
    [SerializeField] private Text _summary, _stats, _mode, _effect, _progress, _scope;
    [SerializeField] private Image _progressFill;
    [SerializeField] private Text _choiceTitle, _intro, _detailTitle, _detail;
    [SerializeField] private ScrollRect _detailScroll;
    [SerializeField] private ScrollRect _effectScroll;
    [SerializeField] private Text _readingHint;
    [SerializeField] private Image _statusThoughtMark;
    private Action _created;
    private int _aptitude = -1, _thought = -1;
    private bool _choosing, _creating, _committed;
    private string _detailContent;
    private readonly List<HiddenVisual> _hiddenVisuals=new List<HiddenVisual>();
    private sealed class HiddenVisual { public CanvasGroup group;public float alpha;public bool interactable,blocks; }
    private static Copy _copy;
    public static Copy Presentation
    {
        get { if (_copy == null) _copy = JsonUtility.FromJson<Copy>(Resources.Load<TextAsset>("Narrative/playerPresentation").text); return _copy; }
    }
    public static bool IsChoosing => UIManager.Instance?.Get<PlayerBuildPanel>() is PlayerBuildPanel p && p.IsOpen && p._choosing;
    public static bool IsShowing => UIManager.Instance?.Get<PlayerBuildPanel>()?.IsOpen==true;
    public override bool IsModal => true;
    public string CurrentGuideTopic => !_choosing ? "player_status" : _creating ? "player_type" : "thought";
    public override void OnInit()
    {
        for (int i = 0; i < 3; i++)
        {
            int n = i;
            _types[i].onClick.AddListener(() => { if(SelectionInputBlocked)return;_aptitude = n; RefreshChoice(); });
            _thoughts[i].onClick.AddListener(() => { if(SelectionInputBlocked)return;_thought = n; RefreshChoice(); });
        }
        _confirm.onClick.AddListener(Confirm);
        _back.onClick.AddListener(() => UIManager.Instance.Close(this));
        if (_keep) _keep.onClick.AddListener(() => CommitThought(RunStateManager.Instance.run.player.thought));
        if (_none) _none.onClick.AddListener(() => CommitThought(-1));
        if (_detailScroll) _detailScroll.onValueChanged.AddListener(_ => RefreshReadingHint());
    }
    public override void OnOpen(object data = null)
    {
        _created = data as Action;
        var narrative = SystemManager.Instance.Narrative;
        _creating = narrative.NeedsPlayerCreation;
        _choosing = _created != null && (_creating || narrative.NeedsThoughtSelection);
        _committed = false;
        _detailContent = null;
        _aptitude = -1; _thought = _creating ? -1 : RunStateManager.Instance.run.player.thought;
        base.OnOpen(data);
        _choiceRoot.SetActive(_choosing); _statusRoot.SetActive(!_choosing);
        // 状态查阅与脑内选择都使用纯场景背景；底层页面只藏画面，不关闭或重置。
        HideChoiceBusinessVisuals();
        if (_choosing) {
            RefreshChoice();
            if(EventSystem.current)EventSystem.current.SetSelectedGameObject((_creating?_types[0]:_thoughts[Mathf.Max(0,_thought)]).gameObject);
        }
        else { RefreshStatus(); if(_effectScroll)_effectScroll.verticalNormalizedPosition=1; }
    }
    public override void OnRefresh(object data = null) { if (_choosing) RefreshChoice(); else RefreshStatus(); }
    private void OnEnable() => TopBarRefreshEvent.Register(RefreshCurrent);
    private void OnDisable() => TopBarRefreshEvent.UnRegister(RefreshCurrent);
    private void RefreshCurrent() { if (IsOpen) OnRefresh(); }
    private void RefreshChoice()
    {
        var copy = Presentation; var state = RunStateManager.Instance.run.player;
        for (int i = 0; i < 3; i++)
        {
            _types[i].gameObject.SetActive(_creating); _thoughts[i].gameObject.SetActive(!_creating);
            Tint(_types[i], i == _aptitude); Tint(_thoughts[i], i == _thought);
            if (_typeValues != null && i < _typeValues.Length && _typeValues[i])
                _typeValues[i].text = i == 0 ? "感知55　学识35　精神力35" : i == 1 ? "感知35　学识55　精神力35" : "感知35　学识35　精神力55";
        }
        bool firstThought = state.thoughtConfirmedPhase <= 0;
        _choiceTitle.text = _creating ? copy.typeTitle : firstThought ? copy.thoughtTitle : copy.phaseTitle;
        _intro.text = _creating ? copy.typeIntro : firstThought ? copy.thoughtIntro : copy.phaseIntro;
        _intro.gameObject.SetActive(true);_summary.gameObject.SetActive(false);
        if (_keep) _keep.gameObject.SetActive(!_creating && !firstThought);
        if (_none) _none.gameObject.SetActive(!_creating);
        int selected = _creating ? _aptitude : _thought;
        _confirm.interactable = selected >= 0;
        _confirm.GetComponentInChildren<Text>().text = _creating ? "就凭这个" : "我好像想起了这个";
        var confirmHint=_confirm.GetComponent<UIHoverHint>();if(confirmHint)confirmHint.enabled=!_creating;
        _summary.text = _creating ? selected < 0 ? "选择你的处事本能。" : "自身类型：" + copy.types[selected].name
            : selected < 0 ? "选择一条已解锁的思维。" : "当前选择：" + copy.thoughts[selected].name;
        // 尚未提交选择时先展示首项说明；确认仍然禁用，预览不替玩家作出选择。
        int preview=Mathf.Max(0,selected);
        {
            var entry = _creating ? copy.types[preview] : copy.thoughts[preview];
            _detailTitle.text = entry.name;
            _detail.text = _creating ? entry.subtitle + "\n" + _typeValues[preview].text + "\n\n" + entry.description + "\n\n" + entry.brief + "\n\n" + entry.selected
                : entry.description + "\n\n" + entry.effect + "\n\n" + entry.selected;
        }
        RefreshScroll();ConfigureChoiceNavigation();
    }
    private void RefreshScroll()
    {
        if (!_detailScroll || !_detail) return;
        bool changed=_detailContent!=_detail.text;
        float position=_detailScroll.verticalNormalizedPosition;
        _detailContent=_detail.text;
        Canvas.ForceUpdateCanvases();
        _detail.rectTransform.SetSizeWithCurrentAnchors(RectTransform.Axis.Vertical, Mathf.Max(_detailScroll.viewport.rect.height, _detail.preferredHeight + 36));
        _detailScroll.StopMovement(); _detailScroll.verticalNormalizedPosition = changed?1:position;
        RefreshReadingHint();
    }
    private void RefreshReadingHint()
    {
        if(!_readingHint||!_detailScroll)return;
        _readingHint.gameObject.SetActive(false);
    }
    private void ConfigureChoiceNavigation()
    {
        var cards=_creating?_types:_thoughts;var actions=new List<Button>();
        foreach(var button in new[]{_keep,_confirm,_none})if(button&&button.gameObject.activeSelf&&button.interactable)actions.Add(button);
        for(int i=0;i<cards.Length;i++) {
            var nav=cards[i].navigation;nav.mode=Navigation.Mode.Explicit;
            nav.selectOnUp=i==2?cards[0]:null;nav.selectOnDown=i<2?cards[2]:actions.Count>0?actions[0]:null;
            nav.selectOnLeft=i==0?null:cards[0];nav.selectOnRight=i==1?null:cards[1];cards[i].navigation=nav;
        }
        int selected=_creating?_aptitude:_thought;
        for(int i=0;i<actions.Count;i++) {
            var nav=actions[i].navigation;nav.mode=Navigation.Mode.Explicit;nav.selectOnUp=cards[Mathf.Max(0,selected)];
            nav.selectOnLeft=actions[(i+actions.Count-1)%actions.Count];nav.selectOnRight=actions[(i+1)%actions.Count];actions[i].navigation=nav;
        }
    }
    private static void Tint(Button button, bool selected)
    {
        var border = button.GetComponent<Outline>(); if (border) border.enabled = false;
        var background=button.GetComponent<Image>();if(background)background.color=Color.clear;
        var marker = button.transform.Find("Selected");
        if (marker) {var icon=marker.GetComponent<Image>();marker.gameObject.SetActive(icon?selected:true);var text=marker.GetComponent<Text>();if(text)text.text=selected?"●":"○";}
        var colors = button.colors; colors.normalColor = selected ? new Color(1, .91f, .71f) : Color.white;
        colors.highlightedColor = new Color(1, .94f, .8f); colors.selectedColor = colors.normalColor;
        colors.pressedColor = new Color(.78f, .7f, .54f); colors.disabledColor = new Color(.45f, .43f, .4f); button.colors = colors;
    }
    private void Confirm()
    {
        if (SelectionInputBlocked || !_choosing || _committed) return;
        if (!_creating) { CommitThought(_thought); return; }
        if (_aptitude < 0 || !SystemManager.Instance.Narrative.CreatePlayer(_aptitude, -1)) return;
        Finish(); ToastManager.ShowFeedback(Presentation.typeConfirmed);
    }
    private void CommitThought(int value)
    {
        if (SelectionInputBlocked || !_choosing || _creating || _committed || !SystemManager.Instance.Narrative.ConfirmThought(value)) return;
        Finish();
        if (value >= 0) ToastManager.ShowFeedback(Presentation.thoughts[value].selected);
    }
    private void Finish()
    {
        _committed = true; var callback = _created; _created = null;
        UIManager.Instance.Close(this); callback?.Invoke();
    }
    // 鼠标由遮罩阻挡；键盘Submit也必须等待揭幕或教程结束。
    private static bool SelectionInputBlocked=>PresentationEffects.IsBusy||GuideHelpOverlay.BlocksInput;
    private void RefreshStatus()
    {
        var state = RunStateManager.Instance.run.player;
        _stats.text = "自身类型：" + (state.created ? Presentation.types[state.aptitude].name : "尚未选择") + "\n\n感知　" + state.perception + "　　 学识　" + state.knowledge + "　　 精神力　" + state.willpower;
        var run = RunStateManager.Instance.run;
        if(_statusThoughtMark)_statusThoughtMark.gameObject.SetActive(state.thought >= 0 && state.ThoughtActive(run.currentDay,run.currentPhase));
        _mode.text = "当前思维：" + (state.thought >= 0 && state.ThoughtActive(run.currentDay, run.currentPhase) ? PlayerBuildState.ThoughtNames[state.thought] : "尚未采用");
        _effect.text = state.ThoughtActive(run.currentDay, run.currentPhase) ? Presentation.thoughts[state.thought].effect : "";
        if(_effectScroll) {
            float position=_effectScroll.verticalNormalizedPosition;Canvas.ForceUpdateCanvases();
            _effect.rectTransform.SetSizeWithCurrentAnchors(RectTransform.Axis.Vertical,Mathf.Max(_effectScroll.viewport.rect.height,_effect.preferredHeight+24));
            _effectScroll.verticalNormalizedPosition=position;
        }
        _scope.text = "每个阶段开始接待前可调整思维。";
        _scope.gameObject.SetActive(false);
        if (_progress) _progress.gameObject.SetActive(false); if (_progressFill) _progressFill.gameObject.SetActive(false);
    }
    public override bool OnEscapePressed() { if (!_choosing) UIManager.Instance.Close(this); return true; }
    private void HideChoiceBusinessVisuals()
    {
        // 脑内选择期间只隐藏经营控件的图像与命中，不关闭业务页面、不改变棋盘实例。
        foreach(var panel in FindObjectsOfType<PanelBase>(true))
            if(panel!=this && (panel is HUDPanel || panel is PreparationPanel || panel is ShopPanel || panel is BackpackPanel || panel is TalkPanel))HideChoiceVisual(panel.gameObject);
        if(BackpackBoard.Instance)HideChoiceVisual(BackpackBoard.Instance.transform.parent.gameObject);
    }
    private void HideChoiceVisual(GameObject target)
    {
        var group=target.GetComponent<CanvasGroup>();if(!group)group=target.AddComponent<CanvasGroup>();
        if(_hiddenVisuals.Exists(v=>v.group==group))return;
        _hiddenVisuals.Add(new HiddenVisual{group=group,alpha=group.alpha,interactable=group.interactable,blocks=group.blocksRaycasts});
        group.alpha=0;group.interactable=false;group.blocksRaycasts=false;
    }
    private void RestoreChoiceBusinessVisuals()
    {
        foreach(var visual in _hiddenVisuals)if(visual.group){visual.group.alpha=visual.alpha;visual.group.interactable=visual.interactable;visual.group.blocksRaycasts=visual.blocks;}
        _hiddenVisuals.Clear();
    }
    public override void OnClose() { RestoreChoiceBusinessVisuals();base.OnClose(); SystemManager.Instance?.Narrative?.Save(); }
    public override void OnDestroy() { RestoreChoiceBusinessVisuals();base.OnDestroy(); }
}
