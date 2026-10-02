/****************************************************
    文件：HUDPanel.cs
	作者：DADI
    邮箱: 1581507659@qq.com
    日期：2026-09-21 12:20:23
	功能：Nothing
*****************************************************/

using System;
using Unity.VisualScripting;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UI;

public class HUDPanel : PanelBase
{
    public Transform ButtonRoot;

    public override bool IsModal => false;

    public override bool OnEscapePressed() => false;

    public override void OnInit()
    {
        BindButtonClick("Backpack", ToggleBackpack);
    }


    private void Update()
    {
        if (Input.GetButtonDown("Inventory"))
        {
            ToggleBackpack();
        }
    }

    private void BindButtonClick(string buttonName,UnityAction action)
    {
        ButtonRoot.Find(buttonName).Find(buttonName+"Button").GetComponent<Button>().onClick.AddListener(action);
    }

    #region OnButton
    private void ToggleBackpack()
    {
        // 已打开就关闭，没打开就打开
        if (UIManager.Instance.IsOpen<BackpackPanel>())
            UIManager.Instance.Close<BackpackPanel>();
        else
            UIManager.Instance.Open<BackpackPanel>();
    }
    #endregion
}

