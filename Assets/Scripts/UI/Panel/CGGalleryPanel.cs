using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

/// <summary>主菜单CG鉴赏。只读取全局解锁记录，不修改经营档或终局判定。</summary>
public sealed class CGGalleryPanel : PanelBase
{
    [SerializeField] private GameObject _listRoot,_viewerRoot;
    [SerializeField] private Button[] _tiles;
    [SerializeField] private Image[] _thumbnails;
    [SerializeField] private Text[] _names;
    [SerializeField] private Text _count,_empty,_viewerTitle;
    [SerializeField] private Image _viewerImage;
    [SerializeField] private Button _back,_viewerBack,_previous,_next;
    private static CGGalleryPanel current;
    private static int escapedFrame=-1,closedFrame=-1;
    private CGGalleryProgress.Entry[] entries;
    private int viewing=-1,listGeneration,viewerGeneration,viewerOpenedFrame;
    private GameObject previousSelection;
    public static bool IsShowing=>current!=null&&current.IsOpen;
    public static bool BlocksInput=>IsShowing||closedFrame==Time.frameCount;
    public int VisibleCount=>entries?.Length??0;
    public bool IsViewing=>viewing>=0;
    public string ViewedAddress=>IsViewing?entries[viewing].address:null;
    public override bool IsModal=>true;

