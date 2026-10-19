using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;

/// <summary>只负责终局展示：七人选择、确认、CG和完成页。判定与存档仍由原剧情系统管理。</summary>
public sealed class EndingPresentationPanel : PanelBase
{
    [SerializeField] private GameObject _selectionRoot,_confirmRoot,_cgRoot,_completeRoot;
    [SerializeField] private Button[] _actorButtons;
    [SerializeField] private Image[] _portraits;
    [SerializeField] private Text[] _names,_tendencies;
    [SerializeField] private Image[] _actorFocus;
    [SerializeField] private Button _confirm,_cancel,_stay,_returnTitle,_settings;
    [SerializeField] private Text _confirmText,_endingText,_continueHint;
    [SerializeField] private Image _cgImage,_black,_dim;
    [SerializeField] private Image _completionImage;
    [SerializeField] private Material _deadMaterial;
    [Header("终局表达：感知、学识、精神力，顺序与按钮一致")]
    [SerializeField] private GameObject _expressionRoot;
    [SerializeField] private Button[] _expressionButtons;
    [SerializeField] private Text[] _expressionTexts;
    [SerializeField] private Text[] _expressionMarks;
    [SerializeField] private Text _requestTitle;
    [Header("确认框两种布局：由预制体保存，切换模式时应用")]
    [SerializeField] private RectTransform _confirmFrame,_confirmBacking;
    [SerializeField] private ConfirmationLayout _expandedLayout=new ConfirmationLayout();
    [SerializeField] private ConfirmationLayout _compactLayout=new ConfirmationLayout();
    [Serializable] private sealed class ConfirmationLayout
    {
        public Vector2 frameSize,backingSize,titlePosition,titleSize,infoPosition,infoSize,confirmPosition,cancelPosition;
    }
    private bool? _expandedApplied;

