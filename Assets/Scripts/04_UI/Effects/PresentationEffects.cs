using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.SceneManagement;
using UnityEngine.EventSystems;

/// <summary>只承载短转场与报复反馈，不推进剧情、不修改存档或随机数。</summary>
public sealed class PresentationEffects : MonoBehaviour
{
    private sealed class Cue { public string key,label; public bool revenge,phase,visitor,opening; public Action covered; public Func<bool> ready; }
    private static PresentationEffects instance;
    private static int releaseFrame=-10;
    private readonly Queue<Cue> queue=new Queue<Cue>();
    private readonly HashSet<string> accepted=new HashSet<string>();
    private Image fade;
    private Text caption;
    private CanvasGroup edges;
    private RectTransform shaking;
    private Vector2 shakeOrigin;
    private bool driving,openingHeld,openingPersistent;
    public static bool IsBusy => (instance&&(instance.openingHeld||instance.driving||instance.queue.Count>0))||Time.frameCount<=releaseFrame+1;
    public static string CurrentCue { get; private set; }
    public static event Action<string> Started,Finished;

    public static void PlayDay(int day,int phase,bool phaseChanged,Action covered=null,Func<bool> ready=null)
    { Enqueue(new Cue{key="day:"+day,label=phaseChanged?"第"+phase+"阶段 · 第"+day+"天":"第"+day+"天",phase=phaseChanged,covered=covered,ready=ready}); }
    public static void PlayInterlude(string stableId)
    { Enqueue(new Cue{key="interlude:"+stableId,label=""}); }
    public static void PlayRevenge(string stableId)
    { Enqueue(new Cue{key="revenge:"+stableId,revenge=true}); }
    /// <summary>新局换场前先盖住画面，直到类型选择已经打开；不改经营数据。</summary>
    public static void PrepareOpening()
    {
        if(!Application.isPlaying)return;
        // 遮罩也应截断已选中按钮的Submit，旧选项不在转场后自动恢复。
        if(EventSystem.current)EventSystem.current.SetSelectedGameObject(null);
        if(!instance)Create();
        if(instance.openingHeld)return;
        instance.StopAllCoroutines();instance.queue.Clear();instance.accepted.Clear();instance.RestorePortrait();
        instance.driving=false;instance.openingHeld=true;instance.openingPersistent=true;
        instance.gameObject.SetActive(true);instance.fade.color=Color.black;instance.edges.alpha=0;instance.caption.text="";
        DontDestroyOnLoad(instance.gameObject);
    }
    /// <summary>类型页面就绪后只揭开现有黑幕，不再先显示整备棋盘。</summary>
    public static void RevealOpening()
    {
        if(!instance||!instance.openingHeld)return;
        instance.openingHeld=false;
        Enqueue(new Cue{key="opening",label="",opening=true});
    }
    public static void PlayVisitor(string stableId,Action whenCovered,Func<bool> ready)
    { Enqueue(new Cue{key="visitor:"+stableId,label="",visitor=true,covered=whenCovered,ready=ready}); }
    public static void CancelAll()
    {
        if(!instance)return;
        instance.StopAllCoroutines();instance.queue.Clear();instance.driving=false;instance.openingHeld=false;instance.RestorePortrait();
        instance.fade.color=Color.clear;instance.edges.alpha=0;instance.caption.text="";
        CurrentCue=null;releaseFrame=Time.frameCount;instance.ReturnToScene();instance.gameObject.SetActive(false);
    }
    private static void Enqueue(Cue cue)
    {
        if(!Application.isPlaying||EndingPresentationPanel.IsShowing)return;
        if(!instance)Create();
        if(!instance.accepted.Add(cue.key))return;
        instance.queue.Enqueue(cue);instance.gameObject.SetActive(true);
        if(!instance.driving){instance.driving=true;instance.StartCoroutine(instance.PlayQueue());}
    }
    private static void Create()
    {
        var go=new GameObject("PresentationEffects",typeof(RectTransform),typeof(Canvas),typeof(CanvasScaler),typeof(GraphicRaycaster));
        var canvas=go.GetComponent<Canvas>();canvas.renderMode=RenderMode.ScreenSpaceOverlay;canvas.sortingOrder=32760;
        var scaler=go.GetComponent<CanvasScaler>();scaler.uiScaleMode=CanvasScaler.ScaleMode.ScaleWithScreenSize;scaler.referenceResolution=new Vector2(1920,1080);scaler.screenMatchMode=CanvasScaler.ScreenMatchMode.Expand;
        instance=go.AddComponent<PresentationEffects>();
        instance.fade=Image(go.transform,"InputAndFade",Color.clear);Stretch(instance.fade.rectTransform,Vector2.zero,Vector2.one,Vector2.zero,Vector2.zero);instance.fade.raycastTarget=true;
        var edgeRoot=new GameObject("RevengeEdges",typeof(RectTransform),typeof(CanvasGroup));edgeRoot.transform.SetParent(go.transform,false);
        Stretch((RectTransform)edgeRoot.transform,Vector2.zero,Vector2.one,Vector2.zero,Vector2.zero);instance.edges=edgeRoot.GetComponent<CanvasGroup>();instance.edges.alpha=0;instance.edges.blocksRaycasts=false;
        for(int band=2;band>=0;band--) {
            float width=new[]{14f,32f,58f}[band],alpha=new[]{.22f,.12f,.055f}[band];
            var color=new Color(.42f,.018f,.012f,alpha);
            var left=Image(edgeRoot.transform,"Left"+band,color);Stretch(left.rectTransform,Vector2.zero,new Vector2(0,1),Vector2.zero,new Vector2(width,0));
            var right=Image(edgeRoot.transform,"Right"+band,color);Stretch(right.rectTransform,new Vector2(1,0),Vector2.one,new Vector2(-width,0),Vector2.zero);
            var top=Image(edgeRoot.transform,"Top"+band,color);Stretch(top.rectTransform,new Vector2(0,1),Vector2.one,new Vector2(0,-width),Vector2.zero);
            var bottom=Image(edgeRoot.transform,"Bottom"+band,color);Stretch(bottom.rectTransform,Vector2.zero,new Vector2(1,0),Vector2.zero,new Vector2(0,width));
        }
        var textGo=new GameObject("TransitionCaption",typeof(RectTransform),typeof(Text));textGo.transform.SetParent(go.transform,false);
        instance.caption=textGo.GetComponent<Text>();instance.caption.font=Resources.Load<Font>("Art/Fonts/FangZhengShuSong");instance.caption.fontSize=42;instance.caption.alignment=TextAnchor.MiddleCenter;instance.caption.color=new Color(1,.89f,.7f,0);instance.caption.raycastTarget=false;
        var rect=instance.caption.rectTransform;rect.anchorMin=rect.anchorMax=rect.pivot=new Vector2(.5f,.5f);rect.anchoredPosition=Vector2.zero;rect.sizeDelta=new Vector2(1400,150);
    }
    private IEnumerator PlayQueue()
    {
        while(queue.Count>0) {
            // 引导自带推进与关闭入口；等待它结束，不抢走它的 ESC 或点击。
            while(GuideHelpOverlay.IsShowing)yield return null;
            var cue=queue.Dequeue();
            if(EndingPresentationPanel.IsShowing)continue; // 终局仍由原CG演出独立管理。
            if(EventSystem.current)EventSystem.current.SetSelectedGameObject(null);
            CurrentCue=cue.key;Started?.Invoke(cue.key);
            if(cue.revenge)yield return Revenge();else yield return Transition(cue);
            Finished?.Invoke(cue.key);CurrentCue=null;
        }
        RestorePortrait();fade.color=Color.clear;edges.alpha=0;caption.text="";
        releaseFrame=Time.frameCount;driving=false;ReturnToScene();gameObject.SetActive(false);
    }
    private IEnumerator Transition(Cue cue)
    {
        caption.text=cue.label;
        if(cue.opening){yield return Fade(1,0,.55f);yield break;}
        float darkness=1f; // 跨天也必须全黑，不能透出上一位来访者。
        yield return Fade(0,darkness,cue.visitor ? .20f : .25f);
        // 人物在黑幕下替换；图片完成加载后再淡亮，避免新旧人物直接跳切。
        cue.covered?.Invoke();
        while(cue.ready!=null&&!cue.ready())yield return null;
        yield return new WaitForSecondsRealtime(cue.visitor ? .10f : .15f);
        yield return Fade(darkness,0,cue.visitor ? .30f : .35f);
    }
    private IEnumerator Fade(float from,float to,float seconds)
    {
        for(float t=0;t<seconds;t+=Time.unscaledDeltaTime) {
            float value=Mathf.Lerp(from,to,Mathf.SmoothStep(0,1,t/seconds));fade.color=new Color(0,0,0,value);
            caption.color=new Color(1,.89f,.7f,value);yield return null;
        }
        fade.color=new Color(0,0,0,to);caption.color=new Color(1,.89f,.7f,to);
    }
    private IEnumerator Revenge()
    {
        caption.text="";fade.color=Color.clear;
        var talk=UIManager.Instance?.Get<TalkPanel>();
        if(talk)foreach(var rect in talk.GetComponentsInChildren<RectTransform>(true))if(rect.name=="Img_CharacterPortrait"){shaking=rect;shakeOrigin=rect.anchoredPosition;break;}
        const float duration=.46f;
        for(float t=0;t<duration;t+=Time.unscaledDeltaTime) {
            float p=Mathf.Clamp01(t/duration);edges.alpha=Mathf.Sin(p*Mathf.PI);
            // 只晃中央立绘，正文、选项、档案与顶部数值保持原位。
            if(shaking)shaking.anchoredPosition=shakeOrigin+new Vector2(Mathf.Sin(p*Mathf.PI*4)*4f*(1-p),0);
            yield return null;
        }
        edges.alpha=0;RestorePortrait();
    }
    private void ReturnToScene()
    {
        if(!openingPersistent)return;
        openingPersistent=false;
        var scene=SceneManager.GetActiveScene();
        if(scene.IsValid()&&scene.isLoaded)SceneManager.MoveGameObjectToScene(gameObject,scene);
    }
    private void RestorePortrait(){if(shaking)shaking.anchoredPosition=shakeOrigin;shaking=null;}
    private void OnDestroy(){RestorePortrait();if(instance==this){instance=null;CurrentCue=null;releaseFrame=Time.frameCount;}}
    private static Image Image(Transform parent,string name,Color color)
    {var go=new GameObject(name,typeof(RectTransform),typeof(Image));go.transform.SetParent(parent,false);var image=go.GetComponent<Image>();image.color=color;image.raycastTarget=false;return image;}
    private static void Stretch(RectTransform rect,Vector2 min,Vector2 max,Vector2 offsetMin,Vector2 offsetMax)
    {rect.anchorMin=min;rect.anchorMax=max;rect.offsetMin=offsetMin;rect.offsetMax=offsetMax;}
}
