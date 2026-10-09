/****************************************************
    文件：ItemShapeDefinition.cs
	作者：DADI
    邮箱: 1581507659@qq.com
    日期：2026-10-09 20:07:55
	功能：物品形状配置（ScriptableObject）
*****************************************************/

using System.Collections.Generic;
using UnityEngine;

[CreateAssetMenu(fileName = "NewShape", menuName = "Backpack/ItemShape")]
public class ItemShapeDefinition : ScriptableObject
{
    public string Id = "item";
    public string DisplayName = "Item";
    public Color Color = Color.white;

    [Tooltip("以 (0,0) 为基准的占格偏移，可以不在第一象限，代码会自动归一化")]
    public Vector2Int[] Shape = { Vector2Int.zero };

    public bool CanRotate = true;

    private Dictionary<int, Vector2Int[]> _cache;

    /// <summary>返回旋转后的归一化形状（最小 x/y 归到 0）</summary>
    public Vector2Int[] GetNormalizedShape(int rotation)
    {
        if (_cache == null) _cache = new Dictionary<int, Vector2Int[]>();
        rotation = ((rotation % 360) + 360) % 360;

        if (_cache.TryGetValue(rotation, out var cached) && cached != null)
            return cached;

        var rotated = new Vector2Int[Shape.Length];
        for (int i = 0; i < Shape.Length; i++)
        {
            var p = Shape[i];
            switch (rotation)
            {
                case 90: rotated[i] = new Vector2Int(p.y, -p.x); break;
                case 180: rotated[i] = new Vector2Int(-p.x, -p.y); break;
                case 270: rotated[i] = new Vector2Int(-p.y, p.x); break;
                default: rotated[i] = p; break;
            }
        }

        int minX = int.MaxValue, minY = int.MaxValue;
        for (int i = 0; i < rotated.Length; i++)
        {
            if (rotated[i].x < minX) minX = rotated[i].x;
            if (rotated[i].y < minY) minY = rotated[i].y;
        }
        var norm = new Vector2Int(minX, minY);
        for (int i = 0; i < rotated.Length; i++)
            rotated[i] -= norm;

        _cache[rotation] = rotated;
        return rotated;
    }

    public Vector2Int GetSize(int rotation)
    {
        var s = GetNormalizedShape(rotation);
        int maxX = 0, maxY = 0;
        for (int i = 0; i < s.Length; i++)
        {
            if (s[i].x > maxX) maxX = s[i].x;
            if (s[i].y > maxY) maxY = s[i].y;
        }
        return new Vector2Int(maxX + 1, maxY + 1);
    }

    private void OnValidate() { _cache = null; }
}
