/****************************************************
    文件：GamePassEvent.cs
	作者：DADI
    邮箱: 1581507659@qq.com
    日期：2026-09-22 13:19:04
	功能：描述面板
*****************************************************/
using System;
using UnityEngine;
using UnityEngine.UI;
public class DescriptionPanel : PanelBase
{
    public sealed class OpenContext
    {
        public ItemData item;
        public Sprite icon;
        public Action onConfirm, onReturn;
        public int combatAdventurerId;
        public int contractId;
    }
    [SerializeField] private Button _routeConfirmButton;
    [SerializeField] private GameObject _combatArea;
    [SerializeField] private Text _combatDifficulty;
    [SerializeField] private Text _routeSummary;
    [SerializeField] private Text _contextTitle;
    [SerializeField] private Text _monsterState;
    public bool IsRouteContext => _route?.onConfirm != null;
    private OpenContext _route;
    private bool _routeConfirmed;
    [Header("显示组件")]
    [SerializeField] private Image _itemImage;
    [SerializeField] private Text _itemNameText;
    [SerializeField] private Text _itemDescriptionText;
    [SerializeField] private ScrollRect _descriptionScroll;
    [SerializeField] private Text _quantityText;

    [Header("按钮")]
    [SerializeField] private Button _confirmButton;

    // 记录当前正在展示的物品 id，用于异步加载回来后校验
    private int _currentItemId = -1;

    public override bool IsModal => true;

    public override void OnInit()
    {
        if (_confirmButton != null) _confirmButton.onClick.AddListener(OnConfirmClick);
        if (_routeConfirmButton != null) _routeConfirmButton.onClick.AddListener(() => {
            if (_route == null || _routeConfirmed) return;
            _routeConfirmed = true;
            _route.onConfirm?.Invoke();
        });
    }

    public override void OnOpen(object data = null)
    {
        base.OnOpen(data);

        _route = data as OpenContext;
        _routeConfirmed = false;
        if (_routeConfirmButton) _routeConfirmButton.gameObject.SetActive(_route?.onConfirm != null);
        var d = _route != null ? _route.item : data as ItemData;
        if (d == null)
        {
            ClearDisplay();
            return;
        }
        bool preview=_route?.combatAdventurerId>0 && _route.onConfirm!=null;
        var guide=GetComponent<GuideTopicAnchor>();
        if(guide&&guide.help)guide.help.gameObject.SetActive(preview);
        if(_combatArea)_combatArea.SetActive(preview);
        if(preview && _combatDifficulty) _combatDifficulty.text="战斗难度："+CombatResolver.Difficulty(_route.combatAdventurerId,d.id);
        // 身份和指路摘要固定在正文外；滚动长说明时仍能看到类别、报酬和战斗难度。
        if(_contextTitle)_contextTitle.text=d.id>100?"魔物详情":"道具详情";
        if(_routeSummary)_routeSummary.text=preview?RouteSummary(_route.combatAdventurerId,_route.contractId,d.id).TrimEnd():"";
        _itemNameText.text = d.itemName;
        if(_monsterState) {
            _monsterState.gameObject.SetActive(d.hasMonsterInstance);
            _monsterState.text=d.hasMonsterInstance?"魔物状态："+(d.monsterResting?"疲惫":"精神充沛"):"";
            _monsterState.color=d.monsterResting?new Color(.78f,.58f,.50f):new Color(.85f,.81f,.62f);
        }
        if(_quantityText) {
            if(d.id>100&&ConfigDatabase.Instance.Monsters.TryGetValue(d.id,out var monster)) {
                string[] types={"史莱姆类","哥布林类","犬类","植物类","构造类","龙类"};
                _quantityText.text="种类："+types[(int)monster.monsterType]+"　等级："+monster.level;
            }
            else _quantityText.text="持有："+(InventoryModel.Instance?.CountId(d.id)??0);
        }
        var description=d.description??"";
        if(d.id>100&&ConfigDatabase.Instance.Monsters.TryGetValue(d.id,out var detailMonster)) {
            string[] types={"史莱姆","哥布林","犬类","植物类","构造类","龙类"};
            string identity="等级："+detailMonster.level+"　种类："+types[(int)detailMonster.monsterType];
            int end=description.IndexOf('\n');
            // 只移除现有魔物详情生成器的完全相同首行，状态、进化效果和原正文均保留。
            if(end>=0&&description.Substring(0,end).TrimEnd('\r')==identity)
                description=description.Substring(end+1);
            if(d.hasMonsterInstance) {
                int stateEnd=description.IndexOf('\n');
                if(description.StartsWith("魔物状态：",StringComparison.Ordinal))
                    description=stateEnd>=0?description.Substring(stateEnd+1):"";
            }
        }
        _itemDescriptionText.text = description;
        if(_descriptionScroll)
        {
            Canvas.ForceUpdateCanvases();
            // 方正字体的字形下沿超出常规行高，留出一行底部余量，确保滚到底能看完整进化效果。
            float bottomPadding = _itemDescriptionText.fontSize * _itemDescriptionText.lineSpacing + 16;
            _itemDescriptionText.rectTransform.SetSizeWithCurrentAnchors(RectTransform.Axis.Vertical,
                Mathf.Max(_descriptionScroll.viewport.rect.height,_itemDescriptionText.preferredHeight + bottomPadding));
            _descriptionScroll.StopMovement();
            Canvas.ForceUpdateCanvases();
            _descriptionScroll.verticalNormalizedPosition=1;
        }

        // 图标异步加载
        _currentItemId = d.id;
        if (_route?.icon != null) { _itemImage.sprite = _route.icon; _itemImage.enabled = true; }
        else SetIconAsync(d.id, d.iconAddress);
    }

