
/****************************************************
    文件：NamePanel.cs
	作者：DADI
    邮箱: 1581507659@qq.com
    日期：2026-09-22 01:54:35
	功能：物品名称提示面板，实时跟随鼠标
*****************************************************/
using UnityEngine;

public abstract class PanelBase : MonoBehaviour, IPanel
{
    public bool IsOpen { get; protected set; }

    // 默认非模态，需要遮罩的面板自行 override
    public virtual bool IsModal => false;

    protected virtual void Awake()
    {
        OnInit();
    }

    public virtual void OnInit() { }

    public virtual void OnOpen(object data = null)
    {
        gameObject.SetActive(true);
        IsOpen = true;
    }

    public virtual void OnClose()
    {
        IsOpen = false;
        gameObject.SetActive(false);
    }

    public virtual void OnRefresh(object data = null) { }

    public virtual void OnDestroy() { }

    // 默认不消费 ESC
    public virtual bool OnEscapePressed() => false;
}