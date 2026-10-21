using System.Collections;
using UnityEngine;
using UnityEngine.EventSystems;

/// <summary>复用原分件的悬停说明；提示不接收输入，不改变按钮选择。</summary>
public sealed class UIHoverHint : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler, ISelectHandler, IDeselectHandler
{
    [SerializeField] private GameObject hint;
    private Coroutine pending;
    public void OnPointerEnter(PointerEventData e) { Hide(); pending = StartCoroutine(ShowLater()); }
    public void OnPointerExit(PointerEventData e) => Hide();
    public void OnSelect(BaseEventData e) { Hide(); pending = StartCoroutine(ShowLater()); }
    public void OnDeselect(BaseEventData e) => Hide();
    IEnumerator ShowLater() { yield return new WaitForSecondsRealtime(.35f); if (hint) hint.SetActive(true); pending = null; }
    void Hide() { if (pending != null) StopCoroutine(pending); pending = null; if (hint) hint.SetActive(false); }
    void OnDisable() => Hide();
}
