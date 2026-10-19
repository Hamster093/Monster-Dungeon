/****************************************************
    文件：DialogueOptionItem.cs
	作者：DADI
    邮箱: 1581507659@qq.com
    日期：2026/10/9 15:44:04
	功能：文本按钮预制体
*****************************************************/

using Ink.Runtime;
using System;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;

public class DialogueOptionItem : MonoBehaviour,IPointerEnterHandler,IPointerExitHandler,ISelectHandler,IDeselectHandler
{
    [SerializeField] private Button _button;
    [SerializeField] private Text _text;

    private Action _onClick;
    private Color _normalColor;
    private bool _hasNormalColor,_hover,_focused;
    private Image _backdrop;
    private Color _normalBackdrop;

    public void SetData(string content, Action onClick)
    {
        if (_text != null) _text.text = content;
        if(!_hasNormalColor&&_text){_normalColor=_text.color;_hasNormalColor=true;}
        if(!_backdrop&&_button){_backdrop=_button.GetComponent<Image>();if(_backdrop)_normalBackdrop=_backdrop.color;}
        _onClick = onClick;

        if (_button != null)
        {
            _button.onClick.RemoveAllListeners();
            _button.onClick.AddListener(HandleClick);
            _button.transition=Selectable.Transition.None;
            // 旧ColorTint曾直接压暗Text的CanvasRenderer，切换模式后也要清掉旧乘色与Tween。
            if(_text)_text.CrossFadeColor(Color.white,0,true,true);
        }
        _hover=_focused=false;RefreshFeedback();
    }

    internal void SetInteractable(bool value)
    {
        _button.interactable = value;
        RefreshFeedback();
    }

    private void HandleClick()
    {
        if(!_button.interactable||PresentationEffects.IsBusy||GuideHelpOverlay.BlocksInput)return;
        _onClick?.Invoke();
    }
    public void OnPointerEnter(PointerEventData e){_hover=true;RefreshFeedback();}
    public void OnPointerExit(PointerEventData e){_hover=false;RefreshFeedback();}
    public void OnSelect(BaseEventData e){_focused=true;RefreshFeedback();}
    public void OnDeselect(BaseEventData e){_focused=false;RefreshFeedback();}
    private void OnDisable(){_hover=_focused=false;RefreshFeedback();}
    private void RefreshFeedback()
    {
        if(!_text||!_hasNormalColor||!_button)return;
        // 淡色底把可点击回答与日志正文区分；悬停/键盘聚焦时继续提亮。
        if(_backdrop){var tint=_button.interactable&&(_hover||_focused)?new Color(.72f,.51f,.24f,.20f):_normalBackdrop;if(!_button.interactable)tint.a*=.4f;_backdrop.color=tint;}
        if(!_button.interactable){var disabled=_normalColor;disabled.a=.4f;_text.color=disabled;return;}
        // 纸面选项使用暖褐提亮，暗底选项使用暖白；禁用状态没有悬停或焦点反馈。
        float luminance=(_normalColor.r+_normalColor.g+_normalColor.b)/3f;
        _text.color=_hover||_focused?(luminance<.5f?new Color(.47f,.29f,.09f,1):new Color(1f,.89f,.54f,1)):_normalColor;
    }
}
