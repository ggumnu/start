using System.Collections.Generic;
using UnityEngine;

// [투명 상태 - 112번 액티브]
// 플레이어가 투명한 동안의 상태와 연출을 관리합니다. 112번 효과가 Begin()으로 시작하면 이 컴포넌트가 플레이어에 자동으로 붙습니다
// (프리팹에 직접 붙일 필요 없음).
//
// [투명한 동안 - 형빈 확정]
// - 적(보스 포함)이 플레이어를 인식하지 못합니다: 근접/원거리/보스 AI가 PlayerInvisibility.IsActive를 보고 추적을 멈추고 대기합니다.
//   돌진 준비/전방위 충전/쫓아가며 도는 공격은 취소되고, 이미 진행 중인 돌진은 그 돌진까지만, 이미 시작한 연사는 끝까지 쏩니다.
//   적이 피해를 입어도 인식하지 못합니다(원래 피격으로 인식하는 처리가 없음).
// - 적과 닿아도 접촉 피해(돌진 포함)를 받지 않습니다(EnemyMeleeContactTrigger). 적 탄환/수류탄 자폭 피해는 그대로 받습니다.
// - 체력 소모 특수방 출입 피해를 받지 않습니다(SpecialRoomHealthDamageOverride.Block).
// - 캐릭터가 점 무늬(디더)로 반투명하게 보입니다. Toon_Character_SD / Toon_Face_SD 셰이더의 _DitherFade 값을 MaterialPropertyBlock으로
//   넣습니다. 이 값이 없는 셰이더를 쓰는 렌더러(다른 셰이더의 무기 모델/이펙트/조준선 등)는 건드리지 않습니다.
// - 시간이 끝나면(또는 플레이어가 죽으면) 모두 원래대로 돌아가고, 적은 평소 규칙(탐지 범위 + 시야)으로 다시 플레이어를 찾습니다.
// - 시간은 게임 시간 기준이라 일시정지 중에는 줄지 않습니다. 방/레벨을 옮겨도 이어집니다.
public class PlayerInvisibility : MonoBehaviour
{
    private const string HazardBlockId = "player_invisibility_112";
    private static readonly int DitherFadeId = Shader.PropertyToID("_DitherFade");

    private static PlayerInvisibility current;

    // 지금 플레이어가 투명한지입니다(적 AI/접촉 판정 등이 매 프레임 물어봄).
    public static bool IsActive => current != null && current.remaining > 0f;

    // 남은 투명 시간 비율(1 = 막 시작, 0 = 끝/투명 아님)입니다. 액티브 아이콘 쿨타임 연출용.
    public static float Remaining01 => IsActive && current.duration > 0f ? Mathf.Clamp01(current.remaining / current.duration) : 0f;

    private float duration;
    private float remaining;
    private float visibleFraction = 0.4f;
    private PlayerHealth health;
    private MaterialPropertyBlock block;
    private readonly List<Renderer> rendererBuffer = new List<Renderer>();
    private readonly List<Renderer> fadedRenderers = new List<Renderer>();

    // player를 seconds초 동안 투명하게 만듭니다. visible = 투명한 동안 보이는 정도(0~1, 0.4 = 픽셀의 40%만 보임).
    public static bool Begin(GameObject player, float seconds, float visible)
    {
        if (player == null || seconds <= 0f) return false;

        var invisibility = player.GetComponent<PlayerInvisibility>();
        if (invisibility == null) invisibility = player.AddComponent<PlayerInvisibility>();
        invisibility.StartInvisibility(seconds, visible);
        return true;
    }

    private void StartInvisibility(float seconds, float visible)
    {
        duration = seconds;
        remaining = seconds;
        visibleFraction = Mathf.Clamp01(visible);
        if (health == null) health = GetComponent<PlayerHealth>();
        current = this;

        SpecialRoomHealthDamageOverride.Block(HazardBlockId, gameObject);
        ApplyFade(visibleFraction);
    }

    void Update()
    {
        if (remaining <= 0f) return;

        remaining -= Time.deltaTime;
        if (health != null && health.IsDead) remaining = 0f;

        if (remaining <= 0f)
        {
            EndInvisibility();
            return;
        }

        // 파츠 교체 등으로 렌더러가 바뀌거나 다른 스크립트가 값을 덮어써도 유지되도록 매 프레임 다시 넣습니다(렌더러 수가 적어 가벼움).
        ApplyFade(visibleFraction);
    }

    private void EndInvisibility()
    {
        remaining = 0f;
        SpecialRoomHealthDamageOverride.Unblock(HazardBlockId);
        RestoreFade();
        if (current == this) current = null;
    }

    void OnDisable()
    {
        if (remaining > 0f) EndInvisibility();
    }

    void OnDestroy()
    {
        if (remaining > 0f) EndInvisibility();
        if (current == this) current = null;
    }

    // 이 오브젝트 아래에서 _DitherFade를 가진 재질을 쓰는 렌더러 전부에 값을 넣습니다(꺼진 파츠 포함).
    private void ApplyFade(float value)
    {
        if (block == null) block = new MaterialPropertyBlock();

        rendererBuffer.Clear();
        GetComponentsInChildren(true, rendererBuffer);
        for (int i = 0; i < rendererBuffer.Count; i++)
        {
            Renderer r = rendererBuffer[i];
            if (r == null || !UsesDitherFade(r)) continue;

            // 다른 값(피부색 등)이 이미 들어 있을 수 있으므로 읽어온 뒤 이 값만 바꿔서 다시 넣습니다(SDCharacterSkin과 같은 방식).
            r.GetPropertyBlock(block);
            block.SetFloat(DitherFadeId, value);
            r.SetPropertyBlock(block);
            block.Clear();

            if (!fadedRenderers.Contains(r)) fadedRenderers.Add(r);
        }
    }

    // 값을 넣었던 렌더러를 평소(1)로 되돌립니다.
    private void RestoreFade()
    {
        if (block == null) block = new MaterialPropertyBlock();
        for (int i = 0; i < fadedRenderers.Count; i++)
        {
            Renderer r = fadedRenderers[i];
            if (r == null) continue;
            r.GetPropertyBlock(block);
            block.SetFloat(DitherFadeId, 1f);
            r.SetPropertyBlock(block);
            block.Clear();
        }
        fadedRenderers.Clear();
    }

    private static bool UsesDitherFade(Renderer r)
    {
        if (r is ParticleSystemRenderer || r is TrailRenderer || r is LineRenderer) return false;
        Material[] mats = r.sharedMaterials;
        for (int i = 0; i < mats.Length; i++)
        {
            if (mats[i] != null && mats[i].HasProperty(DitherFadeId)) return true;
        }
        return false;
    }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetOnPlayStart()
    {
        current = null;
    }
}
