/****************************************************
    文件：PlacedItem.cs
	作者：DADI
    邮箱: 1581507659@qq.com
    日期：2026/10/9 21:50:49
	功能：
*****************************************************/

using UnityEngine;

public class PlacedItem
{
    public ItemShapeDefinition Def;
    public Vector2Int Origin;    // 归一化后 (0,0) 格在棋盘上的坐标
    public int Rotation;         // 0 / 90 / 180 / 270
    public ItemView View;

    public PlacedItem(ItemShapeDefinition def) { Def = def; }

    public Vector2Int[] GetCells()
    {
        var shape = Def.GetNormalizedShape(Rotation);
        var result = new Vector2Int[shape.Length];
        for (int i = 0; i < shape.Length; i++)
            result[i] = Origin + shape[i];
        return result;
    }
}