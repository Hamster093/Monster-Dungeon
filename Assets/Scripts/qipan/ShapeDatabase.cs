/****************************************************
    文件：ShapeDatabase.cs
	作者：DADI
    邮箱: 1581507659@qq.com
    日期：2026/10/9 23:52:25
	功能：
*****************************************************/

using System.Collections.Generic;
using UnityEngine;

[CreateAssetMenu(fileName = "ShapeDatabase", menuName = "Backpack/ShapeDatabase")]
public class ShapeDatabase : ScriptableObject
{
    public List<ItemShapeDefinition> Shapes = new List<ItemShapeDefinition>();

    private Dictionary<string, ItemShapeDefinition> _dict;

    public ItemShapeDefinition Get(string id)
    {
        if (string.IsNullOrEmpty(id)) return null;
        if (_dict == null)
        {
            _dict = new Dictionary<string, ItemShapeDefinition>();
            foreach (var s in Shapes)
                if (s != null && !string.IsNullOrEmpty(s.Id))
                    _dict[s.Id] = s;
        }
        return _dict.TryGetValue(id, out var v) ? v : null;
    }
}