    public static void Show()
    {
        if(GuideHelpOverlay.BlocksInput)return;
        if(current==null)
        {
            var prefab=Resources.Load<CGGalleryPanel>("UI/CGGalleryPanel");
            if(!prefab){Debug.LogError("CG鉴赏面板缺失。");return;}
            current=Instantiate(prefab);
        }
        if(!current.IsOpen)current.OnOpen();
    }
    public override void OnInit()
    {
        for(int i=0;i<_tiles.Length;i++){int index=i;_tiles[i].onClick.AddListener(()=>OpenCG(index));}
        _back.onClick.AddListener(()=>OnEscapePressed());
        _viewerBack.onClick.AddListener(ReturnToList);
        _previous.onClick.AddListener(()=>Move(-1));_next.onClick.AddListener(()=>Move(1));
    }
    public override void OnOpen(object data=null)
    {
        previousSelection=EventSystem.current?EventSystem.current.currentSelectedGameObject:null;
        base.OnOpen(data);current=this;listGeneration++;viewerGeneration++;
        CGGalleryProgress.MigrateCompletedSaves();entries=CGGalleryProgress.GetUnlocked();viewing=-1;
        _listRoot.SetActive(true);_viewerRoot.SetActive(false);
        _count.text=entries.Length+" / "+CGGalleryProgress.Catalog.Count;
        _empty.gameObject.SetActive(entries.Length==0);_empty.text="尚未解锁CG";
        for(int i=0;i<_tiles.Length;i++)
        {
            bool visible=i<entries.Length;_tiles[i].gameObject.SetActive(visible);
            _thumbnails[i].enabled=false;_thumbnails[i].sprite=null;
            if(visible){_names[i].text=entries[i].name;LoadThumbnail(i,entries[i].address,listGeneration);}
        }
        ConfigureNavigation();Select(entries.Length>0?_tiles[0]:_back);
    }
    private async void LoadThumbnail(int index,string address,int generation)
    {
        var sprite=await SpriteLoader.LoadAsync(address);
        if(this==null||!IsOpen||generation!=listGeneration)return;
        _thumbnails[index].sprite=sprite;_thumbnails[index].enabled=sprite!=null;
    }
    public void OpenCG(int index)
    {
        if(!IsOpen||GuideHelpOverlay.BlocksInput||index<0||index>=VisibleCount)return;
        viewing=index;viewerOpenedFrame=Time.frameCount;_listRoot.SetActive(false);_viewerRoot.SetActive(true);
        _previous.interactable=_next.interactable=entries.Length>1;
        _viewerTitle.text=entries[index].name+"   "+(index+1)+" / "+entries.Length;
        _viewerImage.enabled=false;_viewerImage.sprite=null;LoadViewer(entries[index].address,++viewerGeneration);
        Select(_viewerBack);
    }
    private async void LoadViewer(string address,int generation)
    {
        var sprite=await SpriteLoader.LoadAsync(address);
        if(this==null||!IsOpen||!IsViewing||generation!=viewerGeneration)return;
        _viewerImage.sprite=sprite;_viewerImage.enabled=sprite!=null;
    }
    public void Move(int direction)
    {
        if(!IsViewing||GuideHelpOverlay.BlocksInput||VisibleCount<2)return;
        OpenCG((viewing+direction+VisibleCount)%VisibleCount);
    }
    public void ReturnToList()
    {
        if(GuideHelpOverlay.BlocksInput)return;
        int previous=viewing;viewing=-1;viewerGeneration++;
        _viewerRoot.SetActive(false);_listRoot.SetActive(true);_viewerImage.sprite=null;
        Select(previous>=0&&previous<VisibleCount?_tiles[previous]:_back);
    }
    public override bool OnEscapePressed()
    {
        if(GuideHelpOverlay.BlocksInput)return true;
        if(IsViewing)ReturnToList();else OnClose();return true;
    }
    public static bool HandleEscape()
    {
        // 主菜单与UIManager均可转发；同一帧只返回一层，不穿透到标题按钮。
        if(escapedFrame==Time.frameCount||closedFrame==Time.frameCount)return true;
        if(!IsShowing)return false;escapedFrame=Time.frameCount;return current.OnEscapePressed();
    }
    private void Update()
    {
        if(!IsOpen||GuideHelpOverlay.BlocksInput)return;
        if(Input.GetKeyDown(KeyCode.Escape)){HandleEscape();return;}
        if(IsViewing)
        {
            if(Input.GetKeyDown(KeyCode.LeftArrow))Move(-1);
            else if(Input.GetKeyDown(KeyCode.RightArrow))Move(1);
            else if(Time.frameCount>viewerOpenedFrame&&(Input.GetKeyDown(KeyCode.Return)||Input.GetKeyDown(KeyCode.Space)))ReturnToList();
        }
        else if(Input.GetKeyDown(KeyCode.Tab))
        {
            var selected=EventSystem.current?EventSystem.current.currentSelectedGameObject:null;
            int index=VisibleCount;for(int i=0;i<VisibleCount;i++)if(_tiles[i].gameObject==selected)index=i;
            index=(index+(Input.GetKey(KeyCode.LeftShift)?-1:1)+VisibleCount+1)%(VisibleCount+1);
            Select(index==VisibleCount?_back:_tiles[index]);
        }
    }
    private void ConfigureNavigation()
    {
        for(int i=0;i<VisibleCount;i++)
        {
            var nav=_tiles[i].navigation;nav.mode=Navigation.Mode.Explicit;
            nav.selectOnLeft=i>0?_tiles[i-1]:_back;nav.selectOnRight=i+1<VisibleCount?_tiles[i+1]:_back;
            nav.selectOnUp=i>=4?_tiles[i-4]:_back;nav.selectOnDown=i+4<VisibleCount?_tiles[i+4]:_back;
            _tiles[i].navigation=nav;
        }
        var backNav=_back.navigation;backNav.mode=Navigation.Mode.Explicit;
        backNav.selectOnDown=backNav.selectOnRight=VisibleCount>0?_tiles[0]:_back;_back.navigation=backNav;
    }
    private static void Select(Button button)
    {if(EventSystem.current&&button)EventSystem.current.SetSelectedGameObject(button.gameObject);}
    public override void OnClose()
    {
        listGeneration++;viewerGeneration++;viewing=-1;closedFrame=Time.frameCount;
        UIManager.Instance?.ConsumeModalCloseInput();base.OnClose();
        if(EventSystem.current&&previousSelection&&previousSelection.activeInHierarchy)EventSystem.current.SetSelectedGameObject(previousSelection);
        previousSelection=null;
    }
}
