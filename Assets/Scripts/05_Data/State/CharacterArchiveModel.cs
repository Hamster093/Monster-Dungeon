/****************************************************
    文件：CharacterArchiveModel.cs
	作者：DADI
    邮箱: 1581507659@qq.com
    日期：2026/10/2 12:15:31
	功能：角色档案模型

*****************************************************/

using System;
using System.Collections.Generic;

[Serializable]
public class CharacterArchiveEntry : DialogueLine
{
}

/// <summary>
/// 人物档案数据模型（纯C#单例），按说话人分组存储
/// </summary>
public class CharacterArchiveModel
{
    public static CharacterArchiveModel Instance { get; private set; }

    private readonly Dictionary<string, List<CharacterArchiveEntry>> _archive = new();

    public static void Initialize()
    {
        if (Instance != null) return;
        Instance = new CharacterArchiveModel();
    }

    /// <summary>
    /// 写入一条对话到指定人物的档案
    /// </summary>
    public void Add(string characterId, DialogueLine line)
    {
        if (string.IsNullOrEmpty(characterId) || line == null) return;

        if (!_archive.TryGetValue(characterId, out var list))
        {
            list = new List<CharacterArchiveEntry>();
            _archive[characterId] = list;
        }

        list.Add(new CharacterArchiveEntry
        {
            speakerName = line.speakerName,
            iconName = line.iconName,
            content = line.content,
        });
    }

    public IReadOnlyList<DialogueLine> Get(string characterId)
    {
        if (string.IsNullOrEmpty(characterId))
            return Array.Empty<DialogueLine>();

        return _archive.TryGetValue(characterId, out var list)
            ? list
            : (IReadOnlyList<DialogueLine>)Array.Empty<DialogueLine>();
    }

    public void Clear(string characterId)
    {
        if (!string.IsNullOrEmpty(characterId)) _archive.Remove(characterId);
    }

    public void ClearAll() => _archive.Clear();
}