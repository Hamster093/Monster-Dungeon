/****************************************************
    文件：HUDPanel.cs
	作者：DADI
    邮箱: 1581507659@qq.com
    日期：2026-09-21 12:20:23
	功能：Nothing
*****************************************************/

using System;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UI;

public class HUDPanel : MonoBehaviour 
{
    public Transform ButtonRoot;

    private void Start()
    {
        BindButtonClick("Talk", OnTalk);
        BindButtonClick("Investigate", OnInvestigate);
        BindButtonClick("RouteReal", OnRouteReal);
        BindButtonClick("RouteFake", OnRouteFake);
        BindButtonClick("NextDay", OnNextDay);
        BindButtonClick("Backpack", OnOpenBackpack);
    }

    private void BindButtonClick(string buttonName,UnityAction action)
    {
        ButtonRoot.Find(buttonName).Find(buttonName+"Button").GetComponent<Button>().onClick.AddListener(action);
    }

    #region OnButton
    public void OnTalk()
    {
        Debug.Log($"搭话第 1 次，消耗 1 AP");
    }
    public void OnInvestigate()
    {
        Debug.Log($"调查第 1 次，消耗 1 AP");
    }
    public void OnRouteReal()
    {
        Debug.Log($"编造指路第 1 次，消耗 1 AP");
    }
    public void OnRouteFake()
    {
        Debug.Log($"真实指路第 1 次，消耗 1 AP");
    }
    private void OnNextDay()
    {
        NextDayEvent.Trigger();
    }
    private void OnOpenBackpack()
    {
        OpenBackPack.Trigger();
    }
    #endregion
}

