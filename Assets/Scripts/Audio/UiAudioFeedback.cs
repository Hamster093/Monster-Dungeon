using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;

/// <summary>控件声明声音用途，不从显示文字猜测。业务结果音由业务提交点负责。</summary>
public sealed class UiAudioFeedback : MonoBehaviour,IPointerClickHandler,IPointerDownHandler,ISubmitHandler
{
    public string cue="click";
    public void OnPointerClick(PointerEventData data){if(data.button==PointerEventData.InputButton.Left)Play();}
    public void OnPointerDown(PointerEventData data){var s=GetComponent<Selectable>();if(data.button==PointerEventData.InputButton.Left&&s&&!s.IsInteractable())AudioManager.Instance?.PlayCue("denied");}
    public void OnSubmit(BaseEventData data)=>Play();
    void Play(){var s=GetComponent<Selectable>();if(!s||!s.IsInteractable())return;if(!string.IsNullOrEmpty(cue))AudioManager.Instance?.PlayCue(cue);}
}
