using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;

/// <summary>只对显示层区分姓名与正文；历史模型仍保留原句。</summary>
public class DialogueHistoryItem : MonoBehaviour,IPointerEnterHandler,IPointerExitHandler
{
    public Text content;
    private Color normal;
    private bool styled;
    private void Awake(){EnsureStyle();}
    private void EnsureStyle()
    {
        if(content==null)content=GetComponent<Text>();
        if(!content||styled)return;
        normal=content.color;content.supportRichText=true;content.raycastTarget=true;styled=true;
    }
    public static Color NameColor(Color body)
    {return (body.r+body.g+body.b)/3f<.5f?new Color(.44f,.27f,.09f,1):new Color(.97f,.75f,.39f,1);}
    public static Color HoverColor(Color body)
    {return (body.r+body.g+body.b)/3f<.5f?new Color(.43f,.30f,.15f,1):new Color(1,.94f,.77f,1);}
    public void SetData(DialogueLine line)
    {
        string name=line.speakerName??"";
        SetDisplayText(string.IsNullOrEmpty(name)?line.content:name+": "+line.content,name.Length);
    }
    public void SetDisplayText(string raw,int nameLength=0)
    {
        EnsureStyle();if(!content)return;
        raw=NarrativeSystem.HidePrivateSuccessionProgress(raw);nameLength=Mathf.Clamp(nameLength,0,raw.Length);
        content.color=normal;
        content.text=nameLength==0?raw:"<b><color=#"+ColorUtility.ToHtmlStringRGB(NameColor(normal))+">"+raw.Substring(0,nameLength)+"</color></b>"+raw.Substring(nameLength);
    }
    public void OnPointerEnter(PointerEventData e){if(content)content.color=HoverColor(normal);}
    public void OnPointerExit(PointerEventData e){if(content)content.color=normal;}
    private void OnDisable(){if(styled&&content)content.color=normal;}
}
