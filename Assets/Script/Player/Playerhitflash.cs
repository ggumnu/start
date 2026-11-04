using UnityEngine;
using System.Collections.Generic;

// 플레이어가 무적 시간(PlayerHealth.IsInvincible) 동안 흰색으로 깜빡이며 점멸하는
// 연출을 담당합니다. PlayerHealth.cs의 무적 판정 로직은 전혀 건드리지 않고, 공개된
// IsInvincible 프로퍼티만 매 프레임 읽어서 렌더러를 껐다 켰다 합니다.
//
// [EnemyHealth.cs와 다른 점 - 왜 온/오프 방식을 쓰는지]
// EnemyHealth의 피격 색상 피드백은 머티리얼 색상을 빨갛게 바꿨다가 서서히 원래색으로
// 페이드하는 "한 번짜리 색상 전환" 방식입니다. 플레이어는 "지금 무적 상태다"를 무적이
// 끝날 때까지 계속 알려줘야 하는 상태 표시라서, 실제로 깜빡깜빡 점멸시키는 편이 훨씬
// 직관적입니다. 그래서 이 스크립트는 일정 간격으로 Renderer.enabled를 껐다 켰다
// 반복합니다(꺼짐 = 안 보임). 실제 알파 블렌딩(반투명) 대신 이 방식을 쓴 이유는,
// 캐릭터 머티리얼이 원래 불투명(Opaque)으로 만들어져 있을 가능성이 높아서 반투명으로
// 바꾸면 렌더링 문제가 생길 위험이 있기 때문입니다 - Renderer.enabled 토글은
// 셰이더/머티리얼 설정과 완전히 무관하게 항상 확실히 동작합니다.
//
// [여러 개의 Renderer - 모델이 여러 메쉬로 쪼개진 구조 대비]
// EnemyHealth.cs 주석에 남아있는 것처럼, 캐릭터 모델은 자식 오브젝트에 여러 개의
// Renderer(몸통/머리카락/무기 등)로 나뉘어 있을 수 있습니다. GetComponentInChildren
// (단수형) 대신 GetComponentsInChildren(복수형)로 전부 모아서, 캐릭터 전체가 함께
// 깜빡이도록 합니다.
//
// [참고 - 항상 숨겨둔 렌더러가 있는 경우]
// GetComponentsInChildren<Renderer>()는 기본값(includeInactive: false)으로 호출됩니다.
// 즉, 처음부터 비활성화(enabled = false)해둔 렌더러가 있다면 수집 대상에서 제외되어
// 무적 연출과 무관하게 그대로 안 보이는 상태를 유지합니다(의도적인 선택입니다). 다만
// 만약 그런 "항상 숨김" 렌더러가 활성 상태의 GameObject 위에 있는데 enabled만 꺼둔
// 경우라면 이 스크립트가 수집해버릴 수 있으니, 그런 구조가 있다면 알려주세요.
[RequireComponent(typeof(PlayerHealth))]
public class PlayerHitFlash : MonoBehaviour
{
    [Header("참조 (비워두면 자동으로 찾습니다)")]
    public PlayerHealth playerHealth;

    [Header("깜빡임 설정")]
    [Tooltip("보임 <-> 안 보임을 전환하는 간격 (초). 값이 작을수록 더 빠르게 깜빡입니다.")]
    public float blinkInterval = 0.08f;

    [Header("색상 틴트 (선택 - 셰이더가 _Color를 지원하는 렌더러에만 적용됩니다)")]
    [Tooltip("켜져서 보이는 동안 이 색으로 틴트합니다. 기본값은 흰색입니다.")]
    public bool tintWhileVisible = true;
    public Color tintColor = Color.white;

    private class RendererEntry
    {
        public Renderer renderer;
        public bool supportsColor;
        public Color originalColor;
    }

    private readonly List<RendererEntry> entries = new List<RendererEntry>();

    private float blinkTimer = 0f;
    private bool isVisible = true;

    // [실시간 초상화] 초상화를 그리는 동안만 잠깐 다시 켠 렌더러들(그린 직후 다시 끔)
    private readonly List<Renderer> portraitRestored = new List<Renderer>();
    private bool wasInvincible = false;

