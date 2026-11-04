using UnityEngine;

// 생성된 빛 이펙트에 붙는 표식입니다. 어떤 아이템/설정 버전으로 만들어졌는지 기억해서, 같은
// 아이템이면 매 프레임 다시 만들지 않도록 합니다. 직접 붙일 필요는 없습니다.
public class ItemGlowMarker : MonoBehaviour
{
    [HideInInspector] public ItemDefinition item;
    [HideInInspector] public int version;
}