    // 《说明6》原文；难度由同一份继任评估追加，不在界面另算一套概率。
    private static readonly string[] ExpressionAttributes={"perception","knowledge","willpower"};
    private static readonly string[] ExpressionCopy={
        "试图察觉冒险者的微表情，找出冒险者接任的理由。",
        "从接任地牢主人的好处谈起，找出冒险者接任的理由。",
        "其实我已经找到了他的“心之壁”，就在这里……"
    };
    private int _hover=-1,_focus=-1,_generation;
    private int _expressionHover=-1,_expressionFocus=-1;
    private bool _cgReady,_transitioning,_keyboardFocus;
    private Vector3 _lastPointerPosition;
    private string _renderedStage;
    private readonly List<VisualState> _hiddenBusiness=new List<VisualState>();
    private sealed class VisualState
    {
        public CanvasGroup group;
        public float alpha;
        public bool interactable,blocksRaycasts;
    }
    private NarrativeSystem Campaign=>SystemManager.Instance.Narrative;
    public static bool IsShowing=>UIManager.Instance!=null&&UIManager.Instance.IsOpen<EndingPresentationPanel>();
    public bool IsSelectingForGuide=>IsOpen&&Campaign.EndingStage=="ending_selection";
    public override bool IsModal=>true;
    public override void OnInit()
    {
        for(int i=0;i<_actorButtons.Length;i++) {
            int index=i;_actorButtons[i].onClick.AddListener(()=>Campaign.PreviewEndingCandidate(index+1));
            var trigger=_actorButtons[i].gameObject.AddComponent<EventTrigger>();
            var enter=new EventTrigger.Entry{eventID=EventTriggerType.PointerEnter};enter.callback.AddListener(_=>{if(_actorButtons[index].interactable){_hover=index;_keyboardFocus=false;}});trigger.triggers.Add(enter);
            var exit=new EventTrigger.Entry{eventID=EventTriggerType.PointerExit};exit.callback.AddListener(_=>{if(_hover==index)_hover=-1;});trigger.triggers.Add(exit);
            // 键盘选中与鼠标悬停共用下面的立绘提亮，死亡人物不参与导航。
            var selected=new EventTrigger.Entry{eventID=EventTriggerType.Select};selected.callback.AddListener(_=>{if(_actorButtons[index].interactable){_focus=index;_keyboardFocus=true;}});trigger.triggers.Add(selected);
            var deselected=new EventTrigger.Entry{eventID=EventTriggerType.Deselect};deselected.callback.AddListener(_=>{if(_focus==index)_focus=-1;});trigger.triggers.Add(deselected);
        }
        if(_expressionButtons!=null)for(int i=0;i<_expressionButtons.Length&&i<ExpressionAttributes.Length;i++) {
            int index=i;
            _expressionButtons[i].onClick.AddListener(()=>{
                Campaign.SelectEndingExpression(ExpressionAttributes[index]);
                RefreshExpressionPreview();
            });
            // 整行文字可点击；悬停、键盘焦点与已经选定的表达分别反馈。
            var feedback=_expressionButtons[i].gameObject.AddComponent<EventTrigger>();
            ExpressionEvent(feedback,EventTriggerType.PointerEnter,()=>{_expressionHover=index;RefreshExpressionVisuals();});
            ExpressionEvent(feedback,EventTriggerType.PointerExit,()=>{if(_expressionHover==index)_expressionHover=-1;RefreshExpressionVisuals();});
            ExpressionEvent(feedback,EventTriggerType.Select,()=>{_expressionFocus=index;RefreshExpressionVisuals();});
            ExpressionEvent(feedback,EventTriggerType.Deselect,()=>{if(_expressionFocus==index)_expressionFocus=-1;RefreshExpressionVisuals();});
        }
        _confirm.onClick.AddListener(()=>Campaign.ConfirmEndingCandidate());_cancel.onClick.AddListener(()=>Campaign.CancelEndingCandidate());
        _stay.onClick.AddListener(()=>Campaign.ConfirmEndingCandidate(true));
        _settings.onClick.AddListener(()=>UIManager.Instance.Open<SettingsPanel>());
        _returnTitle.onClick.AddListener(EndingCredits.Show);
    }
    public override void OnOpen(object data=null){base.OnOpen(data);_lastPointerPosition=Input.mousePosition;HideBusinessVisuals();Render();}
    public override void OnRefresh(object data=null){Render();}
    private void Render()
    {
        StopAllCoroutines();_generation++;_cgReady=false;_transitioning=false;
        string stage=Campaign.EndingStage;bool select=stage=="ending_selection"||stage=="ending_confirm";
        bool changed=stage!=_renderedStage;_renderedStage=stage;
        if(changed) {
            // 确认框打开时隐藏底层页标题；返回选人后恢复，人物位置与按钮仍由原流程保留。
            var selectionTitle=_selectionRoot.transform.Find("Title");
            if(selectionTitle)selectionTitle.gameObject.SetActive(stage!="ending_confirm");
            var selectionHelp=_selectionRoot.transform.Find("SelectionHelp");
            if(selectionHelp)selectionHelp.gameObject.SetActive(false);
        }
        HideBusinessVisuals();
        _selectionRoot.SetActive(select);_confirmRoot.SetActive(stage=="ending_confirm");_cgRoot.SetActive(false);_dim.gameObject.SetActive(stage!="ending_cg");_completeRoot.SetActive(stage=="ending_complete");
        _black.color=new Color(0,0,0,0);_black.raycastTarget=false;
        if(select) {
            for(int i=0;i<7;i++) {
                var a=Campaign.AssessSuccession(i+1);_actorButtons[i].interactable=!a.dead;
                _portraits[i].material=a.dead?_deadMaterial:null;
                _names[i].text=a.name;_tendencies[i].text=a.dead?"已死亡":a.tendency;_portraits[i].color=new Color(.43f,.43f,.43f,1);
                LoadPortrait(i,Campaign.BasePortrait(i+1),_generation);
            }
            ConfigureActorNavigation();
            if(stage=="ending_confirm") {
                RefreshExpressionPreview();
                if(changed&&EventSystem.current!=null) {
                    var a=Campaign.AssessSuccession(Campaign.EndingCandidate);
                    int index=Array.IndexOf(ExpressionAttributes,Campaign.EndingExpression);
                    var first=a.gap>0&&a.gap<=20&&_expressionButtons!=null&&index>=0&&index<_expressionButtons.Length?_expressionButtons[index]:_confirm;
                    EventSystem.current.SetSelectedGameObject(first.gameObject);
                }
            }
            else if(changed&&EventSystem.current!=null)EventSystem.current.SetSelectedGameObject(null);
        }
        if(stage=="ending_cg")StartCoroutine(ShowCG(_generation));
        if(stage=="ending_complete") {
            _returnTitle.GetComponentInChildren<Text>().text="继续";
            _endingText.text="结局："+Campaign.EndingActorName+"接任结局完成";
            LoadCompletionImage(_generation);
            if(changed&&EventSystem.current)EventSystem.current.SetSelectedGameObject(_returnTitle.gameObject);
        }
    }

