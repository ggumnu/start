using System.Collections.Generic;
using UnityEngine;

// 패시브 아이템 하나를 정의하는 데이터입니다. RoomVariant/EnemyDefinition과 동일한
// 패턴 - 아이템 하나마다 스크립트를 만드는 대신, 이 에셋 하나를 만들고 인스펙터에서
// statModifiers/specialEffects 값만 채우면 새 아이템이 완성됩니다.
//
// [2D 아이콘]
// 3D 보물상자(보급드론 등) 위에 표시되는 아이템 자체는 2D 스프라이트입니다. icon 필드에
// 스프라이트를 등록하면 ItemPedestal이 자동으로 SpriteRenderer에 적용합니다.
//
// [itemId - 중복 획득 방지의 핵심]
// 같은 아이템을 두 번 얻을 수 없도록 하기 위해, 모든 아이템은 고유한 itemId를 가져야
// 합니다. PlayerInventory가 이 id로 보유 여부를 판정하므로, 반드시 프로젝트 전체에서
// 겹치지 않는 값으로 지어주세요 (예: "atk_up_small", "speed_down_health_up" 등).
//
// [특수 효과 - statModifiers와 별개]
// 스탯 가감/배율이 아닌 효과(공격 방식 변경, 발사/소멸 시점에 반응하는 효과 등)는
// specialEffects 리스트에 등록합니다. 인스펙터에서 이 필드를 보면 드롭다운으로 특수 효과
// 종류를 고를 수 있고(PassiveItemEffectDrawer.cs), 고른 타입의 세부 필드만 표시됩니다.
// GetEffects()가 statModifiers와 specialEffects를 하나로 합쳐서 반환하므로, 새 특수 효과
// 클래스를 추가해도 이 스크립트나 PlayerInventory/ItemPedestal 쪽 코드는 손댈 필요가
// 없습니다. (지금 세션은 이 등록 구조 자체만 만드는 단계라, 실제 특수 효과 클래스(폭발/관통 등)는
// 아직 하나도 없습니다 - specialEffects 리스트는 당장은 비어있는 채로 둬도 됩니다.)
//
// [등급(Rarity)/태그(Tags)/분류(Category) - v1.23 추가]
// 세 필드 모두 기존 아이템 에셋에는 없던 새 필드라, 이미 만들어두신 에셋을 열면 자동으로
// 아래 기본값(rarity=Tier1, category=Passive, tags=빈 리스트)이 채워집니다 - 기존
// 아이템을 일일이 손볼 필요가 없고, 동작도 지금까지와 완전히 동일합니다.
//   - rarity: weight(등장 가중치)를 대체하지 않는 순수 분류 정보입니다. weight는 지금처럼
//     계속 직접 입력하는 값이고, 등급은 그 옆에 "이 아이템은 대략 어느 등급이다"라고
//     표시해두는 용도입니다(같은 등급끼리도 weight를 서로 다르게 줄 수 있습니다). 등급별
//     권장 weight 가이드는 아래 ItemRarity 툴팁을 참고하세요.
//   - tags: 학생/학원/동아리 같은 세트 효과용 태그를 자유 텍스트로 적어두는 칸입니다.
//     지금은 드롭다운/검증 없이 그냥 문자열을 직접 입력합니다 - 오타가 나도 잡아주지
//     않으니 표기를 통일해서 적어주세요.
//   - category: 이 아이템이 [패시브]/[액티브]/[장신구]/[일회용]/[음식] 중 어디에
//     속하는지 나타냅니다. 지금은 이 값이 딱 한 곳(ItemDatabase.GetItemsForContext의
//     category 필터)에서만 실제로 쓰입니다 - 보물상자가 뽑는 패시브 풀에 Passive가
//     아닌 아이템이 섞여 나오지 않도록 막는 안전장치입니다. 액티브/장신구/일회용/음식의
//     실제 동작(슬롯 제한, 교체, 소모, 전용 UI)은 아직 구현되지 않았고, 각 분류를 실제로
//     만들 때 별도로 진행합니다 - 지금 이 값을 Passive가 아닌 다른 값으로 바꿔서 만든
//     아이템은 아직 아무 보물상자에도 등장하지 않습니다(등장하게 만드는 것 자체가
//     그 분류의 "구현"에 포함되는 작업입니다).
[CreateAssetMenu(fileName = "ItemDefinition", menuName = "MobArchive/Item Definition")]
public class ItemDefinition : ScriptableObject
{
    [Header("식별 (itemId는 프로젝트 전체에서 고유해야 합니다 - 중복 획득 방지의 기준)")]
    public string itemId;
    public string displayName;

    [Header("설명 (스토리/설정용 - 아이템 설명칸 하단에 항상 표시)")]
    [TextArea] public string description;

    [Header("2D 아이콘 (보물상자 위에 표시될 스프라이트)")]
    public Sprite icon;

