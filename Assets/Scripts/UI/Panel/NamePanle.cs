
/****************************************************
    文件：NamePanel.cs
	作者：DADI
    邮箱: 1581507659@qq.com
    日期：2026-09-21 22:12:35
	功能：物品名称提示面板，实时跟随鼠标
*****************************************************/

using UnityEngine;
using UnityEngine.UI;

public class NamePanel : MonoBehaviour
{
    [SerializeField] private Text _itemNameText;

    // 鼠标偏移量，避免面板正好压在鼠标上
    [SerializeField] private Vector2 _offset = new Vector2(100f, -150f);

    [SerializeField] private Canvas _canvas;

    private void Awake()
    {
        if (_canvas == null)
            _canvas = GetComponentInParent<Canvas>();
        Hide();
    }

    private void Update()
    {
        FollowMouse();
    }

    private void FollowMouse()
    {
        Vector2 mousePos = Input.mousePosition;

        if (_canvas != null && _canvas.renderMode != RenderMode.ScreenSpaceOverlay)
        {
            RectTransformUtility.ScreenPointToWorldPointInRectangle(
                _canvas.transform as RectTransform,
                mousePos,
                _canvas.worldCamera,
                out Vector3 worldPos);

            transform.position = worldPos + (Vector3)_offset;
        }
        else
        {
            // Screen Space - Overlay
            transform.position = mousePos + _offset;
        }
    }

    public void Show(string itemName)
    {
        _itemNameText.text = itemName;
        gameObject.SetActive(true);
        FollowMouse();
    }

    public void Hide()
    {
        gameObject.SetActive(false);
    }
}