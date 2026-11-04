using UnityEngine;

// 특수방 진입 조건(그냥/수류탄/해킹툴/체력 소모)이 "어떻게 동작하는지"를 한 곳에 모아두는 공용 설정 에셋입니다
// (형빈 확정 A안). 레이아웃 에셋(SpecialRoomLayout)에서는 조건 종류만 드롭다운으로 고르고, 실제 동작 값은 전부
// 여기서 정합니다 - 여기 값을 바꾸면 그 조건을 쓰는 모든 레이아웃에 한 번에 적용됩니다.
//
// [만들기/연결]
// Project 창에서 Create > MobArchive > Special Room Entry Settings로 하나만 만들고, 씬의 LevelBuilder >
// Special Room Entry Settings 칸에 연결합니다. 연결하지 않아도 아래 기본값으로 동작합니다.
//
// [단계별로 칸이 늘어납니다]
// - 2단계: 잠금 바리케이드 임시 큐브 색
// - 3단계: 잠금 해제 이펙트(수류탄 = 연쇄 폭파, 해킹툴 = 전용 이펙트) - 조건마다 따로 설정
// - 4단계: 체력 소모 방 출입 피해량(0.5) - SpecialRoomHealthHazard
// - 조건별 잠금 바리케이드 모델(Lock Barricade Prefab) - 수류탄 방과 해킹툴 방이 서로 다른 바리케이드로 보이게(형빈 확정 안 1)
// - 체력 소모 방 표시 장식(Health Marker Prefab) - 가시철창 등. 잠금 바리케이드 자리에 서고, 콜라이더가 꺼져 통과 가능(형빈 확정)
[CreateAssetMenu(fileName = "SpecialRoomEntrySettings", menuName = "MobArchive/Special Room Entry Settings")]
public class SpecialRoomEntrySettings : ScriptableObject
{
    [Header("수류탄으로 여는 특수방")]
    public SpecialRoomLockSettings grenade = new SpecialRoomLockSettings
    {
        placeholderColor = new Color(1f, 0.55f, 0.1f, 1f),
        burstCount = 4,
        spreadAcrossBarricade = true,
    };

    [Header("해킹툴로 여는 특수방")]
    public SpecialRoomLockSettings hackingTool = new SpecialRoomLockSettings
    {
        placeholderColor = new Color(0.2f, 0.9f, 1f, 1f),
        burstCount = 1,
        spreadAcrossBarricade = false,
    };

    [Header("체력 소모 특수방")]
    [Tooltip("체력 소모 특수방과 부모 방 사이를 지날 때마다(들어갈 때/나올 때 각각) 받는 피해입니다. 일반 피격으로 처리되어 " +
             "무적 시간 중에는 받지 않고, 21번 장신구 같은 피해 고정 효과도 적용되며, 체력이 이 값 이하이면 사망합니다.")]
    [Min(0f)] public float healthDamagePerPass = DefaultHealthDamagePerPass;

    [Tooltip("체력 소모 특수방의 부모 방 입구에 세우는 표시용 장식 프리팹입니다(예: 가시철창). 그냥 진입하는 방과 겉모습으로 " +
             "구분되게 하려는 용도입니다. 수류탄/해킹툴 잠금 바리케이드와 같은 자리(입구 벽 프리팹 안의 Special Lock Barricade " +
             "자리/방향, 없으면 벽에서 방 안쪽 자리)에 세워집니다. 잠금이 아니라 표시용이라 사라지지 않고(37번 보유 중에도 " +
             "그대로), 프리팹에 들어 있는 콜라이더는 전부 꺼져 플레이어와 총알이 그대로 통과합니다 - 골목을 막는 모양이어도 " +
             "지나가며 닿아서 피해를 입는 것처럼 보입니다. 비워 두면 아무것도 세우지 않습니다.")]
    public GameObject healthMarkerPrefab;

    // 설정 에셋이 연결되지 않았을 때 쓰는 출입 피해 기본값입니다(형빈 확정 0.5).
    public const float DefaultHealthDamagePerPass = 0.5f;

    // 조건에 맞는 잠금 설정을 돌려줍니다. 잠금이 없는 조건(그냥/체력 소모)이면 null입니다.
    public SpecialRoomLockSettings GetLockSettings(SpecialRoomEntryCondition condition)
    {
        switch (condition)
        {
            case SpecialRoomEntryCondition.Grenade: return grenade;
            case SpecialRoomEntryCondition.HackingTool: return hackingTool;
            default: return null;
        }
    }

    // 잠금 바리케이드가 필요한 조건인지(수류탄/해킹툴)입니다.
    public static bool NeedsLockBarricade(SpecialRoomEntryCondition condition)
    {
        return condition == SpecialRoomEntryCondition.Grenade || condition == SpecialRoomEntryCondition.HackingTool;
    }

