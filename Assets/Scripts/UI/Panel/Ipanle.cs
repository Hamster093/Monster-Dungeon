public interface IPanel
{

    bool IsOpen { get; }
    bool IsModal { get; }//遮挡下层

    void OnInit();
    void OnOpen(object data = null);
    void OnClose();
    void OnRefresh(object data = null);//刷新
    void OnDestroy();

    bool OnEscapePressed(); // 返回 true 表示已消费
}