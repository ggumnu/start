using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

// uGUI Button(시작 화면 등)에 붙여 마우스가 올라갈 때 호버음을 냅니다. 누를 수 없는 버튼이거나 isBlocked가 true면 소리 없음.
public class UIButtonHoverSound : MonoBehaviour, IPointerEnterHandler
{
    [System.NonSerialized] public System.Func<bool> isBlocked;

    private Button button;

    void Awake()
    {
        button = GetComponent<Button>();
    }

    public void OnPointerEnter(PointerEventData eventData)
    {
        if (button != null && !button.IsInteractable()) return;
        if (isBlocked != null && isBlocked()) return;
        GameAudio.Play(SfxId.UIHover);
    }
}
