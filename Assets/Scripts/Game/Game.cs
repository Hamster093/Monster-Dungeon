/****************************************************
    文件：Game.cs
	作者：DADI
    邮箱: 1581507659@qq.com
    日期：2026-09-21 13:17:30
	功能：Nothing
*****************************************************/

using UnityEngine;

public class StratGameController : MonoBehaviour 
{
    private void Awake()
    {
        NextDayEvent.Register(OnNextDay);
    }

    private void OnNextDay()
    {
        GameMode.Day++;
        Debug.Log("下一天 当前第"+GameMode.Day+"天");
        if (GameMode.Day == 5)
        {
            Debug.Log("到达第五天 游戏结束");
            GamePassEvent.Trigger();
        }
    }

    private void OnDestroy()
    {
        NextDayEvent.UnRegister(OnNextDay);
    }
}