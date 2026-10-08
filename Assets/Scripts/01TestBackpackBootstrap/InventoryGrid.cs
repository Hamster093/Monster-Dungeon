/****************************************************
    文件：InventoryGrid.cs
	作者：DADI
    邮箱: 1581507659@qq.com
    日期：2026/10/8 0:29:32
	功能：
*****************************************************/

using UnityEngine;

public class InventoryGrid
{
    public readonly int Width;
    public readonly int Height;
    private readonly PlacedItem[,] _cells;

    public InventoryGrid(int width, int height)
    {
        Width = width;
        Height = height;
        _cells = new PlacedItem[width, height];
    }

    public bool InBounds(Vector2Int c)
        => c.x >= 0 && c.y >= 0 && c.x < Width && c.y < Height;

    public PlacedItem Get(Vector2Int c)
        => InBounds(c) ? _cells[c.x, c.y] : null;

    public bool CanPlace(ItemDefinition def, Vector2Int origin, int rotation, PlacedItem ignore = null)
    {
        var shape = def.GetShape(rotation);
        for (int i = 0; i < shape.Length; i++)
        {
            var c = origin + shape[i];
            if (!InBounds(c)) return false;
            var occ = _cells[c.x, c.y];
            if (occ != null && occ != ignore) return false;
        }
        return true;
    }

    public void Place(PlacedItem item, Vector2Int origin, int rotation)
    {
        item.Origin = origin;
        item.Rotation = rotation;
        var shape = item.Def.GetShape(rotation);
        for (int i = 0; i < shape.Length; i++)
        {
            var c = origin + shape[i];
            _cells[c.x, c.y] = item;
        }
    }

    public void Remove(PlacedItem item)
    {
        var shape = item.Def.GetShape(item.Rotation);
        for (int i = 0; i < shape.Length; i++)
        {
            var c = item.Origin + shape[i];
            if (InBounds(c) && _cells[c.x, c.y] == item)
                _cells[c.x, c.y] = null;
        }
    }
}