#if UNITY_EDITOR
using System;using System.IO;using System.Linq;using System.Collections;using System.Collections.Generic;using System.Reflection;using Newtonsoft.Json;using Newtonsoft.Json.Linq;using UnityEditor;using UnityEditor.SceneManagement;using UnityEngine;using UnityEngine.UI;using UnityEngine.SceneManagement;
[InitializeOnLoad]public static class Doc5QuickChecks
{
 const string Key="Doc5QA";static IEnumerator routine;static double started,next;static bool busy;static List<string> checks=new List<string>(),errors=new List<string>();
 static T F<T>(object o,string f)=>(T)o.GetType().GetField(f,BindingFlags.Instance|BindingFlags.Public|BindingFlags.NonPublic).GetValue(o);
 static object Call(object o,string m,params object[] a)=>o.GetType().GetMethod(m,BindingFlags.Instance|BindingFlags.NonPublic|BindingFlags.Public).Invoke(o,a);
 static NarrativeSystem C=>SystemManager.Instance.Narrative;static NarrativeSystem.SaveData S=>F<NarrativeSystem.SaveData>(C,"_save");static NarrativeRuntime R=>F<NarrativeRuntime>(C,"_runtime");static RunState Run=>RunStateManager.Instance.run;
 static void Check(bool v,string n){if(!v)throw new Exception(n);checks.Add(n);}
 static Doc5QuickChecks(){if(SessionState.GetBool(Key,false)){EditorApplication.update+=Tick;started=EditorApplication.timeSinceStartup;Application.logMessageReceived+=Log;}}
 static void Log(string m,string stack,LogType t){if(t==LogType.Error||t==LogType.Exception)errors.Add(m);}
 public static void ConfigureAndRun(){Doc5IntegrationEditor.Configure();RunBatch();}
 public static void RunBatch(){if(!Application.productName.EndsWith("_NarrativeValidation"))throw new Exception("只允许隔离工程");
 var run=JsonConvert.DeserializeObject<RunState>(File.ReadAllText("../Global27Checkpoint.json"));var data=JsonConvert.DeserializeObject<NarrativeSystem.SaveData>(run.narrativeSave);
 var r=new NarrativeRuntime(Resources.Load<TextAsset>("Narrative/story27").text);r.Story.state.LoadJson(data.ink);r.Story.ChoosePathString("END_SELECTION");while(r.Story.canContinue)r.Read();
 data.ink=r.Story.state.ToJson();data.stage="playing";data.frame=null;data.ending="";data.uiVersion=6;data.archiveOpen=false;data.pendingRoute=null;data.panel=null;run.pendingDice=null;
 foreach(var a in run.adventurers.Values)a.isDead=false;run.adventurers[4].isDead=true;run.adventurers[1].currentSevenStats[SevenDimension.Tolerance]=90;run.adventurers[1].currentSevenStats[SevenDimension.Resolve]=75;
 run.narrativeSave=JsonConvert.SerializeObject(data);SaveAndLoadManager.Save("run_state",run);File.WriteAllText("../Doc5/SelectionFixture.json",JsonConvert.SerializeObject(run));
 SessionState.SetBool(Key,true);EditorSceneManager.OpenScene("Assets/Scenes/PlayerGame.unity");Size(1920,1080);EditorApplication.update+=Tick;Application.logMessageReceived+=Log;started=EditorApplication.timeSinceStartup;EditorApplication.EnterPlaymode();}
 static void Size(int w,int h){var v=EditorWindow.GetWindow(typeof(EditorWindow).Assembly.GetType("UnityEditor.GameView"));v.Show();typeof(NarrativeIntegrationChecks).GetMethod("SetGameViewSize",BindingFlags.NonPublic|BindingFlags.Static).Invoke(null,new object[]{v,w,h});}
 static void Tick(){if(!EditorApplication.isPlaying||EditorApplication.isCompiling||EditorApplication.timeSinceStartup<next)return;next=EditorApplication.timeSinceStartup+.04;EditorApplication.QueuePlayerLoopUpdate();try{if(EditorApplication.timeSinceStartup-started>180)throw new Exception("快验超时，阶段="+S.stage);if(errors.Count>0)throw new Exception(errors[0]);if(busy||!SystemManager.Instance.IsInitialized||!F<bool>(C,"_booted"))return;if(routine==null)routine=Scenario();if(!routine.MoveNext())Finish(true,"");}catch(Exception e){Finish(false,e.ToString());}}
 static void Finish(bool ok,string why){File.WriteAllText("../Doc5/QuickAcceptance.json",JsonConvert.SerializeObject(new{passed=ok,failure=why,checks,errors},Formatting.Indented));SessionState.SetBool(Key,false);EditorApplication.update-=Tick;Application.logMessageReceived-=Log;EditorApplication.isPlaying=false;EditorApplication.delayCall+=()=>EditorApplication.Exit(ok?0:1);}
 static IEnumerator Scenario(){for(int i=0;i<12;i++)yield return null;
 Check(S.uiVersion==7&&Run.saveVersion==8,"旧档升级：剧情7/存档8");Check(S.stage=="ending_selection","旧选人停点进入新面板");
 var neutral=Run.adventurers[1].currentSevenStats;foreach(int gap in new[]{0,1,20,21}){neutral[SevenDimension.Tolerance]=90-gap;var a=C.AssessSuccession(1);Check(a.gap==gap&&a.tendency==(gap==0?"容易接受":gap<=20?"不感兴趣":"非常厌恶"),"三档边界 gap="+gap);if(gap>0&&gap<=20)Check(a.chance==D20Service.SuccessPercent(6+3*gap,a.modifier),"成功率与骰子枚举一致 gap="+gap);}
 neutral[SevenDimension.Tolerance]=90;
 var panel=UIManager.Instance.Get<EndingPresentationPanel>();Check(panel.IsOpen&&!F<Button[]>(panel,"_actorButtons")[3].interactable,"死亡人物保留位置且禁选");
 foreach(var a in new[]{"MY","FE","DW","SA","AU","DA","UN"})Check(Resources.Load<Sprite>("Art/Endings/"+a)!=null,"CG可加载 "+a);
 var rt=new NarrativeRuntime(Resources.Load<TextAsset>("Narrative/story27").text);rt.Story.ChoosePathString("SUCCESSION_MY_01");var first=rt.Read().nodes[0].lines[0];Check(first.sourceId=="SUCCESSION_MY_01.0.2"&&first.content.Contains("什么种族"),"逐句标识与原稿正文一致");
 Shot("选人_1920");yield return null;ArtAcceptanceChecks.Pointer(F<Button[]>(panel,"_actorButtons")[0]);yield return null;Check(S.stage=="ending_confirm","点击人物只进入确认");Shot("确认_1920");yield return null;
 string before=JsonConvert.SerializeObject(Run.pendingDice);C.CancelEndingCandidate();Check(Run.pendingDice==null,"返回不掷骰");
 Size(1280,720);for(int i=0;i<5;i++)yield return null;Shot("选人_1280");yield return null;
 C.PreviewEndingCandidate(1);Shot("确认_1280");yield return null;C.CancelEndingCandidate();
 // 档案基本页显示进度；其他页隐藏，切页不推进剧情。
 UIManager.Instance.Close<EndingPresentationPanel>();UIManager.Instance.Open<TalkPanel>();var archive=UIManager.Instance.Open<CharacterArchivePanel>();archive.Refresh(1);archive.SelectTab(0);Shot("档案_1280");yield return null;Size(1920,1080);for(int q=0;q<5;q++)yield return null;Shot("档案_1920");yield return null;Size(1280,720);for(int q=0;q<5;q++)yield return null;Check(archive.GetComponentsInChildren<Text>().Count(t=>t.name=="SuccessionInfo")==1,"档案倾向仅一层可见文字");Check(F<Text>(archive,"_succession").text.Contains("容易接受")&&F<Text>(archive,"_succession").text.Contains(C.AssessSuccession(1).progress),"档案显示共用倾向和实际条件进度");archive.SelectTab(1);Check(!F<Text>(archive,"_succession").gameObject.activeSelf,"记录页隐藏倾向");UIManager.Instance.Close<CharacterArchivePanel>();C.PresentPause();
 // 保存确认上下文，重载仍停在同一人物。
 C.PreviewEndingCandidate(1);C.Save();RunStateManager.Instance.SaveSlot(4);SceneManager.LoadScene("PlayerGame");for(int i=0;i<18;i++)yield return null;Check(S.stage=="ending_confirm"&&C.EndingCandidate==1,"确认框中途读档恢复原人选");
 panel=UIManager.Instance.Get<EndingPresentationPanel>();ArtAcceptanceChecks.Pointer(F<Button>(panel,"_confirm"));yield return null;Check(Run.pendingDice==null&&S.stage=="playing","条件达标无需掷骰直接进入人物终局");
 // 数据快验遍历一条原终局；真实指针流程另行在独立运行版验收。
 UIManager.Instance.Close<TalkPanel>();for(int guard=0;guard<120&&S.stage!="ending_cg";guard++){var f=C.GetFrame();if(f!=null){var node=f.nodes[0];if(node.options.Count>0)C.Choose(node.options[0]);else C.Next();}yield return null;}
 Check(S.stage=="ending_cg"&&C.Ending=="succession:MY","原人物终局完成后进入对应CG");C.PresentPause();for(int i=0;i<130;i++)yield return null;Shot("CG_1280");yield return null;
 C.Save();SceneManager.LoadScene("PlayerGame");for(int q=0;q<55;q++)yield return null;
 Check(S.stage=="ending_cg"&&F<Image>(UIManager.Instance.Get<EndingPresentationPanel>(),"_cgImage").enabled,"CG中途读档恢复可见画面");
 // 普通接任检定的已掷结果、物品与成长随同一存档恢复。
 C.CompleteEndingCG();UIManager.Instance.Close<EndingPresentationPanel>();S.ending="";S.endingPresentationPhase=null;S.stage="playing";S.frame=null;Run.adventurers[1].currentSevenStats[SevenDimension.Tolerance]=80;R.Story.ChoosePathString("SUCCESSION");R.Set("current_actor","MY");
 C.GetFrame();C.PresentPause();Check(Run.pendingDice!=null&&!Run.pendingDice.guaranteedFailure&&Run.pendingDice.annotation.Contains("精神力"),"不感兴趣使用带精神力标注的真实检定");
 Run.pendingDice.roll=85;string snapshot=JsonConvert.SerializeObject(Run.pendingDice);int xpSnapshot=Run.player.proficiency,itemSnapshot=InventoryModel.Instance.CountId(3);C.Save();SceneManager.LoadScene("PlayerGame");for(int i=0;i<18;i++)yield return null;
 Check(JsonConvert.SerializeObject(Run.pendingDice)==snapshot&&Run.player.proficiency==xpSnapshot&&InventoryModel.Instance.CountId(3)==itemSnapshot,"接任骰读档保留点数/物品/成长");
 Run.pendingDice.confirmed=true;S.stage="playing";C.Next();C.GetFrame();Check(Run.pendingDice==null&&!S.successionFailed,"普通说服成功沿用已确认结果");
 // 检验强制失败不被自然100变成成功、不登记成长。
 UIManager.Instance.Close<DiceCheckPanel>();S.ending="";S.stage="playing";S.frame=null;Run.adventurers[1].currentSevenStats[SevenDimension.Tolerance]=69;
 int xp=Run.player.proficiency,items=InventoryModel.Instance.CountId(3);bool paused=false;try{Call(C,"Check","succession","MY",0);}catch(TargetInvocationException e){paused=e.InnerException is NarrativeRuntime.PendingCheckException;}
 Check(paused&&Run.pendingDice.guaranteedFailure,"超过20进入必败骰子演出");Run.pendingDice.roll=100;Check(!Run.pendingDice.Result().Success&&!Run.pendingDice.Result().IsCritical,"必败不会自然100大成功");
 Run.pendingDice.confirmed=true;S.stage="playing";Check(!(bool)Call(C,"Check","succession","MY",0)&&Run.player.proficiency==xp&&InventoryModel.Instance.CountId(3)==items,"必败不消耗护符或成长");
 R.Story.ChoosePathString("END_SELECTION");for(int g=0;g<8&&S.ending!="swallowed";g++){C.Next();C.GetFrame();}Check(S.stage!="ending_selection"&&S.ending=="swallowed","确认失败跳吞噬且不返回选人");
 // 以原稿稳定句标识抽查受伤返程、报复与Sarah透明副本的实际渲染。
 UIManager.Instance.Close<DiceCheckPanel>();UIManager.Instance.Close<EndingPresentationPanel>();S.stage="playing";
 var talk=UIManager.Instance.Open<TalkPanel>();talk.SetAutoCompleteAtEnd(false);talk.SetInstantText(true);UIManager.Instance.Close<CharacterArchivePanel>();
 foreach(var id in new[]{"DAY01_END.8.b.26.b.3.b.1","REVENGE.129.b.48.b.3","RETURN_FOLLOWUP.39.b.7.b.12.b.6.b.12.b.16"}) {
  var literal=(JArray)JObject.Parse(Resources.Load<TextAsset>("Narrative/story27").text)["root"];JToken token=literal;
  foreach(var part in id.Split('.')) {var a=(JArray)token;token=int.TryParse(part,out int at)?a[at]:((JObject)a.Last)[part];}
  var line=new DialogueLineData{speaker="旁白",content=token.ToString().Substring(1),sourceId=id};string address=(string)Call(C,"ResolvePortrait",line);
  Check(address.Contains(id.StartsWith("DAY")?"IMG_7629":id.StartsWith("REVENGE")?"IMG_7651":"Sarah-hurt-transparent"),"原稿受伤句命中对应血脸 "+id);
  talk.Init(new List<DialogueLine>{new DialogueLine{speakerName="旁白",content=line.content,iconName=address}});for(int q=0;q<4;q++)yield return null;
  Shot(id.StartsWith("DAY")?"受伤返程_1280":id.StartsWith("REVENGE")?"报复_1280":"Sarah透明副本_1280");yield return null;
 }
 // 提供仅隔离产品使用的真实点击夹具。
 SaveAndLoadManager.Save("run_state",JsonConvert.DeserializeObject<RunState>(File.ReadAllText("../Doc5/SelectionFixture.json")));
 }
 static void Shot(string n){busy=true;UIManager.Instance.StartCoroutine(Capture(n));}
 static IEnumerator Capture(string n){yield return new WaitForSecondsRealtime(.2f);Canvas.ForceUpdateCanvases();int w=Screen.width,h=Screen.height;var cam=Camera.main;var target=new RenderTexture(w,h,24);var prev=cam.targetTexture;var active=RenderTexture.active;var canvases=UnityEngine.Object.FindObjectsOfType<Canvas>().Where(c=>c.isRootCanvas).ToArray();var modes=canvases.Select(c=>c.renderMode).ToArray();var cameras=canvases.Select(c=>c.worldCamera).ToArray();cam.targetTexture=target;foreach(var c in canvases){c.renderMode=RenderMode.ScreenSpaceCamera;c.worldCamera=cam;}Canvas.ForceUpdateCanvases();cam.Render();RenderTexture.active=target;var tex=new Texture2D(w,h,TextureFormat.RGB24,false);tex.ReadPixels(new Rect(0,0,w,h),0,0);tex.Apply();Directory.CreateDirectory("../Doc5/Shots");File.WriteAllBytes("../Doc5/Shots/"+n+".png",tex.EncodeToPNG());for(int i=0;i<canvases.Length;i++){canvases[i].renderMode=modes[i];canvases[i].worldCamera=cameras[i];}cam.targetTexture=prev;RenderTexture.active=active;UnityEngine.Object.Destroy(tex);UnityEngine.Object.Destroy(target);busy=false;}
}
#endif
