/****************************************************
    文件：IconEntry.cs
	作者：DADI
    邮箱: 1581507659@qq.com
    日期：2026/9/29 17:43:29
	功能：AssetReferenceSprite的包装类 多包装了一个iconName
*****************************************************/

using System;
using UnityEngine.AddressableAssets;

[Serializable]
public class IconEntry
{
    public string iconName;              // 对应 DialogueLine.iconName
    public AssetReferenceSprite spriteRef;
}

