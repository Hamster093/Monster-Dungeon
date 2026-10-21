/****************************************************
    文件：ITypewriterEffect.cs
	作者：DADI
    邮箱: 1581507659@qq.com
    日期：2026-09-29 13:19:04
	功能：打字机效果抽象接口
*****************************************************/
public interface ITypewriterEffect
{
    /// <summary>是否正在播放</summary>
    bool IsTyping { get; }

    /// <summary>开始播放文本</summary>
    void Play(string content);

    /// <summary>跳过动画，立即显示完整文本</summary>
    void Skip();

    /// <summary>清空当前文本</summary>
    void Clear();
}
