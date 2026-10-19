using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;

/// <summary>沿用历史记录条目；按实际行数撑开，长句不覆盖下一条。</summary>
public class DialogueHistoryEntry : MonoBehaviour,IPointerEnterHandler,IPointerExitHandler
{
    [SerializeField] private Text _speakerNameText;
    [SerializeField] private Text _contentText;
    private Color normal;
    private bool styled;
    private void Awake(){ApplyStyle();}
    private void ApplyStyle()
    {
        if(styled||!_contentText||!_speakerNameText)return;
        normal=_contentText.color;_contentText.raycastTarget=true;_speakerNameText.raycastTarget=true;
        _speakerNameText.color=DialogueHistoryItem.NameColor(normal);_speakerNameText.fontStyle=FontStyle.Bold;styled=true;
    }
    public void OnPointerEnter(PointerEventData e){if(_contentText)_contentText.color=DialogueHistoryItem.HoverColor(normal);}
    public void OnPointerExit(PointerEventData e){if(_contentText)_contentText.color=normal;}
    private void OnDisable(){if(styled&&_contentText)_contentText.color=normal;}
    public void Bind(DialogueLine line)
    {
        if (line == null) return;
        ApplyStyle();_contentText.color=normal;
        _speakerNameText.text = string.IsNullOrEmpty(line.speakerName) ? "旁白" : line.speakerName;
        _contentText.text = NarrativeSystem.HidePrivateSuccessionProgress(line.content);
        var layout = GetComponent<LayoutElement>();
        if (layout != null) layout.preferredHeight = Mathf.Max(40, PreferredHeight(_contentText,840), PreferredHeight(_speakerNameText,140)) + 12;
    }
    private static float PreferredHeight(Text text, float width)
    {
        var settings = text.GetGenerationSettings(new Vector2(width,0));
        return text.cachedTextGeneratorForLayout.GetPreferredHeight(text.text,settings) / text.pixelsPerUnit;
    }
}