    [Header("등장 가중치 (동아리방/보스방/상점 등 모든 풀에서 공통으로 사용)")]
    [Tooltip("같은 풀 안에서 뽑힐 가중치입니다. 값이 클수록 더 자주 등장합니다.")]
    public float weight = 1f;

    public enum ItemRarity { Tier0, Tier1, Tier2, Tier3 }

    [Header("등급 (weight와는 별개인 순수 분류 정보)")]
    [Tooltip("Tier 0~3의 4단계입니다. weight를 대체하지 않습니다 - weight는 위 " +
             "필드에 계속 직접 입력하고, 이 필드는 그 아이템이 어느 등급인지 표시만 해둡니다 " +
             "(같은 등급끼리도 weight를 다르게 줄 수 있습니다). 권장 weight 가이드: " +
             "Tier0(초급)=100, Tier1(일반)=70, Tier2(상급)=40, Tier3(최상급)=10 - " +
             "참고용이며 자동으로 반영되지는 않으니 필요하면 위 weight 필드에 직접 입력하세요.")]
    public ItemRarity rarity = ItemRarity.Tier1;

    public enum ItemCategory
    {
        Passive,    // 패시브 - 획득 즉시 반영구적으로 효과 적용 (지금까지의 유일한 동작, 기본값)
        Active,     // 액티브 - 코스트를 소모해 발휘. 1개만 소지 가능, 다른 액티브 획득 시 보물상자와 교체 (미구현)
        Trinket,    // 장신구 - 패시브와 유사한 상시 효과지만 1개만 소지 가능, 다른 장신구와 교체 (미구현)
        Disposable, // 일회용 - 사용/특정 조건 시 소모. 전용 가방 1칸 (미구현)
        Food,       // 음식 - 일회용과 사용 방식은 비슷하나 별도의 전용 가방 1칸을 따로 가짐 (미구현)
    }

    [Header("분류 (패시브/액티브/장신구/일회용/음식)")]
    [Tooltip("이 아이템의 분류입니다. 기본값 Passive는 지금까지의 동작과 동일합니다. " +
             "Active/Trinket/Disposable/Food는 아직 슬롯 제한/교체/소모/전용 UI가 구현되지 " +
             "않았습니다 - 그 분류를 실제로 만들 때까지는 이 값을 Passive가 아닌 값으로 " +
             "바꿔도 보물상자의 패시브 풀에는 등장하지 않습니다(ItemDatabase 참고).")]
    public ItemCategory category = ItemCategory.Passive;

    [Tooltip("액티브/장신구/일회용/음식 전용. 체크하면 이 아이템을 장착하고 있는 상태에서 같은 분류의 다른 아이템을 " +
             "보물상자에서 주울 때, 바로 교체하지 않고 \"아이템 슬롯을 교체하시겠습니까?\" 확인창을 먼저 띄웁니다 " +
             "(예: 20번처럼 교체되면 손해를 보는 아이템). 체크하지 않으면 지금처럼 바로 교체됩니다.")]
    public bool confirmBeforeReplace = false;

    [Header("상극 아이템 (같이 가질 수 없고, 서로 등장하지도 않는 아이템)")]
    [Tooltip("이 아이템과 같이 가질 수 없는 아이템의 Item Id를 적습니다(예: 99번에 \"69\"). 한쪽에만 적어도 양쪽 방향으로 적용됩니다. " +
             "가진 아이템이나 이번 레벨에 이미 등장한 아이템과 상극인 아이템은 보물상자/좌대/상점/48·56·80번 후보에서 빠지고, " +
             "주우려 해도 획득되지 않습니다(ItemCompatibility.cs 참고).")]
    public List<string> incompatibleItemIds = new List<string>();

    [Header("태그 (세트 효과용 - 자유 텍스트, 드롭다운 아님)")]
    [Tooltip("학생/학원/동아리 등 세트 효과에 쓸 태그를 직접 입력하세요. 지금은 검증/드롭다운이 " +
             "없으니 표기를 통일해서 적어주세요(예: 오타로 \"학샹\"처럼 적으면 다른 아이템의 " +
             "\"학생\" 태그와 매칭되지 않습니다). 여러 태그를 가질 수 있습니다.")]
    public List<string> tags = new List<string>();

    [Header("액티브 아이템 설정 (category가 Active일 때만 사용)")]
    [Tooltip("스페이스 바로 사용할 때 소모되는 코스트입니다. PlayerResources의 Cost가 이 값보다 " +
             "적으면 사용할 수 없습니다.")]
    public int activeCost = 1;
    [Tooltip("스페이스 바로 사용했을 때 실제로 일어나는 효과입니다. IActiveItemEffect를 구현한 " +
             "클래스를 골라 넣으세요(패시브의 IPassiveItemEffect와 같은 방식).")]
    [SerializeReference] public IActiveItemEffect activeEffect;

