using UnityEngine;

// 보스 프리팹 루트(BossAIController가 붙은 오브젝트)에 붙여서, 이 보스가 보스 체력바 HUD에
// 보여줄 "보스별 표시 정보"를 지정하는 아주 작은 데이터 컴포넌트입니다.
//   - 초상화(목업의 파란 하트 자리) 스프라이트
//   - 보스 이름, 이름 글자색, 이름 테두리색 (체력바 위쪽에 표시됩니다)
// 보스마다 다르게 하고 싶으면 보스 프리팹마다 이 컴포넌트를 하나씩 붙이고 칸을 채우면 됩니다.
// 이 컴포넌트가 없거나 Portrait 칸이 비어 있으면 보스 체력바 HUD(BossHealthBarDisplay)의
// Default Portrait Sprite가 대신 쓰이고, 그것도 비어 있으면 옅은 자리표시 색으로 보입니다.
// Boss Name 칸이 비어 있으면 이름은 표시되지 않습니다.
// 이름의 위치/크기/테두리 두께는 보스 프리팹이 아니라 BossHealthBarDisplay에서 조정합니다.
public class BossPortrait : MonoBehaviour
{
    [Tooltip("이 보스의 체력바 옆에 표시될 초상화 스프라이트입니다.")]
    public Sprite portrait;

    [Header("보스 이름 (체력바 위쪽에 표시)")]
    [Tooltip("체력바 위에 표시될 보스 이름입니다. 비워두면 이름을 표시하지 않습니다.")]
    public string bossName = "";
    [Tooltip("보스 이름 글자색입니다.")]
    public Color nameColor = Color.white;
    [Tooltip("보스 이름 테두리색입니다. 테두리 두께는 BossHealthBarDisplay의 Name Outline Thickness에서 정합니다.")]
    public Color nameOutlineColor = Color.black;
}
