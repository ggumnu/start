using UnityEngine;

// 이 오브젝트(와 그 자식)의 렌더러를 플레이어 몸 연출에서 뺍니다: 피격 깜빡임(PlayerHitFlash), 112번 투명(PlayerInvisibility),
// 74번 돌진 잔상(DashAfterimage). 헤일로처럼 몸과 따로 늘 그대로 보여야 하는 것에 Add Component로 붙입니다(설정 칸 없음).
[DisallowMultipleComponent]
public class PlayerEffectExclude : MonoBehaviour
{
    // 렌더러가 이 컴포넌트가 붙은 오브젝트 아래(자기 자신 포함)에 있으면 true입니다. 꺼진 오브젝트에 붙어 있어도 찾습니다.
    public static bool IsExcluded(Renderer renderer)
    {
        return renderer != null && renderer.GetComponentInParent<PlayerEffectExclude>(true) != null;
    }
}
