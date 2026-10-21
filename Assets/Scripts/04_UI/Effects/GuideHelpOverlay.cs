using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

/// <summary>说明7：独立首见引导及可折叠机制说明。只记录阅读进度，不写剧情、金币或检定。</summary>
public sealed class GuideHelpOverlay : MonoBehaviour, IPointerClickHandler
{
    [Serializable] public sealed class Entry {
        public string id,title,introRevision;
        public string[] main,details,intro,sources,mainIds,detailsIds,introIds;
        public int[] introTargets;
        public bool HasHelp => (main?.Length??0)>0 || (details?.Length??0)>0;
        public bool HasIntro => (intro?.Length??0)>0;
        public int TargetStep(int index) => introTargets!=null&&index<introTargets.Length?introTargets[index]:index;
    }
    [Serializable] public sealed class SharedCopy
    {
        public string step="{current} / {total}　点击继续",skip="略过本项",close="返回",replay="重新查看引导",detailsOpen="详细信息  ＋",detailsClose="收起详细信息  −";
    }
    [Serializable] private sealed class Catalog { public Entry[] entries;public SharedCopy common; }
    [SerializeField] private RectTransform _root, _tutorialCopy, _helpCard;
    [SerializeField] private Image[] _dimmers, _outlines;
    [SerializeField] private Image _helpDim;
    [SerializeField] private Button _advance, _skip, _close, _detailsButton, _replay;
    [SerializeField] private Text _tutorialTitle, _tutorialText, _stepText, _helpTitle, _mainText, _detailText, _detailsLabel;
    [SerializeField] private ScrollRect _detailsScroll;
    [SerializeField] private ScrollRect _tutorialScroll;
    [SerializeField] private CanvasGroup _tutorialGroup, _helpGroup;
    private static GuideHelpOverlay current;
    private static readonly List<GuideTopicAnchor> anchors = new List<GuideTopicAnchor>();
    private static Dictionary<string,Entry> catalog;
    private static SharedCopy shared=new SharedCopy();
    private static int inputConsumedFrame = -10;
    public static bool SuppressAutomatic;
    public static bool IsShowing => current && current._source;
    public static bool BlocksInput => IsShowing || Time.frameCount <= inputConsumedFrame + 1;
    public static string ActiveTopic => IsShowing ? current._entry.id : "";
    public static bool IsTutorial => IsShowing && !current._helpMode;
    // 只重放本轮重写的档案引导，其余阅读记录继续沿用。
    private static string SeenKey(string topic) {
        var entry=Find(topic);
        return !string.IsNullOrEmpty(entry?.introRevision)?"MD.Guide.copy."+topic+"."+entry.introRevision:"MD.Guide."+(topic=="archive"?"v2.":"v1.")+topic;
    }
    public static bool HasSeen(string topic) => PlayerPrefs.GetInt(SeenKey(topic), 0) == 1;
    public static Entry Find(string topic)
    {
        if (catalog == null) {
            var asset = Resources.Load<TextAsset>("Narrative/mechanismHelp");
            var data=asset?JsonUtility.FromJson<Catalog>(asset.text):null;
            catalog = data!=null ? data.entries.ToDictionary(e=>e.id) : new Dictionary<string,Entry>();
            shared=data?.common??new SharedCopy();
        }
        catalog.TryGetValue(topic, out var entry); return entry;
    }
    public static void Register(GuideTopicAnchor anchor)
    {
        if (!anchors.Contains(anchor)) anchors.Add(anchor);
        Ensure();
    }
    public static void Unregister(GuideTopicAnchor anchor)
    {
        anchors.Remove(anchor);
        if (current && current._source == anchor) current.Close(false);
    }
    private static GuideHelpOverlay Ensure()
    {
        if (current) return current;
        var prefab=Resources.Load<GuideHelpOverlay>("Panel/GuideHelpOverlay");
        if (prefab) current=Instantiate(prefab);
        return current;
    }
    public static void ShowHelp(GuideTopicAnchor source)
    {
        if (!source || !source.IsVisible || Find(source.Topic)?.HasHelp!=true) return;
        var panel=Ensure(); if(panel)panel.Open(source,true);
    }
    public static bool HandleEscape()
    {
        if (!IsShowing) return BlocksInput;
        current.Close(!current._helpMode); return true;
    }
    private GuideTopicAnchor _source;
    private Entry _entry;
    private GameObject _oldFocus;
    private bool _helpMode, _expanded;
    private int _step;
    private float _opened, _nextScan;
    private readonly Vector3[] corners=new Vector3[4];
    private void Awake()
    {
        current=this;
        _advance.onClick.AddListener(Advance);
        _skip.onClick.AddListener(()=>Close(true));
        _close.onClick.AddListener(()=>Close(false));
        _detailsButton.onClick.AddListener(()=>{_expanded=!_expanded;RenderHelp();});
        _replay.onClick.AddListener(()=>{if(!_entry.HasIntro)return;_helpMode=false;_step=0;_opened=Time.unscaledTime;_source.BeginTutorial();Render();});
        Hide();
    }
    private void OnDestroy() { if(current==this)current=null; anchors.RemoveAll(a=>!a); }
    private void Update()
    {
        if (_source) {
            if (!_source.IsVisible) { Close(false); return; }
            if (Input.GetKeyDown(KeyCode.Escape)) { Close(!_helpMode); return; }
            // 最后一步会关闭来源；同一帧不再刷新已关闭的高亮。
            if (!_helpMode && Time.unscaledTime-_opened>.2f && (Input.GetKeyDown(KeyCode.Return)||Input.GetKeyDown(KeyCode.Space))) { Advance(); return; }
            if (!_helpMode) { _tutorialGroup.alpha=Mathf.Clamp01((Time.unscaledTime-_opened)/.18f); PositionSpotlight(); }
            return;
        }
        if(SuppressAutomatic || Time.unscaledTime<_nextScan || PresentationEffects.IsBusy || SaveSlotsPanel.IsShowing)return;
        _nextScan=Time.unscaledTime+.3f;
        if(BackpackBoard.Instance?.IsEditing==true)return;
        // 仅介绍当前真正可见的面板。已看过的上层窗口不会使下层引导突然弹出。
        var ui=UIManager.Instance;
        var top=ui ? ui.OpenStack.LastOrDefault(p=>p&&p.IsOpen&&p.gameObject.activeInHierarchy&&!(p is HUDPanel)) : null;
        if(CGGalleryPanel.IsShowing)top=FindObjectOfType<CGGalleryPanel>();
        if(!top&&!ui)top=FindObjectsOfType<SettingsPanel>().FirstOrDefault(p=>p.IsOpen&&p.gameObject.activeInHierarchy);
        if(!top)return;
        var candidate=anchors.LastOrDefault(a=>a&&a.CanAutoShow&&a.IsVisible&&a.Owner==top);
        if(!candidate)return;
        if(candidate.Owner is PreparationPanel && SystemManager.Instance.Narrative?.IsPreparing!=true)return;
        string topic=candidate.Topic;
        if(HasSeen(topic)||Find(topic)==null)return;
        Open(candidate,false);
    }
    private void Open(GuideTopicAnchor source,bool help)
    {
        var entry=Find(source.Topic);if(entry==null||(help?!entry.HasHelp:!entry.HasIntro))return;
        if(!_source)_oldFocus=EventSystem.current?EventSystem.current.currentSelectedGameObject:null;
        _source=source;_entry=entry;_helpMode=help;_step=0;_expanded=false;_opened=Time.unscaledTime;
        if(!help)_source.BeginTutorial();
        if(EventSystem.current)EventSystem.current.SetSelectedGameObject(null);
        Render();
    }
    private void Render()
    {
        // 公共按钮文字也来自同一文案配置，Word回填无需改脚本或猜测预制体字段。
        SetLabel(_skip,shared.skip);SetLabel(_close,shared.close);SetLabel(_replay,shared.replay);
        _helpCard.gameObject.SetActive(_helpMode);_helpDim.gameObject.SetActive(_helpMode);
        _advance.gameObject.SetActive(!_helpMode);_tutorialCopy.gameObject.SetActive(!_helpMode);
        foreach(var image in _dimmers)image.gameObject.SetActive(!_helpMode);
        foreach(var image in _outlines)image.gameObject.SetActive(!_helpMode);
        _tutorialGroup.blocksRaycasts=!_helpMode;_helpGroup.blocksRaycasts=_helpMode;
        if(_helpMode) { RenderHelp(); if(EventSystem.current)EventSystem.current.SetSelectedGameObject(_detailsButton.gameObject.activeSelf?_detailsButton.gameObject:_close.gameObject); }
        else {
            _tutorialTitle.text=_entry.title;
            var intro=_entry.intro; // 删除首次教程后绝不用问号正文顶替。
            _tutorialText.text=intro[Mathf.Min(_step,intro.Length-1)];
            _stepText.text=shared.step.Replace("{current}",(_step+1).ToString()).Replace("{total}",intro.Length.ToString());
            PositionSpotlight();
            if(_tutorialScroll){_tutorialScroll.StopMovement();_tutorialScroll.verticalNormalizedPosition=1;}
        }
    }
    public void OnPointerClick(PointerEventData data){if(IsTutorial&&!data.dragging)Advance();}
    private void Advance()
    {
        if(!_source||_helpMode||Time.unscaledTime-_opened<.2f)return;
        int count=_entry.intro?.Length??0;
        if(++_step>=count) { Close(true);return; }
        _opened=Time.unscaledTime;Render();
    }
    private void RenderHelp()
    {
        bool title=!string.IsNullOrWhiteSpace(_entry.title),details=(_entry.details?.Length??0)>0,replay=_entry.HasIntro;
        _helpTitle.text=_entry.title;_helpTitle.gameObject.SetActive(title);
        _mainText.text=string.Join("\n",_entry.main??Array.Empty<string>());
        _mainText.gameObject.SetActive((_entry.main?.Length??0)>0);
        _detailText.text=string.Join("\n\n",_entry.details??Array.Empty<string>());
        _detailsLabel.text=_expanded?shared.detailsClose:shared.detailsOpen;
        _detailsButton.gameObject.SetActive(details);_replay.gameObject.SetActive(replay);
        _expanded&=details;_detailsScroll.gameObject.SetActive(_expanded);
        float mainHeight=_mainText.gameObject.activeSelf?Mathf.Max(36,_mainText.preferredHeight+12):0;
        float height=30+(title?58:0)+mainHeight+(details?62:12)+(_expanded?260:0)+92;
        height=Mathf.Clamp(height,260,_root.rect.height-70);_helpCard.sizeDelta=new Vector2(1100,height);
        float top=height*.5f-28;
        if(title){Box(_helpTitle.rectTransform,0,top-26,990,52);top-=58;}
        Box(_mainText.rectTransform,0,top-mainHeight*.5f,980,mainHeight);top-=mainHeight;
        if(details){Move((RectTransform)_detailsButton.transform,0,top-31);top-=62;}
        if(_expanded){Box(_detailsScroll.transform as RectTransform,0,top-120,980,240);_detailText.rectTransform.sizeDelta=new Vector2(0,Mathf.Max(240,_detailText.preferredHeight+12));_detailsScroll.verticalNormalizedPosition=1;}
        Move((RectTransform)_close.transform,replay?235:0,-height*.5f+48);
        Move((RectTransform)_replay.transform,-235,-height*.5f+48);
    }
    private static void Box(RectTransform t,float x,float y,float w,float h){t.anchorMin=t.anchorMax=t.pivot=new Vector2(.5f,.5f);t.anchoredPosition=new Vector2(x,y);t.sizeDelta=new Vector2(w,h);}
    private static void SetLabel(Button button,string value){var label=button?button.GetComponentInChildren<Text>(true):null;if(label)label.text=value;}
    private static void Move(RectTransform t,float x,float y) { t.anchoredPosition=new Vector2(x,y); }
    private void PositionSpotlight()
    {
        var target=_source.TutorialTarget(_entry.TargetStep(_step));
        // 旧档可以停在记录页；隐藏的七维不能成为高亮洞，也不能为了引导切换页签。
        if(!target || !target.gameObject.activeInHierarchy)
            target=_source.help?_source.help.transform as RectTransform:_source.transform as RectTransform;
        if(!target)return;
        target.GetWorldCorners(corners);
        Canvas canvas=target.GetComponentInParent<Canvas>();Camera camera=canvas&&canvas.renderMode!=RenderMode.ScreenSpaceOverlay?canvas.worldCamera:null;
        Vector2 min=Vector2.positiveInfinity,max=Vector2.negativeInfinity;
        foreach(var corner in corners) {
            var screen=RectTransformUtility.WorldToScreenPoint(camera,corner);
            RectTransformUtility.ScreenPointToLocalPointInRectangle(_root,screen,null,out var local);
            min=Vector2.Min(min,local);max=Vector2.Max(max,local);
        }
        var bounds=_root.rect; min=Vector2.Max(min-Vector2.one*10,bounds.min+Vector2.one*12);max=Vector2.Min(max+Vector2.one*10,bounds.max-Vector2.one*12);
        Rect hole=Rect.MinMaxRect(min.x,min.y,max.x,max.y);
        SetRect(_dimmers[0].rectTransform,Rect.MinMaxRect(bounds.xMin,bounds.yMin,min.x,bounds.yMax));
        SetRect(_dimmers[1].rectTransform,Rect.MinMaxRect(max.x,bounds.yMin,bounds.xMax,bounds.yMax));
        SetRect(_dimmers[2].rectTransform,Rect.MinMaxRect(min.x,bounds.yMin,max.x,min.y));
        SetRect(_dimmers[3].rectTransform,Rect.MinMaxRect(min.x,max.y,max.x,bounds.yMax));
        SetRect(_outlines[0].rectTransform,new Rect(min.x,min.y,hole.width,2));
        SetRect(_outlines[1].rectTransform,new Rect(min.x,max.y-2,hole.width,2));
        SetRect(_outlines[2].rectTransform,new Rect(min.x,min.y,2,hole.height));
        SetRect(_outlines[3].rectTransform,new Rect(max.x-2,min.y,2,hole.height));
        // 介绍文字避开高亮区；不复制棋盘，也不提高业务对象的Canvas层级。
        float left=min.x-bounds.xMin,right=bounds.xMax-max.x;
        // 中央棋盘两侧近似等宽时用左侧留白，避开右侧原有按钮文字。
        bool onRight=right>left+60;float free=onRight?right:left;
        float width=Mathf.Clamp(free-66,340,740);
        float x=onRight?(max.x+bounds.xMax)*.5f:(bounds.xMin+min.x)*.5f;float y=0;
        bool vertical=free<400;
        if(vertical) {
            // 横向列表用上下留白。将文字和推进入口一起收进空区，不压住高亮图片。
            width=Mathf.Min(1100,bounds.width-90);x=0;
            float above=bounds.yMax-max.y,below=min.y-bounds.yMin;
            y=above>below+60?(max.y+bounds.yMax)*.5f:(bounds.yMin+min.y)*.5f;
            y=Mathf.Clamp(y,bounds.yMin+122,bounds.yMax-122);
        }
        // 长教程沿用同一步正文，以滚动承载，不拆句、不缩小字号，也不新增文字。
        bool hasTitle=!string.IsNullOrWhiteSpace(_entry.title);_tutorialTitle.gameObject.SetActive(hasTitle);
        _tutorialText.rectTransform.SetSizeWithCurrentAnchors(RectTransform.Axis.Horizontal,width);
        float preferred=_tutorialText.preferredHeight+12;
        float available=vertical?Mathf.Max(230,Mathf.Max(bounds.yMax-max.y,min.y-bounds.yMin)-24):bounds.height-100;
        float copyHeight=Mathf.Min(available,Mathf.Max(360,preferred+(hasTitle?178:118)));
        float bodyHeight=Mathf.Max(72,copyHeight-(hasTitle?164:104));
        _tutorialCopy.sizeDelta=new Vector2(width,copyHeight);
        Box(_tutorialTitle.rectTransform,0,copyHeight*.5f-30,width,54);
        float bodyTop=copyHeight*.5f-(hasTitle?74:12);
        if(_tutorialScroll){
            Box(_tutorialScroll.transform as RectTransform,0,bodyTop-bodyHeight*.5f,width,bodyHeight);
            var textRect=_tutorialText.rectTransform;textRect.anchorMin=textRect.anchorMax=new Vector2(.5f,1);textRect.pivot=new Vector2(.5f,1);textRect.sizeDelta=new Vector2(width,Mathf.Max(bodyHeight,preferred));
        }
        Box(_stepText.rectTransform,0,-copyHeight*.5f+62,width,34);
        Move((RectTransform)_skip.transform,0,-copyHeight*.5f+20);
        if(vertical)y=Mathf.Clamp(y,bounds.yMin+copyHeight*.5f+10,bounds.yMax-copyHeight*.5f-10);
        _tutorialCopy.anchoredPosition=new Vector2(x,y);

    }
    private static void SetRect(RectTransform t,Rect r) { t.anchorMin=t.anchorMax=t.pivot=new Vector2(.5f,.5f);t.anchoredPosition=r.center;t.sizeDelta=r.size; }
    private void Close(bool markSeen)
    {
        if(!_source)return;
        if(markSeen) { PlayerPrefs.SetInt(SeenKey(_entry.id),1);PlayerPrefs.Save(); }
        _source.EndTutorial();
        _source=null;Hide();inputConsumedFrame=Time.frameCount;_nextScan=Time.unscaledTime+.5f;
        UIManager.Instance?.ConsumeModalCloseInput();
        if(EventSystem.current&&_oldFocus&&_oldFocus.activeInHierarchy)EventSystem.current.SetSelectedGameObject(_oldFocus);
        _oldFocus=null;
    }
    private void Hide()
    {
        _helpCard.gameObject.SetActive(false);_helpDim.gameObject.SetActive(false);_advance.gameObject.SetActive(false);_tutorialCopy.gameObject.SetActive(false);
        foreach(var image in _dimmers)image.gameObject.SetActive(false);
        foreach(var image in _outlines)image.gameObject.SetActive(false);
        _tutorialGroup.blocksRaycasts=_helpGroup.blocksRaycasts=false;
    }
}