    /// <summary>表达只改变预览上下文；点击正式确认后，剧情系统读取同一个已保存的表达。</summary>
    private void RefreshExpressionPreview()
    {
        if(Campaign.EndingStage!="ending_confirm")return;
        var a=Campaign.AssessSuccession(Campaign.EndingCandidate);
        bool expressions=a.gap>0&&a.gap<=20;
        if(_expressionRoot)_expressionRoot.SetActive(expressions);
        ApplyConfirmationLayout(expressions);
        if(_requestTitle)_requestTitle.text=a.name;
        string summary="接任倾向："+a.tendency;
        _confirmText.text=summary;
        _confirmText.text+="\n<size=24>确认后无法改选；说服失败将被吞噬。</size>";
        SetButtonText(_confirm,a.gap==0?"交付经营权":expressions?"就这样开口":"仍然开口");
        SetButtonText(_cancel,expressions?"再想一想":"返回");
        if(!expressions)return;
        for(int i=0;i<ExpressionAttributes.Length;i++) {
            var preview=Campaign.AssessSuccession(Campaign.EndingCandidate,ExpressionAttributes[i]);
            if(_expressionTexts!=null&&i<_expressionTexts.Length&&_expressionTexts[i])
                _expressionTexts[i].text=ExpressionCopy[i]+"\n"+preview.Annotation;
        }
        RefreshExpressionVisuals();
    }
    private static void ExpressionEvent(EventTrigger trigger,EventTriggerType type,Action action)
    {
        var entry=new EventTrigger.Entry{eventID=type};entry.callback.AddListener(_=>action());trigger.triggers.Add(entry);
    }
    private void RefreshExpressionVisuals()
    {
        if(Campaign.EndingStage!="ending_confirm")return;
        for(int i=0;i<ExpressionAttributes.Length;i++) {
            bool chosen=ExpressionAttributes[i]==Campaign.EndingExpression;
            bool focus=i==_expressionHover||i==_expressionFocus;
            var color=chosen||focus?new Color(1f,.94f,.79f,1):new Color(.86f,.8f,.69f,1);
            if(_expressionTexts!=null&&i<_expressionTexts.Length&&_expressionTexts[i])_expressionTexts[i].color=color;
            if(_expressionMarks!=null&&i<_expressionMarks.Length&&_expressionMarks[i]) {
                _expressionMarks[i].text=chosen?"●":"○";
                _expressionMarks[i].color=chosen?new Color(1f,.75f,.35f,1):color;
            }
            if(_expressionButtons!=null&&i<_expressionButtons.Length&&_expressionButtons[i]) {
                var colors=_expressionButtons[i].colors;
                colors.normalColor=chosen?new Color(1f,.95f,.85f,1):new Color(.75f,.75f,.75f,1);
                colors.highlightedColor=colors.selectedColor=Color.white;
                _expressionButtons[i].colors=colors;
            }
        }
    }
    /// <summary>自动接任和必败只显示紧凑确认；表达页使用编辑器保存的大框，不在每帧重排。</summary>
    private void ApplyConfirmationLayout(bool expanded)
    {
        if(_expandedApplied==expanded)return;
        _expandedApplied=expanded;
        var preset=expanded?_expandedLayout:_compactLayout;
        if(_confirmFrame&&_confirmBacking) {
            _confirmFrame.sizeDelta=preset.frameSize;
            _confirmBacking.sizeDelta=preset.backingSize;
            _confirmBacking.anchoredPosition=_confirmFrame.anchoredPosition;
            _requestTitle.rectTransform.anchoredPosition=preset.titlePosition;
            _requestTitle.rectTransform.sizeDelta=preset.titleSize;
            _confirmText.rectTransform.anchoredPosition=preset.infoPosition;
            _confirmText.rectTransform.sizeDelta=preset.infoSize;
            _confirmText.alignment=expanded?TextAnchor.UpperLeft:TextAnchor.MiddleCenter;
            ((RectTransform)_confirm.transform).anchoredPosition=preset.confirmPosition;
            ((RectTransform)_cancel.transform).anchoredPosition=preset.cancelPosition;
        }
        // 紧凑页的键盘焦点只在两个可见按钮间移动，不再指向已隐藏的表达选项。
        var confirmNav=_confirm.navigation;var cancelNav=_cancel.navigation;
        var lastExpression=expanded&&_expressionButtons!=null&&_expressionButtons.Length>0?_expressionButtons[_expressionButtons.Length-1]:null;
        confirmNav.selectOnUp=lastExpression?lastExpression:_cancel;
        cancelNav.selectOnUp=lastExpression?lastExpression:_confirm;
        _confirm.navigation=confirmNav;_cancel.navigation=cancelNav;
    }
    private static void SetButtonText(Button button,string text)
    {
        var label=button.GetComponentInChildren<Text>(true);if(label)label.text=text;
    }
    private void ConfigureActorNavigation()
    {
        for(int i=0;i<_actorButtons.Length;i++) {
            var navigation=_actorButtons[i].navigation;navigation.mode=Navigation.Mode.Explicit;
            navigation.selectOnLeft=NextAliveButton(i,-1);navigation.selectOnRight=NextAliveButton(i,1);navigation.selectOnDown=_stay;
            _actorButtons[i].navigation=navigation;
        }
        var stayNavigation=_stay.navigation;stayNavigation.mode=Navigation.Mode.Explicit;
        stayNavigation.selectOnUp=NextAliveButton(-1,1);stayNavigation.selectOnLeft=_settings;stayNavigation.selectOnRight=_settings;_stay.navigation=stayNavigation;
        var settingsNavigation=_settings.navigation;settingsNavigation.mode=Navigation.Mode.Explicit;
        settingsNavigation.selectOnLeft=_stay;settingsNavigation.selectOnDown=_stay;_settings.navigation=settingsNavigation;
    }
    private Button NextAliveButton(int from,int direction)
    {
        for(int step=1;step<=_actorButtons.Length;step++) {
            int index=(from+direction*step+_actorButtons.Length*2)%_actorButtons.Length;
            if(_actorButtons[index].interactable)return _actorButtons[index];
        }
        return null;
    }
    private bool IsTopPanel()
    {
        var stack=UIManager.Instance.OpenStack;return stack.Count>0&&stack[stack.Count-1]==this;
    }
    // 终局只隐藏经营控件的画面与命中，不关闭面板，不触发棋盘注销、退仓或退还魔物。
    private void HideBusinessVisuals()
    {
        foreach(var panel in FindObjectsOfType<PanelBase>(true)) {
            string name=panel.GetType().Name;
            if(name=="HUDPanel"||name=="PreparationPanel"||name=="ShopPanel"||name=="BackpackPanel"||
                name=="AbyssalAltarPanel"||name=="DecomposePanel"||name=="DailySettlementPanel"||name=="CommissionPanel")
                HideVisual(panel.gameObject);
        }
        if(BackpackBoard.Instance!=null)HideVisual(BackpackBoard.Instance.gameObject);
    }
    private void HideVisual(GameObject target)
    {
        var group=target.GetComponent<CanvasGroup>();if(!group)group=target.AddComponent<CanvasGroup>();
        if(_hiddenBusiness.Exists(state=>state.group==group))return;
        _hiddenBusiness.Add(new VisualState{group=group,alpha=group.alpha,interactable=group.interactable,blocksRaycasts=group.blocksRaycasts});
        group.alpha=0;group.interactable=false;group.blocksRaycasts=false;
    }
    private void RestoreBusinessVisuals()
    {
        foreach(var state in _hiddenBusiness)if(state.group) {
            state.group.alpha=state.alpha;state.group.interactable=state.interactable;state.group.blocksRaycasts=state.blocksRaycasts;
        }
        _hiddenBusiness.Clear();
    }
    private async void LoadPortrait(int index,string address,int generation)
    {
        var sprite=await SpriteLoader.LoadAsync(address);
        if(this==null||generation!=_generation)return;_portraits[index].sprite=sprite;
    }
    private async void LoadCompletionImage(int generation)
    {
        if(!_completionImage)return;
        _completionImage.enabled=false;
        var sprite=await SpriteLoader.LoadAsync(Campaign.EndingCGAddress);
        if(this==null||generation!=_generation||!_completionImage)return;
        _completionImage.sprite=sprite;_completionImage.enabled=sprite!=null;
    }
    private IEnumerator Fade(float from,float to,float duration)
    {
        _black.raycastTarget=true;float t=0;
        while(t<duration){t+=Time.unscaledDeltaTime;_black.color=new Color(0,0,0,Mathf.Lerp(from,to,Mathf.Clamp01(t/duration)));yield return null;}
        _black.color=new Color(0,0,0,to);_black.raycastTarget=to>0;
    }
    private IEnumerator ShowCG(int generation)
    {
        _transitioning=true;_continueHint.text="";_cgImage.enabled=false;
        // 先黑场再换图，CG未加载完成时不会闪出白块。
        bool restoring=!string.IsNullOrEmpty(Campaign.EndingPresentationPhase);
        if(!restoring){Campaign.SetEndingPresentationPhase("fade_out");yield return Fade(0,1,.6f);Campaign.SetEndingPresentationPhase("black");yield return new WaitForSecondsRealtime(.25f);}
        else _black.color=Color.black;
        UIManager.Instance.Close<TalkPanel>();_cgRoot.SetActive(true);
        var task=SpriteLoader.LoadAsync(Campaign.EndingCGAddress);while(!task.IsCompleted)yield return null;
        if(generation!=_generation)yield break;_cgImage.sprite=task.Result;_cgImage.enabled=true;
        Campaign.SetEndingPresentationPhase("fade_in");yield return Fade(1,0,.8f);Campaign.SetEndingPresentationPhase("display");
        // 图像已经加载并淡亮显示后才解锁；选择人物、确认和黑场均不登记。
        if(_cgImage.sprite!=null)CGGalleryProgress.RecordShown(Campaign.EndingCGAddress);
        yield return new WaitForSecondsRealtime(3f);
        _continueHint.text="点击继续";_cgReady=true;_transitioning=false;
    }
    private IEnumerator FinishCG()
    {
        _cgReady=false;_transitioning=true;_continueHint.text="";Campaign.SetEndingPresentationPhase("finish");
        yield return Fade(0,1,.6f);yield return new WaitForSecondsRealtime(.25f);
        Campaign.CompleteEndingCG();_cgRoot.SetActive(false);_completeRoot.SetActive(true);
        _returnTitle.GetComponentInChildren<Text>().text="继续";
        _endingText.text="结局："+Campaign.EndingActorName+"接任结局完成";
        if(_completionImage){_completionImage.sprite=_cgImage.sprite;_completionImage.enabled=_cgImage.sprite!=null;}
        yield return Fade(1,0,.8f);_transitioning=false;
        if(EventSystem.current)EventSystem.current.SetSelectedGameObject(_returnTitle.gameObject);
    }
    private void Update()
    {
        if(!IsOpen)return;
        if(GuideHelpOverlay.BlocksInput)return;
        bool select=Campaign.EndingStage=="ending_selection";
        if(Input.mousePosition!=_lastPointerPosition){_lastPointerPosition=Input.mousePosition;_keyboardFocus=false;}
        // 初始保持全员压暗；第一次按方向键时再建立键盘焦点。
        bool navigationKey=Input.GetKeyDown(KeyCode.LeftArrow)||Input.GetKeyDown(KeyCode.RightArrow)||Input.GetKeyDown(KeyCode.UpArrow)||Input.GetKeyDown(KeyCode.DownArrow)||Input.GetKeyDown(KeyCode.Tab);
        var selectedObject=EventSystem.current!=null?EventSystem.current.currentSelectedGameObject:null;
        bool noValidSelection=selectedObject==null||!selectedObject.activeInHierarchy;
        if(select&&navigationKey&&IsTopPanel())_keyboardFocus=true;
        if(select&&_focus<0&&noValidSelection&&IsTopPanel()&&navigationKey) {
            var first=NextAliveButton(Input.GetKey(KeyCode.LeftShift)||Input.GetKeyDown(KeyCode.LeftArrow)?0:-1,Input.GetKey(KeyCode.LeftShift)||Input.GetKeyDown(KeyCode.LeftArrow)?-1:1);
            if(first&&EventSystem.current!=null)EventSystem.current.SetSelectedGameObject(first.gameObject);
        }
        else if(select&&_focus>=0&&IsTopPanel()&&Input.GetKeyDown(KeyCode.Tab)) {
            var next=NextAliveButton(_focus,Input.GetKey(KeyCode.LeftShift)?-1:1);
            if(next&&EventSystem.current!=null)EventSystem.current.SetSelectedGameObject(next.gameObject);
        }
        int highlighted=_keyboardFocus?_focus:_hover;
        for(int i=0;i<7;i++) {
            bool focus=select&&highlighted==i&&_actorButtons[i].interactable;
            float v=focus?1:.43f;
            _portraits[i].color=Color.Lerp(_portraits[i].color,new Color(v,v,v,1),Time.unscaledDeltaTime*12);
            bool dead=!_actorButtons[i].interactable;
            _names[i].color=dead?new Color(.55f,.55f,.55f):focus?new Color(1f,.94f,.78f):new Color(.78f,.69f,.55f);
            _tendencies[i].color=dead?new Color(.6f,.6f,.6f):_names[i].color;
            if(_actorFocus!=null&&i<_actorFocus.Length&&_actorFocus[i]) {
                var color=_actorFocus[i].color;color.a=Mathf.Lerp(color.a,focus?1:0,Time.unscaledDeltaTime*12);_actorFocus[i].color=color;
            }
        }
        if(_cgReady&&IsTopPanel()&&(Input.GetMouseButtonDown(0)||Input.GetKeyDown(KeyCode.Return)||Input.GetKeyDown(KeyCode.Space)))StartCoroutine(FinishCG());
    }
    public override bool OnEscapePressed()
    {
        if(_transitioning)return true;
        if(Campaign.EndingStage=="ending_confirm")Campaign.CancelEndingCandidate();
        else if(Campaign.EndingStage!="ending_complete")UIManager.Instance.Open<SettingsPanel>();
        return true;
    }
    public override void OnClose(){StopAllCoroutines();_generation++;_cgReady=false;_hover=_focus=-1;_renderedStage=null;RestoreBusinessVisuals();base.OnClose();}
    public override void OnDestroy(){RestoreBusinessVisuals();base.OnDestroy();}
}