    [Header("장신구 설정 (category가 Trinket일 때만 사용)")]
    [Tooltip("장착하고 있는 동안 발휘되는 효과입니다. ITrinketEffect를 구현한 클래스를 골라 " +
             "넣으세요. 다른 장신구로 교체되면 Remove()가 호출되어 효과가 해제됩니다.")]
    [SerializeReference] public ITrinketEffect trinketEffect;

    [Header("일회용 아이템 설정 (category가 Disposable일 때만 사용)")]
    [Tooltip("Q로 사용했을 때 실제로 일어나는 효과입니다. IDisposableItemEffect를 구현한 " +
             "클래스를 골라 넣으세요. IsImmediate가 true인 즉시형은 사용 즉시 효과가 발동된 " +
             "뒤 바로 소멸하고, false인 지연형은 BeginPending()으로 대기 상태에 들어간 뒤 " +
             "실제 트리거가 발생했을 때 소멸합니다(대기 중 다른 일회용 아이템으로 교체되면 " +
             "CancelPending()이 호출되어 효과 없이 취소됩니다).")]
    [SerializeReference] public IDisposableItemEffect disposableEffect;

    [Header("음식 설정 (category가 Food일 때만 사용)")]
    [Tooltip("E로 사용했을 때 실제로 일어나는 효과입니다. IFoodEffect를 구현한 클래스를 골라 " +
             "넣으세요. 일회용과 사용 방식(즉시형/지연형)은 완전히 동일하고, 추가로 이 음식이 " +
             "슬롯에 새로 장착되는 순간(최초 장착 또는 교체로 새로 들어올 때) OnAcquired()가 " +
             "호출됩니다(예: 획득 시 최초 한정 스탯 증가).")]
    [SerializeReference] public IFoodEffect foodEffect;

    [Header("등장 위치 (카테고리는 드롭다운, variantId는 세부 구분이 필요할 때만)")]
    [Tooltip("이 아이템이 등장할 수 있는 방들의 목록입니다. 각 항목의 category는 드롭다운으로 " +
             "고르므로 오타가 날 수 없습니다. variantId는 비워두면 그 category 전체(예: Club)에서 " +
             "등장하고, 채우면(예: 카페='cafe', 편의점='convenience_store') 그 variantId를 가진 " +
             "방에서만 등장합니다 - 레벨 설정(RoomGenerationConfig)의 RoomVariant.variantId를 " +
             "그대로 복사해오세요. 여러 항목을 추가하면 여러 방에서 등장 가능해집니다. " +
             "예: 동아리방 전체 + 카페에서만 등장 -> [{category: Club}, {category: Store, variantId: \"cafe\"}]")]
    public List<AppearanceContext> appearanceContexts = new List<AppearanceContext>();

    [Header("스탯 변경 효과")]
    [Tooltip("이 아이템을 얻었을 때 적용될 스탯 가감/배율 목록입니다. 여러 개를 넣으면 " +
             "복합 효과(예: 공격력 증가 + 이동속도 감소 트레이드오프)가 됩니다.")]
    public List<StatModifier> statModifiers = new List<StatModifier>();

    [Header("특수 효과 (스탯이 아닌 능력 - 공격 방식 변경, 발사/소멸 시 반응 등)")]
    [Tooltip("드롭다운으로 특수 효과 종류를 고르세요. 여러 개를 등록하면 전부 함께 적용됩니다. " +
             "스탯형 효과는 여기가 아니라 위 statModifiers에 등록하세요.")]
    [SerializeReference] public List<IPassiveItemEffect> specialEffects = new List<IPassiveItemEffect>();

    [Header("특수 효과 설명 (UI 표시용 - 위 description과 완전히 별개)")]
    [Tooltip("특수 효과가 있는 아이템만 채우세요. 플레이어가 이해할 수 있는 문장으로, 이 " +
             "아이템이 실제로 뭘 하는지 설명합니다 (예: '발사 시 15% 확률로 탄환이 하나 더 " +
             "나갑니다'). description(스토리/설정용)과는 화면에서 완전히 별도 칸에 따로 " +
             "표시되며, 비워두면 그 칸 자체가 UI에 나타나지 않습니다.")]
    [TextArea] public string specialEffectDescription;

    // PlayerInventory가 이 아이템을 획득 처리할 때 호출합니다.
    // statModifiers는 StatModifierEffect 하나로 감싸고, specialEffects는 그대로 이어붙여
    // 하나의 리스트로 반환합니다.
    public List<IPassiveItemEffect> GetEffects()
    {
        var effects = new List<IPassiveItemEffect>();

        if (statModifiers != null && statModifiers.Count > 0)
        {
            effects.Add(new StatModifierEffect(statModifiers));
        }

        if (specialEffects != null)
        {
            foreach (var effect in specialEffects)
            {
                if (effect != null) effects.Add(effect);
            }
        }

        return effects;
    }
}
