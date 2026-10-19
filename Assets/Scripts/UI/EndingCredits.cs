using System.Collections;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

/// <summary>全部结局共用的片尾致谢，只负责显示，不修改金币、人物或结局判定。</summary>
public sealed class EndingCredits : MonoBehaviour
{
    private static EndingCredits current;
    public static bool IsShowing => current != null;
    public const float ScrollSpeed = 32f; // 1920×1080基准像素/秒，分辨率变化不改变阅读速度。
    public const float LastLineHold = 4f;
    public const float MinimumPlaySeconds = 55f;
    private RectTransform content, viewport;
    private CanvasGroup visibility;
    public float ScrollPosition => content.anchoredPosition.y;
    public bool LastLineVisible { get; private set; }
    public bool CanReturn { get; private set; }
    private Button returnButton;

    public static void Show()
    {
        if (current) return; // 连点结尾按钮只创建一次，不重复执行结算。
        if (EventSystem.current) EventSystem.current.SetSelectedGameObject(null);
        var root = new GameObject("EndingCredits", typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster), typeof(CanvasGroup));
        root.GetComponent<Canvas>().renderMode = RenderMode.ScreenSpaceOverlay;
        root.GetComponent<Canvas>().sortingOrder = 32767;
        var scaler = root.GetComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920,1080); scaler.matchWidthOrHeight = .5f;
        current = root.AddComponent<EndingCredits>();
        AudioManager.Instance?.SetContext("credits");
        current.visibility = root.GetComponent<CanvasGroup>(); current.visibility.alpha=0;
        var backdrop = new GameObject("Backdrop",typeof(RectTransform),typeof(Image));
        backdrop.transform.SetParent(root.transform,false); Stretch((RectTransform)backdrop.transform);
        backdrop.GetComponent<Image>().color = new Color(.018f,.016f,.022f,1);
        // 整个黑幕拦截指针；UIManager也拦截键盘，片尾期间不会操作到结局下层。
        backdrop.GetComponent<Image>().raycastTarget = true;
        var clip = new GameObject("Viewport",typeof(RectTransform),typeof(RectMask2D));
        clip.transform.SetParent(root.transform,false); current.viewport=(RectTransform)clip.transform;
        Stretch(current.viewport);current.viewport.offsetMin=new Vector2(180,90);current.viewport.offsetMax=new Vector2(-180,-90);
        var textRoot=new GameObject("CreditsText",typeof(RectTransform),typeof(Text));textRoot.transform.SetParent(clip.transform,false);
        current.content=(RectTransform)textRoot.transform;current.content.anchorMin=current.content.anchorMax=new Vector2(.5f,0);
        current.content.pivot=new Vector2(.5f,1);current.content.sizeDelta=new Vector2(1420,100);
        current.content.anchoredPosition=new Vector2(0,-55);
        var text=textRoot.GetComponent<Text>();text.font=Resources.Load<Font>("Art/Fonts/FangZhengShuSong");
        text.fontSize=40;text.lineSpacing=1.65f;text.alignment=TextAnchor.UpperCenter;
        text.color=new Color(.91f,.85f,.73f);text.raycastTarget=false;text.supportRichText=false;
        text.horizontalOverflow=HorizontalWrapMode.Wrap;text.verticalOverflow=VerticalWrapMode.Overflow;
        text.text=Resources.Load<TextAsset>("Narrative/endingCredits").text.TrimEnd('\r','\n');
        Canvas.ForceUpdateCanvases();current.content.sizeDelta=new Vector2(1420,text.preferredHeight+16);
        var buttonRoot=new GameObject("ReturnToTitle",typeof(RectTransform),typeof(Image),typeof(Button));
        buttonRoot.transform.SetParent(root.transform,false);var buttonRect=(RectTransform)buttonRoot.transform;
        buttonRect.anchorMin=buttonRect.anchorMax=new Vector2(.5f,0);buttonRect.sizeDelta=new Vector2(360,76);buttonRect.anchoredPosition=new Vector2(0,48);
        buttonRoot.GetComponent<Image>().color=new Color(.32f,.27f,.19f,.25f);
        current.returnButton=buttonRoot.GetComponent<Button>();current.returnButton.onClick.AddListener(current.ReturnToTitle);
        var label=new GameObject("Label",typeof(RectTransform),typeof(Text));label.transform.SetParent(buttonRoot.transform,false);Stretch((RectTransform)label.transform);
        var labelText=label.GetComponent<Text>();labelText.font=text.font;labelText.fontSize=34;labelText.alignment=TextAnchor.MiddleCenter;
        labelText.color=text.color;labelText.text="返回主菜单";labelText.raycastTarget=false;
        buttonRoot.SetActive(false);
        current.StartCoroutine(current.Play());
    }
    private IEnumerator Play()
    {
        float started=Time.unscaledTime;
        for(float t=0;t<.7f;t+=Time.unscaledDeltaTime){visibility.alpha=Mathf.SmoothStep(0,1,t/.7f);yield return null;}
        visibility.alpha=1;
        // 最后一行停在画面中部，避免尚未读完就从顶部被裁掉。
        float destination=content.rect.height+viewport.rect.height*.5f;
        while(content.anchoredPosition.y<destination)
        {content.anchoredPosition=new Vector2(0,Mathf.Min(destination,content.anchoredPosition.y+ScrollSpeed*Time.unscaledDeltaTime));yield return null;}
        LastLineVisible=true;
        float unlock=Mathf.Max(started+MinimumPlaySeconds,Time.unscaledTime+LastLineHold);
        while(Time.unscaledTime<unlock)yield return null;
        CanReturn=true;returnButton.gameObject.SetActive(true);
        if(EventSystem.current)EventSystem.current.SetSelectedGameObject(returnButton.gameObject);
    }
    private void ReturnToTitle()
    {
        if(!CanReturn)return;
        CanReturn=false;returnButton.interactable=false;StartCoroutine(Finish());
    }
    private IEnumerator Finish()
    {
        // 先淡去文字，黑底持续遮住旧结局，直到标题场景加载完成。
        var text=content.GetComponent<Text>();Color color=text.color;
        for(float t=0;t<.7f;t+=Time.unscaledDeltaTime){color.a=1-Mathf.Clamp01(t/.7f);text.color=color;yield return null;}
        SceneManager.LoadScene("StartGame");
    }
    private static void Stretch(RectTransform r){r.anchorMin=Vector2.zero;r.anchorMax=Vector2.one;r.offsetMin=r.offsetMax=Vector2.zero;}
    private void OnDestroy(){if(current==this)current=null;}
}
