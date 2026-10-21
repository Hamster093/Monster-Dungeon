/****************************************************
    文件：DontDestroy.cs
	作者：DADI
    邮箱: 1581507659@qq.com
    日期：2026-09-21 20:07:55
	功能：切换场景不销毁
*****************************************************/

using UnityEngine;

public class DontDestroy : MonoBehaviour 
{
    private static GameObject[] persistentobjects=new GameObject[3];
    public int objectIndex;
    private void Awake()
    {
        if (persistentobjects[objectIndex]==null)
        {
            persistentobjects[objectIndex] = gameObject;
            DontDestroyOnLoad(this);
        }
        else if (persistentobjects[objectIndex]!=gameObject)
        {
            Destroy(gameObject);
        }


    }
}