    /// <summary>只读指路摘要：复用正式结算的类别和报酬计算，不入队、不掷骰。</summary>
    public static string RouteSummary(int adventurerId, int contractId, int monsterId)
    {
        var db = ConfigDatabase.Instance;
        var run = RunStateManager.Instance.run;
        if (!db.Contracts.TryGetValue(contractId, out var contract) ||
            !db.Monsters.TryGetValue(monsterId, out var monster) ||
            !run.adventurers.TryGetValue(adventurerId, out var actor)) return "";
        var targetType = DailyCommissionSystem.TargetType(contract);
        var entry = new CommissionEntry {
            adventurerId = adventurerId, targetMonsterId = monsterId,
            requestedMonsterType = targetType, wrongRoute = monster.monsterType != targetType, rankFactor = -1
        };
        int reward = Mathf.RoundToInt(contract.reward * DailyCommissionSystem.RewardRate(entry, contract));
        string injury = actor.isSeverelyInjured ? "重伤，再次重伤会死亡" :
            actor.health == HealthState.Damaged ? "轻伤" : "无伤";
        return "类别：" + (entry.wrongRoute ? "不符" : "符合") + "　生还报酬：" + reward + "金币\n当前伤势：" + injury + "\n\n";
    }

    public override void OnClose()
    {
        var returned = !_routeConfirmed ? _route?.onReturn : null;
        _route = null;
        ClearDisplay();
        base.OnClose();
        returned?.Invoke();
    }

    private void ClearDisplay()
    {
        _currentItemId = -1;
        if(_combatArea)_combatArea.SetActive(false);
        if(_routeSummary)_routeSummary.text="";
        if(_contextTitle)_contextTitle.text="";
        if(_quantityText)_quantityText.text="";
        if(_monsterState)_monsterState.text="";
        _itemNameText.text = "";
        _itemDescriptionText.text = "";
        if (_itemImage != null)
        {
            _itemImage.sprite = null;
            _itemImage.enabled = false;
        }
    }

    private async void SetIconAsync(int itemId, string address)
    {
        if (_itemImage == null) return;

        if (string.IsNullOrEmpty(address))
        {
            _itemImage.sprite = null;
            _itemImage.enabled = false;
            return;
        }

        var sp = await SpriteLoader.LoadAsync(address);

        // await 回来后：对象可能已销毁，或面板已经切到别的物品了
        if (this == null || _itemImage == null) return;
        if (_currentItemId != itemId) return;

        _itemImage.sprite = sp;
        _itemImage.enabled = sp != null;
    }

    // 点确认关闭
    private void OnConfirmClick()
    {
        UIManager.Instance.Close(this);
    }

    // ESC 也关闭，并且消费 ESC
    public override bool OnEscapePressed()
    {
        UIManager.Instance.Close(this);
        return true;
    }
}
