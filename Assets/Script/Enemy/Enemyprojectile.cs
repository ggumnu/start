using System;
using System.Collections.Generic;
using UnityEngine;

// 적이 발사한 탄환(직선 이동, 사거리 소멸, 플레이어/아군/벽 명중)입니다. 플레이어 총알 Projectile.cs의 대칭 버전으로, 둘을 독립적으로
// 확장하려고 일부러 분리해 둠. 탄환 오브젝트는 EnemyRangedAttack/EnemyRadialAttack이 구체 프리미티브로 만들어 이 컴포넌트를 붙입니다.
// 살아 있는 탄환 목록(Active)은 PlayerHackTool이 범위 안 탄환을 지울 때 씁니다(물리 검사 대신 목록 - 레이어/트리거 설정과 무관).
[RequireComponent(typeof(Rigidbody))]
public class EnemyProjectile : MonoBehaviour
{
    // [VFX 연동] 플레이어 피격 알림은 실제로 피해가 들어갈 때만(무적/보호막/돌진 회피 제외) 발생합니다.
    public static event Action<Vector3, Vector3> OnAnyEnemyBulletHitPlayer; // (충돌 위치, 총알 진행 방향)
    public static event Action<Vector3, Vector3> OnAnyEnemyBulletHitWall;   // (충돌 위치, 벽 표면 방향)

    // 이 탄환 하나가 플레이어에게 실제로 피해를 줄 때(위 정적 알림과 같은 조건) 알립니다 - 탄환별 연출용(EnemyRadialAttack 피격 굴절).
    public event Action<PlayerHealth> OnHitPlayerDamaging;

    private float damage;
    private float maxRange;
    private float speed;
    private Vector3 direction;
    private Vector3 spawnPosition;
    private Vector3 lastStepStart; // 벽 표면 방향 계산용(Projectile.cs와 동일한 방식)

    private Rigidbody rb;

    private static readonly List<EnemyProjectile> active = new List<EnemyProjectile>();

    // 지금 날아다니는 적 총알 목록(읽기 전용).
    public static IReadOnlyList<EnemyProjectile> Active => active;

    void OnEnable()
    {
        if (!active.Contains(this)) active.Add(this);
    }

    void OnDisable()
    {
        active.Remove(this);
    }

    // 중심(center, 보통 플레이어 발 위치)에서 위로 height만큼 올라간 반경 radius 원기둥 안의 적 총알을 전부
    // 없애고, 없앤 개수를 돌려줍니다. clearedPositions를 넘기면 없앤 총알의 위치를 담아 줍니다(이펙트용).
    // 판정은 PlayerHackTool의 적 판정(OverlapCapsule)과 같은 범위가 되도록, 바닥 평면 거리 <= radius이고
    // 높이는 (center.y - radius) ~ (center.y + height + radius) 안이면 포함합니다.
    public static int ClearInRadius(Vector3 center, float radius, float height, List<Vector3> clearedPositions = null)
    {
        if (radius <= 0f || active.Count == 0) return 0;

        float sqrRadius = radius * radius;
        float minY = center.y - radius;
        float maxY = center.y + Mathf.Max(height, 0f) + radius;

        // 없애는 도중 OnDisable이 목록을 바꾸므로 복사본을 훑습니다.
        var snapshot = active.ToArray();
        int count = 0;
        foreach (var bullet in snapshot)
        {
            if (bullet == null) continue;

            Vector3 p = bullet.transform.position;
            float dx = p.x - center.x;
            float dz = p.z - center.z;
            if (dx * dx + dz * dz > sqrRadius) continue;
            if (p.y < minY || p.y > maxY) continue;

            clearedPositions?.Add(p);
            // 바로 꺼서 이번 프레임에 플레이어를 맞히는 일이 없게 한 뒤 파괴합니다.
            bullet.gameObject.SetActive(false);
            Destroy(bullet.gameObject);
            count++;
        }
        return count;
    }

    // [87번 포섭] 아군이 쏜 탄환인지입니다(EnemyRangedAttack이 발사 직후 SetFromAlly로 표시). 아군 탄환은 적에게만 맞습니다.
    private bool fromAlly;
    public bool FromAlly => fromAlly;
    public void SetFromAlly(bool value) { fromAlly = value; }

    // [플레이 통계 - 사망 원인] 이 탄환을 쏜 적입니다. EnemyRangedAttack/EnemyRadialAttack이 발사 직후 넣습니다(안 넣으면 "기타").
    private PlayerDamageSource damageSource = PlayerDamageSource.Unknown;
    public void SetDamageSource(PlayerDamageSource source) { damageSource = source; }

    void Awake()
    {
        rb = GetComponent<Rigidbody>();
        rb.useGravity = false;
        rb.isKinematic = true;
    }

