/****************************************************
    文件：ItemDefinition.cs
	作者：DADI
    邮箱: 1581507659@qq.com
    日期：2026/10/8 0:28:47
	功能：
*****************************************************/

using System.Collections.Generic;
using UnityEngine;

[CreateAssetMenu(menuName = "Backpack/ItemDefinition", fileName = "NewItem")]
public class ItemDefinition : ScriptableObject
{
    public string id = "item";
    public string displayName = "Item";
    public Color color = Color.white;

    [Tooltip("占格偏移，以(0,0)为基准")]
    public Vector2Int[] shape = { Vector2Int.zero };

    public bool canRotate = true;

    private Dictionary<int, Vector2Int[]> _shapeCache;

    public Vector2Int[] GetShape(int rotation)
    {
        if (_shapeCache == null) _shapeCache = new Dictionary<int, Vector2Int[]>();

        rotation = ((rotation % 360) + 360) % 360;

        if (_shapeCache.TryGetValue(rotation, out var cached) && cached != null)
            return cached;

        var result = new Vector2Int[shape.Length];
        for (int i = 0; i < shape.Length; i++)
        {
            var p = shape[i];
            switch (rotation)
            {
                case 90: result[i] = new Vector2Int(p.y, -p.x); break;
                case 180: result[i] = new Vector2Int(-p.x, -p.y); break;
                case 270: result[i] = new Vector2Int(-p.y, p.x); break;
                default: result[i] = new Vector2Int(p.x, p.y); break;
            }
        }

        int minX = int.MaxValue, minY = int.MaxValue;
        for (int i = 0; i < result.Length; i++)
        {
            if (result[i].x < minX) minX = result[i].x;
            if (result[i].y < minY) minY = result[i].y;
        }
        var norm = new Vector2Int(minX, minY);
        for (int i = 0; i < result.Length; i++)
            result[i] -= norm;

        _shapeCache[rotation] = result;
        return result;
    }

    public Vector2Int GetSize(int rotation)
    {
        var s = GetShape(rotation);
        int maxX = 0, maxY = 0;
        for (int i = 0; i < s.Length; i++)
        {
            if (s[i].x > maxX) maxX = s[i].x;
            if (s[i].y > maxY) maxY = s[i].y;
        }
        return new Vector2Int(maxX + 1, maxY + 1);
    }

    private void OnValidate()
    {
        _shapeCache = null;
    }
}