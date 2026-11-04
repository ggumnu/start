using UnityEngine;

// [탄환 꼬리(궤적) 설정 에셋]
// 플레이어 총알 / 적 총알 / 수류탄 뒤에 별똥별처럼 따라오는 꼬리를 어떤 모양으로 그릴지 정하는 설정입니다.
// 실제로 꼬리를 붙이고 따라가게 하는 일은 BulletTrail.cs가 합니다.
//
// [만들기/위치 - 반드시 Resources 폴더]
// Project 창에서 Create > MobArchive > Bullet Trail Settings로 만들고, 이름을 "BulletTrailSettings" 그대로 두고
// Assets/Resources 폴더 바로 아래에 두세요. 그러면 모든 씬(본 게임/테스트 아레나)에서 자동으로 찾아 씁니다
// (씬에 따로 배치하거나 연결할 필요 없음). 이 에셋이 없거나 템플릿 칸이 비어 있으면 꼬리 없이 지금과 똑같이
// 동작합니다.
//
// [꼬리 모양은 템플릿 프리팹에서 직접 수정 - 형빈 확정 방식]
// 꼬리 템플릿 = Trail Renderer 컴포넌트 하나가 붙은 빈 오브젝트 프리팹입니다. 꼬리 길이(Time), 굵기 곡선(Width),
// 색/투명도 변화(Color), 재질(Materials)은 전부 그 프리팹의 Trail Renderer에서 직접 조절합니다.
// - 굵기: Width 값 1 = 총알 지름과 같은 굵기입니다. 총알 크기(크기 스탯, 분열탄 등)에 맞춰 자동으로 곱해집니다.
// - 색: "탄환 색 따라가기"가 켜져 있으면 템플릿 Color의 색에 총알 재질 색을 곱해서 씁니다(템플릿 색을 흰색으로
//   두면 총알 색 그대로). 투명도(알파) 변화는 템플릿에 정한 그대로 유지됩니다.
// - 사라질 때: 탄환이 사라지면 남은 꼬리를 그 자리에 고정하고 앞쪽 끝을 총알처럼 둥글게 다듬은 뒤, Release Fade Time
//   동안 전체가 함께 투명해지고 가늘어지며 사라집니다(형빈 확정 - 꼬리 앞쪽 끝이 일자로 잘려 "벽 뒤로 사라지는" 것처럼
//   보이던 문제 해결). 자세한 동작은 BulletTrail.cs 상단 주석 참고.
[CreateAssetMenu(fileName = "BulletTrailSettings", menuName = "MobArchive/Bullet Trail Settings")]
public class BulletTrailSettings : ScriptableObject
{
    // Resources.Load로 찾을 때 쓰는 이름입니다(Assets/Resources/BulletTrailSettings.asset).
    public const string ResourcePath = "BulletTrailSettings";

    [Header("총알 (플레이어 총알 + 적 총알 공통)")]
    [Tooltip("총알 꼬리 템플릿 프리팹입니다(Trail Renderer가 붙은 오브젝트). 비워두면 총알에 꼬리가 생기지 않습니다.")]
    public GameObject bulletTrailTemplate;

    [Tooltip("켜면 꼬리 색이 총알 재질 색을 따라갑니다(플레이어 노랑, 적 빨강/분홍 등 자동). 끄면 템플릿 색 그대로 씁니다.")]
    public bool bulletUseProjectileColor = true;

    [Header("수류탄")]
    [Tooltip("수류탄 꼬리 템플릿 프리팹입니다(Trail Renderer가 붙은 오브젝트). 비워두면 수류탄에 꼬리가 생기지 않습니다.")]
    public GameObject grenadeTrailTemplate;

    [Tooltip("켜면 꼬리 색이 수류탄 재질 색을 따라갑니다. 기본은 꺼짐 - 템플릿 색(흰색) 그대로 씁니다.")]
    public bool grenadeUseProjectileColor = false;

    [Header("탄환이 사라질 때 (사거리 끝/명중/벽/해킹 제거/폭발 공통)")]
    [Tooltip("총알이 사라진 뒤 남은 꼬리 전체가 함께 투명해지고 가늘어지며 사라지는 시간(초)입니다. 0이면 즉시 지웁니다.")]
    [Min(0f)] public float bulletReleaseFadeTime = 0.12f;

    [Tooltip("수류탄이 터진 뒤 남은 꼬리 전체가 함께 투명해지고 가늘어지며 사라지는 시간(초)입니다. 0이면 즉시 지웁니다.")]
    [Min(0f)] public float grenadeReleaseFadeTime = 0.15f;
}
