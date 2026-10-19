#if UNITY_EDITOR
using UnityEngine;
using UnityEngine.EventSystems;

/// <summary>只用于编辑器验收：向原 StandaloneInputModule 提供鼠标状态，走原命中、点击和拖拽流程。</summary>
public class ArtPointerInput : BaseInput
{
    public Vector2 position;
    public int button;
    public bool down, up, held;
    public override bool mousePresent => true;
    public override Vector2 mousePosition => position;
    public override Vector2 mouseScrollDelta => Vector2.zero;
    public override int touchCount => 0;
    public override bool GetMouseButtonDown(int index) => index == button && down;
    public override bool GetMouseButtonUp(int index) => index == button && up;
    public override bool GetMouseButton(int index) => index == button && held;
    public override bool GetButtonDown(string name) => false;
    public override float GetAxisRaw(string name) => 0;
    public void Process(Vector2 point, bool press, bool release, bool holding, int mouseButton = 0)
    {
        position = point; button = mouseButton; down = press; up = release; held = holding;
        var module = EventSystem.current.GetComponent<StandaloneInputModule>();
        if (module == null) throw new System.InvalidOperationException("验收需要项目原 StandaloneInputModule");
        module.inputOverride = this;
        module.UpdateModule(); module.Process();
        down = up = false;
    }
}
#endif
