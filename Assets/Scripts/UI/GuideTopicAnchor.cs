using UnityEngine;
using UnityEngine.UI;

/// <summary>在原面板上绑定说明入口和高亮区域；不移动原控件，不更改业务状态。</summary>
public sealed class GuideTopicAnchor : MonoBehaviour
{
    public string topic;
    public Button help;
    public RectTransform[] highlights;
    public bool automatic = true;
    public bool Allowed => !(Owner is SettingsPanel) && !(Owner is CGGalleryPanel);
    public bool CanAutoShow => Allowed && automatic && Topic != "player_type" && GuideHelpOverlay.Find(Topic)?.HasIntro==true &&
        (!(Owner is DescriptionPanel detail) || detail.IsRouteContext);
    private PanelBase owner;
    public PanelBase Owner => owner ? owner : owner = GetComponentInParent<PanelBase>(true);
    public string Topic => Owner is PlayerBuildPanel p ? p.CurrentGuideTopic : Owner is ShopPanel shop && shop.IsStorageMode ? "storage" : topic;
    public void BeginTutorial() { if (Owner is CharacterArchivePanel archive) archive.BeginGuide(); }
    public RectTransform TutorialTarget(int step)
    {
        if (Owner is CharacterArchivePanel archive) return archive.GuideTarget(step);
        return highlights != null && highlights.Length > 0 ? highlights[Mathf.Min(step, highlights.Length-1)] : transform as RectTransform;
    }
    public void EndTutorial() { if (Owner is CharacterArchivePanel archive) archive.EndGuide(); }
    private void Awake() { if (help) help.onClick.AddListener(() => GuideHelpOverlay.ShowHelp(this)); }
    private void OnEnable() { RefreshHelpVisibility();GuideHelpOverlay.Register(this); }
    private void Update()=>RefreshHelpVisibility();
    public void RefreshHelpVisibility(){if(help)help.gameObject.SetActive(Allowed&&GuideHelpOverlay.Find(Topic)?.HasHelp==true);}
    private void OnDisable() => GuideHelpOverlay.Unregister(this);
    public bool IsVisible
    {
        get {
            if (!Allowed || !isActiveAndEnabled || !gameObject.activeInHierarchy || Owner && !Owner.IsOpen) return false;
            if (Owner is DescriptionPanel detail && !detail.IsRouteContext) return false;
            foreach (var group in GetComponentsInParent<CanvasGroup>()) if (group.alpha < .05f) return false;
            return true; // 首次引导独立于问号，撤掉问号后仍可按保留步骤出现。
        }
    }
}
