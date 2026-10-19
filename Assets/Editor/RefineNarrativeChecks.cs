#if UNITY_EDITOR
using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

/// <summary>隔离产品中的构造状态：检查七人真实图片、显示反馈及转场；不代表自然游玩。</summary>
public static class RefineNarrativeChecks
{
    private const BindingFlags Flags=BindingFlags.Instance|BindingFlags.Public|BindingFlags.NonPublic;
    private static T Field<T>(object owner,string name)=>(T)owner.GetType().GetField(name,Flags).GetValue(owner);
    private static void Call(object owner,string name,params object[] args)=>owner.GetType().GetMethod(name,Flags).Invoke(owner,args);
    public static IEnumerator Run(Action<bool,string> check,Action<string> shot)
    {
        if(!Application.productName.EndsWith("_NarrativeValidation",StringComparison.Ordinal))throw new Exception("人物快验只允许隔离产品。");
        GuideHelpOverlay.SuppressAutomatic=true;
        if(GuideHelpOverlay.IsShowing)GuideHelpOverlay.HandleEscape();
        float deadline=Time.unscaledTime+8;
        while(PresentationEffects.IsBusy||GuideHelpOverlay.BlocksInput){if(Time.unscaledTime>deadline){check(false,"构造检查前转场未释放");yield break;}yield return null;}
        UIManager.Instance.Close<PlayerBuildPanel>();
        UIManager.Instance.Close<CharacterArchivePanel>();
        var campaign=SystemManager.Instance.Narrative;
        var runtime=Field<NarrativeRuntime>(campaign,"_runtime");
        var state=Field<NarrativeSystem.SaveData>(campaign,"_save");string oldStage=state.stage;
        string inkBefore=runtime.Story.state.ToJson();state.stage="playing";
        var preparation=UIManager.Instance.Get<PreparationPanel>();if(preparation)preparation.SetReceptionAvailable(false);
        var talk=UIManager.Instance.Open<TalkPanel>();talk.SetAutoCompleteAtEnd(false);talk.SetInstantText(true);
        var image=Field<Image>(talk,"_iconImage");var layouts=Field<TalkPanel.PortraitLayout[]>(talk,"_portraitLayouts");
        check(layouts!=null&&layouts.Length==7,"构造：七人具有独立立绘预设");
        string[] codes={"MY","FE","DW","SA","AU","DA","UN"};
        for(int i=0;i<7;i++)
        {
            string address=campaign.BasePortrait(i+1),name=ConfigDatabase.Instance.Adventurers[i+1].name;
            talk.Init(new List<DialogueLine>{new DialogueLine{iconName=address,speakerName=name,content="构造状态：检查立绘比例与基线。"}});
            talk.SetAutoCompleteAtEnd(false);talk.MoveNext();
            deadline=Time.unscaledTime+5;
            while(talk.IsPortraitLoading){if(Time.unscaledTime>deadline){check(false,"立绘加载超时 "+codes[i]);yield break;}yield return null;}
            yield return null;Canvas.ForceUpdateCanvases();
            var preset=layouts.Single(p=>address.StartsWith(p.addressPrefix,StringComparison.Ordinal));
            check(image.enabled&&image.sprite==Resources.Load<Sprite>(address),"构造：实际人物图片已显示 "+codes[i]);
            float ratio=image.sprite.rect.width/image.sprite.rect.height;
            float height=Mathf.Min(preset.height,preset.maxWidth/ratio);
            check(image.preserveAspect&&Mathf.Abs(image.rectTransform.rect.width/image.rectTransform.rect.height-ratio)<.001f,"构造：立绘保持原裁片比例 "+codes[i]);
            check(Vector2.Distance(image.rectTransform.sizeDelta,new Vector2(height*ratio,height))<.1f&&Vector2.Distance(image.rectTransform.anchoredPosition,preset.baseline)<.1f&&image.rectTransform.pivot==new Vector2(.5f,0)&&image.rectTransform.localScale==Vector3.one,"构造：独立大小与底线正确 "+codes[i]);
            var speaker=Field<Text>(talk,"_speakerNameText");
            check(speaker.text==name&&speaker.fontStyle==FontStyle.Bold,"构造：当前人物姓名有独立样式 "+codes[i]);
            shot("人物立绘_构造_"+codes[i]+"_"+Screen.width);yield return null;
        }
        check(runtime.Story.state.ToJson()==inkBefore,"构造：七人换图不推进Ink");
        check(EventSystem.current!=null,"构造：指针检查具有真实EventSystem");
        var pointer=new PointerEventData(EventSystem.current);
        var parent=Field<Transform>(talk,"_historyContent");
        var log=UnityEngine.Object.Instantiate(Resources.Load<DialogueHistoryItem>("DialogueHistoryItem"),parent);
        log.SetData(new DialogueLine{speakerName="Myth",content="构造日志原句"});
        Color normal=log.content.color;
        check(log.content.raycastTarget&&log.content.text.Contains("<color=#")&&log.content.text.Contains("Myth")&&log.content.text.Contains("构造日志原句"),"构造：日志姓名富文本区分且正文保留");
        ExecuteEvents.Execute(log.gameObject,pointer,ExecuteEvents.pointerEnterHandler);
        check(log.content.color!=normal,"构造：日志pointer-enter实际提亮");
        ExecuteEvents.Execute(log.gameObject,pointer,ExecuteEvents.pointerExitHandler);
        check(log.content.color==normal,"构造：日志pointer-exit恢复原色");
        var option=UnityEngine.Object.Instantiate(Resources.Load<DialogueOptionItem>("Panel/DialogueOptionItem"),parent);
        option.SetData("构造选项\n（检定—感知　难度：一般）",()=>{});
        var optionText=Field<Text>(option,"_text");var button=Field<Button>(option,"_button");normal=optionText.color;
        ExecuteEvents.Execute(option.gameObject,pointer,ExecuteEvents.pointerEnterHandler);
        check(optionText.color!=normal&&optionText.canvasRenderer.GetColor()==Color.white,"构造：选项pointer-enter提亮且旧暗乘色已清除");
        ExecuteEvents.Execute(option.gameObject,pointer,ExecuteEvents.pointerExitHandler);
        check(optionText.color==normal,"构造：选项pointer-exit恢复原色");
        Call(option,"SetInteractable",false);Color disabled=optionText.color;
        ExecuteEvents.Execute(option.gameObject,pointer,ExecuteEvents.pointerEnterHandler);
        check(!button.interactable&&optionText.color==disabled,"构造：禁用选项没有悬停提亮");
        UnityEngine.Object.Destroy(option.gameObject);UnityEngine.Object.Destroy(log.gameObject);yield return null;
        // 开局接口在同一场景中构造调用，绝不伪称自然跨场景验收。
        PresentationEffects.PrepareOpening();check(PresentationEffects.IsBusy,"构造：开局持黑立即阻止底层输入");
        yield return null;PresentationEffects.RevealOpening();deadline=Time.unscaledTime+5;
        while(PresentationEffects.IsBusy){if(Time.unscaledTime>deadline){check(false,"构造开局揭幕未释放");yield break;}yield return null;}
        check(!PresentationEffects.IsBusy&&runtime.Story.state.ToJson()==inkBefore,"构造：开局揭幕结束释放且不推进Ink");
        int covered=0;string key="refine-constructed:"+Guid.NewGuid().ToString("N");
        PresentationEffects.PlayVisitor(key,()=>{covered++;talk.PrepareVisitorPortrait(campaign.BasePortrait(1));},()=>!talk.IsPortraitLoading);
        check(PresentationEffects.IsBusy,"构造：访客淡暗开始立即锁输入");
        talk.MoveNext();check(runtime.Story.state.ToJson()==inkBefore,"构造：转场期间推进被阻挡");
        deadline=Time.unscaledTime+5;
        while(PresentationEffects.IsBusy){if(Time.unscaledTime>deadline){check(false,"构造访客转场未释放");yield break;}yield return null;}
        check(covered==1&&!talk.IsPortraitLoading&&runtime.Story.state.ToJson()==inkBefore,"构造：黑幕换图只执行一次，结束释放且Ink未变");
        PresentationEffects.PlayVisitor(key,()=>covered++,()=>true);yield return null;
        check(!PresentationEffects.IsBusy&&covered==1,"构造：同一访客转场标识不重播");
        talk.SetInstantText(false);UIManager.Instance.Close(talk);state.stage=oldStage;
        if(preparation)preparation.SetReceptionAvailable(oldStage=="preparation");
    }
}
#endif
