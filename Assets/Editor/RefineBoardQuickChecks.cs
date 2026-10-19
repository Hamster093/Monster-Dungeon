#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using Newtonsoft.Json;
using UnityEditor;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

/// <summary>棋盘精修快验。静态入口不写档；运行入口只允许隔离产品、使用调用方提供的测试魔物。</summary>
public static class RefineBoardQuickChecks
{
    private const BindingFlags Flags=BindingFlags.Instance|BindingFlags.NonPublic;
    private static void Check(List<string> results,bool pass,string label)
    {if(!pass)throw new InvalidOperationException(label);results.Add(label);}

    public static string[] RunStatic()
    {
        var results=new List<string>();
        Check(results,!ItemView.ShouldStartDrag(.179f,13.9f),"短按小幅移动仍是单击");
        Check(results,ItemView.ShouldStartDrag(.18f,0),"静止长按0.18秒进入拖动");
        Check(results,ItemView.ShouldStartDrag(0,14),"14像素明确位移可进入拖动");
        Check(results,BackpackBoard.IdleHint=="左键点击查看魔物详情，长按拖动改变魔物位置。"&&BackpackBoard.DragHint=="按R旋转","棋盘两种提示与需求一致");
        var shop=AssetDatabase.LoadAssetAtPath<ShopPanel>("Assets/Resources/Panel/ShopPanel.prefab");
        var data=new SerializedObject(shop);var icons=data.FindProperty("_slotIcons");
        Check(results,data.FindProperty("_slotGrid").objectReferenceValue&&icons.arraySize==shop._itemSlots.Length,"仓库网格和全部图标已绑定");
        for(int i=0;i<icons.arraySize;i++)
        {
            var image=icons.GetArrayElementAtIndex(i).objectReferenceValue as Image;
            Check(results,image&&image.preserveAspect,"仓库图标保持原比例 "+i);
        }
        Check(results,data.FindProperty("_storageIconSize").vector2Value==new Vector2(176,176),"仓库图标为正方形可视区域");
        return results.ToArray();
    }

    /// <summary>调用前准备整备状态与一只可移动的魔物；不依赖原生鼠标，不能替代实际拖放验收。</summary>
    public static string[] Run(ItemView view)
    {
        if(!Application.isPlaying||!Application.productName.EndsWith("_NarrativeValidation",StringComparison.Ordinal))
            throw new InvalidOperationException("棋盘运行快验只允许隔离验证产品。");
        var results=new List<string>(RunStatic());var board=view.Board;var item=view.Item;
        var original=item.Origin;int rotation=item.Rotation;string uid=item.Uid;bool evolved=item.Evolved;int usedDay=item.UsedDay;string kind=item.EvolutionKind;
        Vector2Int destination=original;bool found=false;
        for(int y=0;y<board.Height&&!found;y++)for(int x=0;x<board.Width;x++)
        {
            var p=new Vector2Int(x,y);
            if(p!=original&&board.CanPlace(view.Def,p,rotation,item)){destination=p;found=true;break;}
        }
        Check(results,found,"测试棋盘存在另一处合法落点");
        var canvas=board.GetComponentInParent<Canvas>();var caster=canvas.GetComponent<GraphicRaycaster>();
        var camera=canvas.renderMode==RenderMode.ScreenSpaceOverlay?null:canvas.worldCamera;
        Vector2 ScreenCell(Vector2Int p)=>RectTransformUtility.WorldToScreenPoint(camera,board.GetCell(p).Rect.TransformPoint(board.GetCell(p).Rect.rect.center));
        PointerEventData Pointer(Vector2 press,Vector2 position)=>new PointerEventData(EventSystem.current){button=PointerEventData.InputButton.Left,pressPosition=press,position=position,pointerPressRaycast=new RaycastResult{module=caster}};
        try
        {
            Vector2Int grab=view.Def.GetNormalizedShape(rotation)[0];
            Vector2 press=ScreenCell(original+grab);var pointer=Pointer(press,press);
            view.OnPointerDown(pointer);pointer.position=ScreenCell(destination+grab);view.OnBeginDrag(pointer);view.OnDrag(pointer);
            Check(results,view.IsPlacing&&board.IsEditing,"拖动真实魔物时棋盘处于编辑态");
            view.OnEndDrag(pointer);
            Check(results,!view.IsPlacing&&!board.IsEditing&&item.Origin==destination,"合法松手立即落位，不需空格");
            Check(results,item.GetCells().All(c=>board.GetCell(c).Occupant==item)&&board.Items.Count(v=>v.Item?.Uid==uid)==1,"落位占格完整且同UID只登记一次");
            var saved=JsonConvert.DeserializeObject<NarrativeSystem.SaveData>(RunStateManager.Instance.run.narrativeSave);
            Check(results,saved.board.Count(b=>b.uid==uid)==1&&saved.board.Any(b=>b.uid==uid&&b.x==destination.x&&b.y==destination.y),"松手后的自动快照保存最终位置且无重复实例");
            press=ScreenCell(destination+grab);pointer=Pointer(press,press);view.OnPointerDown(pointer);pointer.position=new Vector2(-100,-100);view.OnBeginDrag(pointer);view.OnDrag(pointer);view.OnEndDrag(pointer);
            Check(results,item.Origin==destination&&item.Rotation==rotation&&!view.IsPlacing,"棋盘外松手恢复拖动前位置与旋转");
            Check(results,item.Uid==uid&&item.Evolved==evolved&&item.UsedDay==usedDay&&item.EvolutionKind==kind,"移动及非法回退保留实例全部状态");

            var ghostObject=new GameObject("BoardCheckGhost",typeof(RectTransform),typeof(CanvasGroup));ghostObject.SetActive(false);
            var ghost=ghostObject.AddComponent<ItemView>();ghost.IsGhost=true;ghostObject.SetActive(true);
            var dragObject=new GameObject("BoardCheckWarehouseDrag",typeof(RectTransform));var drag=dragObject.AddComponent<ShopSlotDragger>();
            try
            {
                drag.Board=null;drag.ResolveDragContext();
                Check(results,drag.Board==board,"仓库早期空棋盘引用在拖动入口补绑真实Instance");
                typeof(ShopSlotDragger).GetField("_ghost",Flags).SetValue(drag,ghost);
                Check(results,board.IsEditing&&!ghostObject.GetComponent<CanvasGroup>().blocksRaycasts,"仓库幽灵锁定整备且不挡底层命中");
                Check(results,!board.Items.Contains(ghost),"仓库幽灵不进入真实棋盘登记");
            }
            finally
            {
                typeof(ShopSlotDragger).GetField("_ghost",Flags).SetValue(drag,null);
                UnityEngine.Object.DestroyImmediate(ghostObject);UnityEngine.Object.DestroyImmediate(dragObject);
            }
            var instance=new MonsterInstance(int.Parse(view.Def.Id)){uid=uid,evolved=evolved,usedDay=RunStateManager.Instance.run.currentDay,evolutionKind=kind};
            var detail=ItemData.FromMonster(instance);
            Check(results,detail.hasMonsterInstance&&detail.monsterResting&&detail.description.Contains("魔物状态：疲惫"),"疲惫实例详情使用真实状态字");
            var product=ItemData.FromConfig(ConfigDatabase.Instance.Items[instance.monsterId]);
            Check(results,!product.hasMonsterInstance,"商店商品不伪造实例状态");
        }
        finally
        {
            view.CancelPlacement();board.PlaceItem(item,original,rotation);
        }
        return results.ToArray();
    }
}
#endif
