using UnityEngine;

// 게임 상황(피격, 아이템 획득, 장전, 사망, 비전투 뛰기)에 맞춰 SD 캐릭터의 표정을 바꿔 주는 연결 컴포넌트입니다.
//
// [원리 - 기존 스크립트는 수정하지 않음]
// 기존 플레이어 스크립트들이 이미 공개해 둔 상태 값만 매 프레임 읽어서, 그 상태인 동안 표정을 유지(Hold)하고
// 상태가 끝나면 놓습니다(Release). 그래서 PlayerHealth / PlayerShooting / PlayerItemPickupPresenter는
// 전혀 바뀌지 않고, 이 컴포넌트를 떼면 표정 연결만 사라집니다.
//
//   표정          읽는 값                                       표정 유지 기간
//   피격          PlayerHealth.IsHitInvincible                 피격 직후 ~ 무적 시간 끝 (보호막으로 막은 무적은 제외)
//   아이템 획득   PlayerItemPickupPresenter.IsPresenting       획득 연출(애니메이션) 동안
//   장전          PlayerShooting.IsReloading                   장전 동안
//   사망          PlayerHealth.IsDead                          사망 이후 계속
//   뛰기          PlayerMovement.IsRunAnimating                뛰기 모션 동안 (최대 가속 + 걷는 중)
//
// 여러 상태가 겹치면 표정 세트의 우선순위(사망 > 피격 > 아이템 획득 > 장전 > 뛰기 > 기본)대로 보입니다.
// 표정 세트 에셋에 뛰기 칸이 없으면 기본 표정으로 보입니다(SDFaceSet_Reisa01에는 눈 4 / 입 1로 들어 있음).
// (PlayerHealth.IsInvincible은 "맞아서 생긴 무적 시간"에만 켜집니다. 테스트용 무적 모드에서는 켜지지 않습니다.)
//
// [에디터 설정]
// SD 플레이어 프리팹 루트(PlayerHealth 등이 붙은 오브젝트)에 Add Component로 붙이면 됩니다.
// 참조 칸은 비워두면 자동으로 찾습니다. SDFaceController가 없는 캐릭터(레이사 등)에서는 아무 일도 하지 않습니다.
[DisallowMultipleComponent]
public class SDFaceGameEvents : MonoBehaviour
{
    [Header("참조 (비워두면 자동으로 찾습니다)")]
    public SDFaceController face;
    public PlayerHealth playerHealth;
    public PlayerShooting shooting;
    public PlayerItemPickupPresenter pickupPresenter;
    public PlayerMovement movement;

    [Header("연결할 표정 (끄면 그 상황에서 표정이 바뀌지 않습니다)")]
    public bool useHit = true;
    public bool useItemPickup = true;
    public bool useReload = true;
    public bool useDeath = true;
    public bool useRun = true;

    private bool hitHeld, pickupHeld, reloadHeld, deathHeld, runHeld;

    void Awake()
    {
        if (face == null) face = GetComponentInChildren<SDFaceController>(true);
        if (playerHealth == null) playerHealth = GetComponentInParent<PlayerHealth>();
        if (shooting == null) shooting = GetComponentInParent<PlayerShooting>();
        if (pickupPresenter == null) pickupPresenter = GetComponentInParent<PlayerItemPickupPresenter>();
        if (movement == null) movement = GetComponentInParent<PlayerMovement>();
    }

    void Update()
    {
        if (face == null) return;

        bool dead = playerHealth != null && playerHealth.IsDead;
        Sync(SDFaceExpression.Death, useDeath && dead, ref deathHeld);
        // [보호막] 보호막이 막아서 생긴 무적 시간에는 피격 표정을 짓지 않도록 IsHitInvincible을 봅니다(형빈 확정).
        Sync(SDFaceExpression.Hit, useHit && playerHealth != null && playerHealth.IsHitInvincible, ref hitHeld);

        // 사망 처리(PlayerDeathHandler)가 다른 컴포넌트를 끄면 그 값이 마지막 상태로 남을 수 있으므로,
        // 꺼진 컴포넌트의 상태는 "아님"으로 봅니다(사망 표정이 가장 우선이라 화면에는 영향이 없음).
        bool pickingUp = pickupPresenter != null && pickupPresenter.isActiveAndEnabled && pickupPresenter.IsPresenting;
        Sync(SDFaceExpression.ItemPickup, useItemPickup && pickingUp, ref pickupHeld);

        bool reloading = shooting != null && shooting.isActiveAndEnabled && shooting.IsReloading;
        Sync(SDFaceExpression.Reload, useReload && reloading, ref reloadHeld);

        bool running = movement != null && movement.isActiveAndEnabled && movement.IsRunAnimating;
        Sync(SDFaceExpression.Run, useRun && running, ref runHeld);
    }

    void OnDisable()
    {
        if (face == null) return;
        // 이 컴포넌트가 꺼지면 걸어둔 표정을 모두 놓습니다(표정이 걸린 채 남지 않도록).
        Sync(SDFaceExpression.Death, false, ref deathHeld);
        Sync(SDFaceExpression.Hit, false, ref hitHeld);
        Sync(SDFaceExpression.ItemPickup, false, ref pickupHeld);
        Sync(SDFaceExpression.Reload, false, ref reloadHeld);
        Sync(SDFaceExpression.Run, false, ref runHeld);
    }

    // 상태가 바뀐 순간에만 Hold / Release를 부릅니다.
    private void Sync(SDFaceExpression expression, bool active, ref bool held)
    {
        if (active == held) return;
        held = active;
        if (active) face.Hold(expression);
        else face.Release(expression);
    }
}