    void Awake()
    {
        if (playerHealth == null) playerHealth = GetComponent<PlayerHealth>();

        // 같은 Player 오브젝트에 AimTrajectoryIndicator(조준선)가 붙어있다면, 그 조준선
        // 렌더러는 캐릭터의 "몸"이 아니라 별도 표시 요소이므로 점멸/틴트 대상에서 제외합니다.
        // (AimTrajectoryIndicator가 없는 경우 excludedRenderers는 null이 되어 아무것도 제외되지 않습니다.)
        var aimIndicator = GetComponent<AimTrajectoryIndicator>();
        // 19번 패시브(양갈래 발사)로 조준선이 두 개로 늘어난 경우까지 전부 제외하기 위해
        // 단일 참조 대신 배열을 받습니다 - AimTrajectoryIndicator.cs 쪽 변경 참고.
        Renderer[] excludedRenderers = aimIndicator != null ? aimIndicator.LineRenderers : null;

        foreach (var rend in GetComponentsInChildren<Renderer>())
        {
            if (IsExcluded(rend, excludedRenderers)) continue;

            var entry = new RendererEntry { renderer = rend };
            entry.supportsColor = rend.material.HasProperty("_Color");
            if (entry.supportsColor)
            {
                entry.originalColor = rend.material.color;
            }
            entries.Add(entry);
        }

        if (entries.Count == 0)
        {
            Debug.LogWarning("PlayerHitFlash: 캐릭터에서 Renderer를 하나도 찾지 못해 점멸 효과를 표시할 수 없습니다.");
        }
    }

    void Update()
    {
        if (playerHealth == null) return;

        // [보호막] 보호막이 막아서 생긴 무적 시간에는 점멸하지 않도록, 체력이 깎여서 생긴 무적(IsHitInvincible)만 봅니다(형빈 확정).
        bool isInvincible = playerHealth.IsHitInvincible;

        if (isInvincible)
        {
            blinkTimer -= Time.deltaTime;
            if (blinkTimer <= 0f)
            {
                blinkTimer = blinkInterval;
                isVisible = !isVisible;
                ApplyVisibility(isVisible);
            }
        }
        else if (wasInvincible)
        {
            // 무적이 막 끝났으면 즉시 원상 복구합니다 (깜빡이다가 안 보이는 상태로 멈춰있는 것 방지).
            ResetToNormal();
        }

        wasInvincible = isInvincible;
    }

    // excluded 배열(조준선 렌더러들) 안에 rend가 들어있는지 확인합니다. excluded가
    // null이면(AimTrajectoryIndicator가 없는 캐릭터) 아무것도 제외하지 않습니다.
    private static bool IsExcluded(Renderer rend, Renderer[] excluded)
    {
        if (excluded == null) return false;
        for (int i = 0; i < excluded.Length; i++)
        {
            if (excluded[i] == rend) return true;
        }
        return false;
    }

    private void ApplyVisibility(bool visible)
    {
        foreach (var entry in entries)
        {
            if (entry.renderer == null) continue;
            entry.renderer.enabled = visible;
            if (visible && tintWhileVisible && entry.supportsColor)
            {
                entry.renderer.material.color = tintColor;
            }
        }
    }

    // [실시간 초상화 - 형빈 확정] 초상화(PlayerPortraitCamera)에서는 점멸 없이 피격 표정만 보이도록, 초상화 카메라가 그리기 직전에
    // 점멸 때문에 꺼져 있는 렌더러를 잠깐 켜고(ShowNormalForPortrait), 그린 직후 다시 끕니다(RestoreAfterPortrait). 메인 화면의 점멸은
    // 그대로입니다. 점멸이 꺼 둔 렌더러만 건드리고, 다른 이유로 꺼진 것은 건드리지 않습니다.
    public void ShowNormalForPortrait()
    {
        portraitRestored.Clear();
        if (isVisible) return; // 지금 점멸로 숨겨진 순간이 아니면 할 일 없음
        foreach (var entry in entries)
        {
            if (entry.renderer == null || entry.renderer.enabled) continue;
            entry.renderer.enabled = true;
            portraitRestored.Add(entry.renderer);
        }
    }

    public void RestoreAfterPortrait()
    {
        foreach (var rend in portraitRestored)
        {
            if (rend != null) rend.enabled = false;
        }
        portraitRestored.Clear();
    }

    private void ResetToNormal()
    {
        blinkTimer = 0f;
        isVisible = true;
        foreach (var entry in entries)
        {
            if (entry.renderer == null) continue;
            entry.renderer.enabled = true;
            if (entry.supportsColor)
            {
                entry.renderer.material.color = entry.originalColor;
            }
        }
    }
}