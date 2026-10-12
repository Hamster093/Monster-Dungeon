/****************************************************
    文件：OpenMonsterStorageButton.cs
	作者：DADI
    日期：2026/10/12
	功能：测试按钮挂在"魔物仓库"按钮上，点击打开仓库面板
*****************************************************/

using UnityEngine;
using UnityEngine.UI;

[RequireComponent(typeof(Button))]
public class OpenMonsterStorageButton : MonoBehaviour
{
    private void Start()
    {
        GetComponent<Button>().onClick.AddListener(OnClick);
    }

    private void OnDestroy()
    {
        GetComponent<Button>().onClick.RemoveListener(OnClick);
    }

    private void OnClick()
    {
        ShopPanel.OpenMonsterStorage();
    }
}