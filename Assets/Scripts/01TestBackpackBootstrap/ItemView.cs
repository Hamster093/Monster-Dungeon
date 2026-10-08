/****************************************************
    文件：ItemView.cs
	作者：DADI
    邮箱: 1581507659@qq.com
    日期：2026/10/8 0:29:49
	功能：
*****************************************************/

using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

public class ItemView : MonoBehaviour
{
    public PlacedItem Item { get; private set; }
    public RectTransform Rect { get; private set; }
    public CanvasGroup CanvasGroup { get; private set; }

    private float _cellSize;
    private Font _font;
    private readonly List<RectTransform> _cellRects = new List<RectTransform>();
    private readonly List<Image> _cellImages = new List<Image>();
    private Text _label;

    public static ItemView Create(PlacedItem item, Transform parent, float cellSize, Font font)
    {
        var go = new GameObject($"Item_{item.Def.id}", typeof(RectTransform));
        var rect = (RectTransform)go.transform;
        rect.SetParent(parent, false);
        rect.pivot = new Vector2(0f, 0f);
        rect.anchorMin = new Vector2(0f, 0f);
        rect.anchorMax = new Vector2(0f, 0f);
        rect.localScale = Vector3.one;

        var view = go.AddComponent<ItemView>();
        view.Item = item;
        view.Rect = rect;
        view._cellSize = cellSize;
        view._font = font;
        view.CanvasGroup = go.AddComponent<CanvasGroup>();
        view.CanvasGroup.blocksRaycasts = false;

        item.View = view;
        view.Rebuild();
        return view;
    }

    public void Rebuild()
    {
        var shape = Item.Def.GetShape(Item.Rotation);
        var size = Item.Def.GetSize(Item.Rotation);

        Rect.sizeDelta = new Vector2(size.x * _cellSize, size.y * _cellSize);

        // 复用 / 补齐 cell 对象
        while (_cellRects.Count < shape.Length)
        {
            var cellGo = new GameObject("Cell", typeof(RectTransform), typeof(Image));
            var r = (RectTransform)cellGo.transform;
            r.SetParent(Rect, false);
            r.pivot = new Vector2(0f, 0f);
            r.anchorMin = new Vector2(0f, 0f);
            r.anchorMax = new Vector2(0f, 0f);

            var img = cellGo.GetComponent<Image>();
            img.raycastTarget = false;

            _cellRects.Add(r);
            _cellImages.Add(img);
        }

        float inset = _cellSize * 0.06f;

        for (int i = 0; i < _cellRects.Count; i++)
        {
            var r = _cellRects[i];
            if (i < shape.Length)
            {
                r.gameObject.SetActive(true);
                r.sizeDelta = new Vector2(_cellSize - inset * 2f, _cellSize - inset * 2f);
                r.anchoredPosition = new Vector2(
                    shape[i].x * _cellSize + inset,
                    shape[i].y * _cellSize + inset);
                _cellImages[i].color = Item.Def.color;
            }
            else
            {
                r.gameObject.SetActive(false);
            }
        }

        EnsureLabel();
        RefreshPosition();
    }

    void EnsureLabel()
    {
        if (_font == null) return;

        if (_label == null)
        {
            var textGo = new GameObject("Label", typeof(RectTransform), typeof(Text));
            var r = (RectTransform)textGo.transform;
            r.SetParent(Rect, false);
            r.anchorMin = Vector2.zero;
            r.anchorMax = Vector2.one;
            r.offsetMin = Vector2.zero;
            r.offsetMax = Vector2.zero;

            _label = textGo.GetComponent<Text>();
            _label.font = _font;
            _label.alignment = TextAnchor.MiddleCenter;
            _label.color = Color.white;
            _label.fontSize = 12;
            _label.raycastTarget = false;
            _label.horizontalOverflow = HorizontalWrapMode.Wrap;
            _label.verticalOverflow = VerticalWrapMode.Overflow;

            var outline = textGo.AddComponent<Outline>();
            outline.effectColor = new Color(0f, 0f, 0f, 0.85f);
            outline.effectDistance = new Vector2(1f, -1f);
        }

        _label.text = Item.Def.displayName;
    }

    public void RefreshPosition()
    {
        Rect.anchoredPosition = new Vector2(
            Item.Origin.x * _cellSize,
            Item.Origin.y * _cellSize);
    }

    public void SetDragging(bool dragging)
    {
        CanvasGroup.alpha = dragging ? 0.85f : 1f;
        if (dragging) Rect.SetAsLastSibling();
    }
}