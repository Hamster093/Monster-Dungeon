/****************************************************
    文件：BackpackBootstrap.cs
	作者：DADI
    邮箱: 1581507659@qq.com
    日期：2026/10/8 0:31:36
	功能：
*****************************************************/

using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

public class BackpackBootstrap : MonoBehaviour
{
    [Header("Grid")]
    public int Width = 7;
    public int Height = 7;
    public float CellSize = 64f;

    private void Start()
    {
        Build();
    }

    private void Build()
    {
        EnsureEventSystem();

        // ---------- Canvas ----------
        var canvasGo = new GameObject("BackpackCanvas",
            typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
        var canvas = canvasGo.GetComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;

        var scaler = canvasGo.GetComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920f, 1080f);
        scaler.matchWidthOrHeight = 0.5f;

        // ---------- 全屏射线接收器（放在最底层） ----------
        var receiverGo = new GameObject("RaycastReceiver",
            typeof(RectTransform), typeof(Image));
        var receiverRect = (RectTransform)receiverGo.transform;
        receiverRect.SetParent(canvasGo.transform, false);
        receiverRect.anchorMin = Vector2.zero;
        receiverRect.anchorMax = Vector2.one;
        receiverRect.offsetMin = Vector2.zero;
        receiverRect.offsetMax = Vector2.zero;

        var receiverImg = receiverGo.GetComponent<Image>();
        receiverImg.color = new Color(0f, 0f, 0f, 0f);
        receiverImg.raycastTarget = true;

        // ---------- InventoryView ----------
        var view = receiverGo.AddComponent<InventoryView>();
        view.GridWidth = Width;
        view.GridHeight = Height;
        view.CellSize = CellSize;
        view.Build(canvasGo.transform, GetDefaultFont());

        // ---------- 测试物品 ----------
        SpawnTestItems(view);
    }

    private void EnsureEventSystem()
    {
        if (EventSystem.current != null) return;
        var es = new GameObject("EventSystem", typeof(EventSystem), typeof(StandaloneInputModule));
        DontDestroyOnLoad(es);
    }

    private void SpawnTestItems(InventoryView view)
    {
        // 盾牌 2x2
        var shield = MakeDef("shield", "盾", new Color(0.35f, 0.6f, 0.95f), new[]
        {
            new Vector2Int(0, 0), new Vector2Int(1, 0),
            new Vector2Int(0, 1), new Vector2Int(1, 1)
        });

        // 长枪 1x3（竖直）
        var spear = MakeDef("spear", "枪", new Color(0.9f, 0.75f, 0.35f), new[]
        {
            new Vector2Int(0, 0), new Vector2Int(0, 1), new Vector2Int(0, 2)
        });

        // 法杖 L 形
        var staff = MakeDef("staff", "杖", new Color(0.75f, 0.4f, 0.95f), new[]
        {
            new Vector2Int(0, 0), new Vector2Int(0, 1), new Vector2Int(1, 0)
        });

        // 剑 1x1
        var sword = MakeDef("sword", "剑", new Color(0.9f, 0.35f, 0.35f), new[]
        {
            new Vector2Int(0, 0)
        });

        // 药水 1x1
        var potion = MakeDef("potion", "药", new Color(0.4f, 0.9f, 0.6f), new[]
        {
            new Vector2Int(0, 0)
        });

        view.AddItem(shield, new Vector2Int(0, 0));
        view.AddItem(spear, new Vector2Int(3, 0));
        view.AddItem(staff, new Vector2Int(2, 3));
        view.AddItem(sword, new Vector2Int(5, 5));
        view.AddItem(potion, new Vector2Int(6, 6));
    }

    private ItemDefinition MakeDef(string id, string name, Color color, Vector2Int[] shape)
    {
        var def = ScriptableObject.CreateInstance<ItemDefinition>();
        def.id = id;
        def.displayName = name;
        def.color = color;
        def.shape = shape;
        def.canRotate = true;
        return def;
    }

    private Font GetDefaultFont()
    {
        Font font = null;
        try { font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf"); } catch { }
        if (font == null)
        {
            try { font = Resources.GetBuiltinResource<Font>("Arial.ttf"); } catch { }
        }
        return font;
    }
}