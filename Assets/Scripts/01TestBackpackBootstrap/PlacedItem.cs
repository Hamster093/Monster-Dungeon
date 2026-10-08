/****************************************************
    文件：PlacedItem.cs
	作者：DADI
    邮箱: 1581507659@qq.com
    日期：2026/10/8 0:29:05
	功能：
*****************************************************/

using UnityEngine;

public class PlacedItem
{
    public ItemDefinition Def;
    public Vector2Int Origin;
    public int Rotation;
    public ItemView View;

    public PlacedItem(ItemDefinition def)
    {
        Def = def;
    }

    public Vector2Int Size => Def.GetSize(Rotation);

    public Vector2Int[] GetCells()
    {
        var shape = Def.GetShape(Rotation);
        var cells = new Vector2Int[shape.Length];
        for (int i = 0; i < shape.Length; i++)
            cells[i] = Origin + shape[i];
        return cells;
    }
}