    // 설정 에셋이 연결되지 않았을 때 쓰는 기본 색입니다(위 필드 초기값과 같음).
    public static Color DefaultPlaceholderColor(SpecialRoomEntryCondition condition)
    {
        return condition == SpecialRoomEntryCondition.HackingTool
            ? new Color(0.2f, 0.9f, 1f, 1f)
            : new Color(1f, 0.55f, 0.1f, 1f);
    }
}

// 잠금 바리케이드가 있는 조건(수류탄/해킹툴) 하나의 설정입니다.
//
// [해제 이펙트 - 적 처치 연쇄 폭파(CombatVfxSpawner 8번)와 같은 방식]
// 잠금이 풀리는 순간(바리케이드가 하얗게 사라지기 시작할 때) unlockVfxPrefab을 burstCount개, 짧은 랜덤 간격으로
// 터뜨립니다. spreadAcrossBarricade를 켜면 바리케이드 모양(가로 폭/높이) 안의 무작위 위치에서 하나씩 터지고(연쇄 폭파),
// 끄면 전부 바리케이드 중심에서 터집니다(단발 이펙트에 적합). 이펙트는 방과 무관한 씬 최상위에 생성되며, 파티클 재생
// 길이(최대 maxLifetime초)가 지나면 자동으로 지워집니다.
[System.Serializable]
public class SpecialRoomLockSettings
{
    [Header("잠금 바리케이드 모델")]
    [Tooltip("이 진입 조건의 특수방 잠금 바리케이드로 세울 프리팹입니다. 넣어 두면 방마다 뽑힌 진입 조건에 맞는 바리케이드가 " +
             "자동으로 섭니다 - 부모 방 입구 벽 프리팹은 조건과 상관없이 하나만 쓰면 됩니다. 입구 벽 프리팹 안의 Special Lock " +
             "Barricade 오브젝트 자리/방향에 세워지고(그 오브젝트는 꺼짐), 그 오브젝트가 없으면 벽에서 방 안쪽 자리에 북쪽 벽 기준 " +
             "방향으로 돌려 세웁니다. 비워 두면 입구 벽 프리팹의 Special Lock Barricade를 그대로 쓰고, 그것도 없으면 아래 색의 " +
             "임시 큐브를 세웁니다. 콜라이더(트리거 아님)가 있어야 플레이어/총알을 막고 수류탄/해킹툴 범위에 걸립니다.")]
    public GameObject lockBarricadePrefab;

    [Tooltip("잠금 바리케이드 모델이 하나도 없을 때(위 Lock Barricade Prefab도, 입구 벽 프리팹의 Special Lock Barricade도 없을 때) " +
             "대신 세우는 임시 큐브의 색입니다.")]
    public Color placeholderColor = Color.white;

    [Header("해제 이펙트")]
    [Tooltip("잠금이 풀릴 때 재생할 이펙트 프리팹입니다. 비워두면 이펙트 없이 바리케이드만 하얗게 사라집니다.")]
    public GameObject unlockVfxPrefab;

    [Tooltip("한 번 풀릴 때 터뜨릴 이펙트 개수입니다(연쇄 폭파는 여러 개, 단발 이펙트는 1).")]
    [Min(1)] public int burstCount = 1;

    [Tooltip("켜면 바리케이드 모양 안의 무작위 위치에서 하나씩 터집니다(연쇄 폭파). 끄면 전부 바리케이드 중심에서 터집니다.")]
    public bool spreadAcrossBarricade = true;

    [Tooltip("이펙트 위치를 월드 기준으로 이만큼 옮깁니다(m). 예: 바닥보다 조금 위에서 터지게 하려면 Y를 올리세요.")]
    public Vector3 positionOffset = Vector3.zero;

    [Tooltip("한 발이 터지고 다음 발이 터지기까지의 랜덤 딜레이 범위입니다(초). X=최소, Y=최대. 전부 0이면 동시에 터집니다.")]
    public Vector2 burstDelayRange = new Vector2(0.05f, 0.15f);

    [Tooltip("켜면 발마다 Y축 회전을 무작위로 줘서 전부 같은 모습으로 겹쳐 보이지 않게 합니다.")]
    public bool randomizeRotation = true;

    [Tooltip("이펙트 크기 배율입니다. 1이 원본 크기입니다(파티클 크기/속도 배율을 곱하는 방식 - 적 처치 폭발과 동일).")]
    [Min(0.01f)] public float scaleMultiplier = 1f;

    [Tooltip("이펙트 하나가 남아 있을 수 있는 최대 시간(초)입니다. 파티클 재생 길이를 알 수 없거나 무한 반복이면 이 시간에 지워집니다.")]
    [Min(0.1f)] public float maxLifetime = 5f;
}
