/****************************************************
    文件：BoardPanel.cs
	作者：DADI
    邮箱: 1581507659@qq.com
    日期：2026/10/17 15:41:02
	功能：棋盘面板
*****************************************************/

using UnityEngine;

public class BoardPanel : PanelBase
{
    public BackpackBoard Board; // 拖入你的 BackpackBoard 引用

    // 是否允许拖拽/点击，随时可改
    public void SetInteraction(bool allowDrag, bool allowClick)
    {
        if (Board != null)
        {
            Board.AllowDrag = allowDrag;
            Board.AllowClick = allowClick;
        }
    }

    public override void OnOpen(object data = null)
    {
        base.OnOpen(data);
        // ★ 每次打开时，自动置顶，防止被其他 UI 挡住
        transform.SetAsLastSibling();
    }
}