    public void Launch(Vector3 fireDirection, float bulletDamage, float bulletRange, float bulletSpeed, float bulletSize)
    {
        GameAudio.Play(SfxId.EnemyShot); // [사운드] 적 사격(전방위/샷건처럼 한꺼번에 여러 발이면 한 번 - 겹침 방지 간격)
        direction = fireDirection.normalized;
        damage = bulletDamage;
        maxRange = bulletRange;
        speed = bulletSpeed;
        spawnPosition = transform.position;

        transform.localScale = Vector3.one * bulletSize;
        transform.rotation = Quaternion.LookRotation(direction);

        // [탄환 꼬리] 위치/크기/색이 정해진 뒤 별똥별 꼬리를 붙입니다(연사/전방위/보스 탄환 공통 - 전부 이 Launch를 거침).
        // 꼬리 색은 탄환 재질 색을 자동으로 따라가고, 탄환이 사라지면 꼬리는 스스로 사라집니다(BulletTrail.cs 참고).
        BulletTrail.Attach(gameObject, BulletTrailKind.Bullet);
        // [방 전환] 플레이어가 다른 방에 들어가면 이 방에서 쏜 적 탄환을 꼬리째 바로 지웁니다(RoomScope - 클리어 여부와 무관).
        RoomScope.DestroyWhenPlayerLeaves(gameObject);
    }

    void FixedUpdate()
    {
        lastStepStart = transform.position;

        Vector3 moveStep = direction * speed * Time.fixedDeltaTime;
        rb.MovePosition(rb.position + moveStep);

        float traveled = Vector3.Distance(spawnPosition, transform.position);
        if (traveled >= maxRange)
        {
            Destroy(gameObject);
        }
    }

    // [한 발 = 한 번] 적 몸통 콜라이더와 자식 접촉 트리거(MeleeHitbox)처럼 콜라이더가 둘인 대상에 같은 순간 닿으면, Destroy가 프레임 끝에
    // 처리되기 전에 OnTriggerEnter가 두 번 불려 피해가 두 번 들어갔습니다(87번 아군 탄환 15+15). 첫 명중에서 표시해 두고 이후는 무시합니다.
    private bool consumed;

    private void Consume()
    {
        consumed = true;
        Destroy(gameObject);
    }

    void OnTriggerEnter(Collider other)
    {
        if (consumed) return;

        // [87번 포섭] 아군 탄환: 적(아군이 아닌 적)에게만 맞고, 플레이어와 다른 아군은 그대로 통과합니다. 피해는 이미 x30 환산된 값입니다.
        // 플레이어 총알처럼 간격 없이 맞을 때마다 들어갑니다(형빈 확정). 피해 숫자는 항상 Weak(아이템으로 생긴 피해 - 형빈 확정).
        if (fromAlly)
        {
            if (other.CompareTag("Player")) return;
            var victim = other.GetComponentInParent<EnemyHealth>();
            if (victim != null)
            {
                if (victim.IsDead || AllyFaction.IsAlly(victim.gameObject)) return;
                victim.TakeWeakDamage(damage);
                Consume();
                return;
            }
            if (other.CompareTag("Wall"))
            {
                OnAnyEnemyBulletHitWall?.Invoke(transform.position, ComputeWallNormal());
                Consume();
            }
            return;
        }

        if (other.CompareTag("Enemy")) return; // 다른 적은 그대로 통과 (적끼리는 서로 맞지 않습니다)

        if (other.CompareTag("Player"))
        {
            // [74번 돌진 회피] 플레이어가 돌진하는 동안은 맞지 않고 그대로 통과해 계속 날아갑니다(소멸 X, 피해 X - 형빈 확정).
            if (PlayerDash.IsDodging) return;

            PlayerHealth playerHealth = other.GetComponent<PlayerHealth>();
            if (playerHealth != null)
            {
                // 무적 시간/보호막이 막을 공격이면 피격 연출을 띄우지 않습니다(PlayerHealth.TakeDamage가 실제로 피해를 넣는 조건과 같음).
                bool willDamage = !playerHealth.IsDead && !playerHealth.IsInvincible && !playerHealth.WillShieldBlockNextHit;
                if (willDamage)
                {
                    OnAnyEnemyBulletHitPlayer?.Invoke(transform.position, direction);
                    try
                    {
                        OnHitPlayerDamaging?.Invoke(playerHealth);
                    }
                    catch (Exception e)
                    {
                        // 순수 연출이라 오류가 나도 피해 처리는 그대로 진행합니다.
                        Debug.LogError($"EnemyProjectile: 탄환 피격 연출 중 오류가 발생해 건너뜁니다: {e}");
                    }
                }
                playerHealth.TakeDamage(damage, damageSource);
            }
            Consume();
            return;
        }

        // [87번 포섭] 적 탄환은 아군에게도 맞습니다(원래 피해 그대로 - 아군의 환산 체력에서 깎이고, 맞은 뒤 1초 무적).
        var hitAlly = other.GetComponentInParent<AllyFaction>();
        if (hitAlly != null)
        {
            var allyHealth = hitAlly.Health;
            if (allyHealth != null && !allyHealth.IsDead) allyHealth.TakeDamage(damage);
            Consume();
            return;
        }

        if (other.CompareTag("Wall"))
        {
            OnAnyEnemyBulletHitWall?.Invoke(transform.position, ComputeWallNormal());
            Consume();
        }
    }

    // 직전 위치에서 진행 방향으로 레이캐스트해 벽 표면 법선을 구합니다(Projectile.ComputeWallNormal()과 같은 방식).
    private Vector3 ComputeWallNormal()
    {
        float rayDistance = Vector3.Distance(lastStepStart, transform.position) + 0.1f;

        if (Physics.Raycast(lastStepStart, direction, out RaycastHit hit, rayDistance))
        {
            return hit.normal;
        }

        return -direction;
    }
}