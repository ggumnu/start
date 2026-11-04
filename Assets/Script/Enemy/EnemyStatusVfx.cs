using System.Collections.Generic;
using UnityEngine;

// [제어 지속 이펙트 - 걸려 있는 동안 적에게 붙어 다니는 이펙트]
// 해킹/수면/매료처럼 적에게 걸리는 제어가 "지속되는 동안" 그 적에게 이펙트 프리팹을 붙여두고,
// 제어가 끝나면 끄는 컴포넌트입니다. 프리팹에 미리 붙여둘 필요 없이, 제어를 거는 쪽(지금은
// PlayerHackTool)이 Attach()를 호출하는 순간 그 적에게 자동으로 붙습니다(EnemyControlState와 같은 패턴).
//
// [이펙트 모양은 프리팹에서 직접 조정]
// 색/크기/분사율 같은 이펙트 자체의 모양은 인스펙터 값으로 바꾸지 않고, 원본 이펙트 프리팹을 복제해서
// 그 복제본을 직접 수정하는 방식입니다(형빈 확정). 그래서 이 컴포넌트는 프리팹을 "그대로" 생성하기만
// 하고, 위치(어디에 붙일지)와 꺼질 때 사라지는 시간만 다룹니다. 프리팹 최상위의 회전/크기(Scale)도
// 프리팹에 정해둔 값 그대로 쓰입니다.
//
// [언제 꺼지는가 - EnemyControlState를 읽기만 합니다]
// 매 프레임 EnemyControlState에 "이 제어가 아직 걸려 있는가"만 물어봅니다(EnemyControlState는 전혀
// 수정하지 않음). 그래서 다음 경우 모두 자동으로 꺼집니다.
// - 제어 시간이 끝났을 때
// - 다른 강력 제어로 바뀌었을 때(예: 해킹 중 수면이 걸림)
// - 적이 죽었을 때
// - 적이 사라지는 연출(DissolveAndVanish가 이 컴포넌트를 끔)이 시작되거나 적 오브젝트가 없어질 때
// 같은 제어가 다시 걸리면(해킹 중 다시 해킹) 이펙트를 새로 만들지 않고 그대로 이어갑니다.
//
// [꺼질 때 남은 입자 처리 - 설정마다 고름]
// - 기본(체크 꺼짐): 새 입자만 멈추고, 이미 나온 입자는 fadeOutTime 동안 남은 수명대로 재생되다 지워집니다
//   (해킹 지속 이펙트가 이 방식).
// - "끝날 때 남은 입자를 투명하게 사라지게" 체크: 새 입자를 멈추는 동시에, 이미 나온 입자 전부가
//   fadeOutTime 동안 함께 투명해지면서 사라집니다(31번 수면 방울 - 형빈 확정). 실제 처리는
//   StatusVfxParticleFader가 이펙트 오브젝트에 붙어서 합니다(적이 사라지는 연출로 이 컴포넌트가 꺼져도
//   끝까지 진행되도록).
//
// [제어가 아닌 상태 이상 - AttachCustom (91번 오한 추가)]
// 오한처럼 제어(EnemyControlState의 제어 칸)가 아닌 상태 이상의 이펙트는 AttachCustom(적, 이름, 설정, 켜져 있어야 하는지 묻는 함수)으로
// 붙입니다. 매 프레임 그 함수가 false를 돌려주면 위와 똑같이 꺼집니다. 다시 켜야 할 때는 거는 쪽이 AttachCustom을 다시 부르면 되고,
// 이미 켜져 있으면 그대로 이어갑니다(매 프레임 불러도 됨). 기존 제어 이펙트(Attach)의 동작은 바뀌지 않았습니다.
//
// [이펙트는 적의 자식으로 붙이지 않습니다]
// 적의 자식으로 붙이면 적의 회전/크기를 따라가 버리므로, 따로 생성한 뒤 매 프레임 위치만 따라가게
// 합니다. 그래서 적이 돌아도 이펙트 방향은 프리팹에 정해둔 그대로 유지됩니다.

// 제어 하나에 대한 지속 이펙트 설정 묶음입니다. 해킹(PlayerHackTool)에서 쓰고, 나중에 수면/매료에도
// 같은 칸 구성으로 그대로 쓸 수 있습니다.
[System.Serializable]
public class EnemyStatusVfxSettings
{
    public enum AnchorPoint
    {
        [InspectorName("발밑 (적 오브젝트 위치)")] Pivot,
        [InspectorName("몸통 중심")] BodyCenter,
        [InspectorName("머리 위 (몸통 맨 위)")] AboveHead,
    }

    [Tooltip("제어가 걸려 있는 동안 적에게 붙여둘 이펙트 프리팹입니다. 원본 이펙트를 복제해서 수정한 프리팹을 " +
             "넣으세요. 비워두면 지속 이펙트 없이 기존과 똑같이 동작합니다. 프리팹 안의 파티클은 Looping이 " +
             "켜져 있어야 제어가 끝날 때까지 계속 재생됩니다.")]
    public GameObject prefab;

    [Tooltip("이펙트를 붙일 기준 위치입니다. \"머리 위\"는 적 몸체(렌더러)의 맨 위 기준이라 보스처럼 큰 적에게도 " +
             "자동으로 맞춰집니다.")]
    public AnchorPoint anchor = AnchorPoint.AboveHead;

    [Tooltip("기준 위치에서 이펙트를 옮기는 값입니다(월드 기준, m). 예: (0, 0.3, 0)이면 0.3m 위.")]
    public Vector3 positionOffset = Vector3.zero;

    [Tooltip("제어가 끝났을 때 이펙트가 사라지는 시간(초)입니다. 이 시간 동안 새 입자는 나오지 않습니다. 아래 " +
             "\"끝날 때 남은 입자를 투명하게\"가 꺼져 있으면 이미 나온 입자가 이 시간 동안 남은 수명대로 재생되다 " +
             "지워지고, 켜져 있으면 이미 나온 입자 전부가 이 시간 동안 함께 투명해지며 사라집니다. 0이면 즉시 지웁니다.")]
    [Min(0f)] public float fadeOutTime = 0.3f;

    [Tooltip("켜면 제어가 끝나는 순간(시간 종료/다른 제어로 교체/사망/사라지는 연출) 이미 떠 있는 입자까지 전부 위의 " +
             "Fade Out Time 동안 함께 투명해지며 사라집니다. 끄면 이미 나온 입자는 남은 수명대로 끝까지 재생됩니다.")]
    public bool fadeOutRemainingParticles = false;
}

[DisallowMultipleComponent]
public class EnemyStatusVfx : MonoBehaviour
{
    private class ActiveEntry
    {
        public EnemyControlType type;
        public string customKey;                 // [AttachCustom] 제어가 아닌 상태 이상의 이름(제어 이펙트면 null)
        public System.Func<bool> customIsActive; // [AttachCustom] 아직 켜져 있어야 하는지
        public EnemyStatusVfxSettings settings;
        public GameObject prefab;
        public GameObject instance;
    }

    private readonly List<ActiveEntry> entries = new List<ActiveEntry>();
    private EnemyControlState controlState;
    private EnemyHealth health;

    // 제어를 건 쪽이 "이 적에게 이 제어가 실제로 걸렸다"고 알릴 때 호출합니다(제어가 실제로 걸린 경우에만
    // 호출하세요 - 예: TryApplyStrong()이 true를 돌려준 뒤). 설정이 비어 있으면 아무 일도 하지 않습니다.
    public static void Attach(GameObject enemy, EnemyControlType type, EnemyStatusVfxSettings settings)
    {
        if (enemy == null || settings == null || settings.prefab == null) return;
        if (type == EnemyControlType.None) return;

        var vfx = enemy.GetComponent<EnemyStatusVfx>();
        if (vfx == null) vfx = enemy.AddComponent<EnemyStatusVfx>();
        vfx.AttachInternal(type, settings);
    }

    // [91번 오한 등] 제어가 아닌 상태 이상의 이펙트를 붙입니다. isActive가 false를 돌려주면 꺼집니다. 이미 같은 이름으로 켜져 있으면 그대로
    // 이어갑니다(매 프레임 불러도 됨). 설정이 비어 있으면 아무 일도 하지 않습니다.
    public static void AttachCustom(GameObject enemy, string key, EnemyStatusVfxSettings settings, System.Func<bool> isActive)
    {
        if (enemy == null || string.IsNullOrEmpty(key) || settings == null || settings.prefab == null || isActive == null) return;

        var vfx = enemy.GetComponent<EnemyStatusVfx>();
        if (vfx == null) vfx = enemy.AddComponent<EnemyStatusVfx>();
        vfx.AttachCustomInternal(key, settings, isActive);
    }

    private void AttachCustomInternal(string key, EnemyStatusVfxSettings settings, System.Func<bool> isActive)
    {
        if (!enabled) return; // 사라지는 연출 중에는 새로 붙이지 않습니다.

        ActiveEntry existing = entries.Find(e => e.customKey == key);
        if (existing != null)
        {
            if (existing.instance != null && existing.prefab == settings.prefab)
            {
                existing.settings = settings;
                existing.customIsActive = isActive;
                return;
            }
            ReleaseEntry(existing);
            entries.Remove(existing);
        }

        var entry = new ActiveEntry
        {
            type = EnemyControlType.None,
            customKey = key,
            customIsActive = isActive,
            settings = settings,
            prefab = settings.prefab,
        };
        entry.instance = Instantiate(settings.prefab, GetPosition(settings), settings.prefab.transform.rotation);
        entries.Add(entry);
    }

    void Awake()
    {
        controlState = GetComponent<EnemyControlState>();
        health = GetComponent<EnemyHealth>();
    }

    private void AttachInternal(EnemyControlType type, EnemyStatusVfxSettings settings)
    {
        if (!enabled) return; // 사라지는 연출 중(이 컴포넌트가 꺼진 상태)에는 새로 붙이지 않습니다.

        ActiveEntry existing = entries.Find(e => e.customKey == null && e.type == type);
        if (existing != null)
        {
            // 같은 제어가 다시 걸림 - 같은 프리팹이면 끊지 않고 그대로 이어갑니다.
            if (existing.instance != null && existing.prefab == settings.prefab)
            {
                existing.settings = settings;
                return;
            }
            ReleaseEntry(existing);
            entries.Remove(existing);
        }

        var entry = new ActiveEntry
        {
            type = type,
            settings = settings,
            prefab = settings.prefab,
        };
        // 프리팹 최상위에 정해둔 회전/크기를 그대로 쓰기 위해 프리팹의 회전값으로 생성합니다.
        entry.instance = Instantiate(settings.prefab, GetPosition(settings), settings.prefab.transform.rotation);
        entries.Add(entry);
    }

    void LateUpdate()
    {
        // 적이 이번 프레임에 움직인 뒤 위치를 맞춥니다.
        for (int i = entries.Count - 1; i >= 0; i--)
        {
            ActiveEntry e = entries[i];

            bool stillActive = e.customKey != null ? IsCustomStillActive(e) : IsStillActive(e.type);
            if (e.instance == null || !stillActive)
            {
                ReleaseEntry(e);
                entries.RemoveAt(i);
                continue;
            }

            e.instance.transform.position = GetPosition(e.settings);
        }
    }

    // [AttachCustom] 상태 이상 이펙트가 아직 켜져 있어야 하는지입니다(죽었으면 끔). 함수에서 오류가 나면 끕니다.
    private bool IsCustomStillActive(ActiveEntry e)
    {
        if (health != null && health.IsDead) return false;
        if (e.customIsActive == null) return false;
        try
        {
            return e.customIsActive();
        }
        catch (System.Exception ex)
        {
            Debug.LogError($"EnemyStatusVfx: 상태 이상 이펙트({e.customKey}) 확인 중 오류가 발생해 이펙트를 끕니다: {ex}");
            return false;
        }
    }

    // 이 제어가 아직 이 적에게 걸려 있는지입니다. EnemyControlState를 읽기만 합니다.
    private bool IsStillActive(EnemyControlType type)
    {
        if (health != null && health.IsDead) return false;
        if (controlState == null) controlState = GetComponent<EnemyControlState>();
        if (controlState == null) return false;

        if (EnemyControlState.GetTier(type) == EnemyControlTier.Strong)
        {
            return controlState.StrongType == type;
        }
        return controlState.NormalType == type;
    }

    private Vector3 GetPosition(EnemyStatusVfxSettings settings)
    {
        Vector3 basePos = transform.position;
        Renderer body = health != null ? health.TargetRenderer : null;

        if (body != null)
        {
            Bounds b = body.bounds;
            if (settings.anchor == EnemyStatusVfxSettings.AnchorPoint.BodyCenter)
            {
                basePos = b.center;
            }
            else if (settings.anchor == EnemyStatusVfxSettings.AnchorPoint.AboveHead)
            {
                basePos = new Vector3(b.center.x, b.max.y, b.center.z);
            }
        }

        return basePos + settings.positionOffset;
    }

    // 이펙트를 끕니다. 새 입자 생성을 멈추고, 설정에 따라 남은 입자를 그대로 재생하거나(기본) 전부 투명하게
    // 사라지게 한 뒤 fadeOutTime 뒤에 지웁니다(0이면 즉시).
    private static void ReleaseEntry(ActiveEntry e)
    {
        if (e == null || e.instance == null) return;

        GameObject inst = e.instance;
        e.instance = null;

        float fade = e.settings != null ? e.settings.fadeOutTime : 0f;
        bool fadeRemaining = e.settings != null && e.settings.fadeOutRemainingParticles;
        var systems = inst.GetComponentsInChildren<ParticleSystem>(true);
        if (fade > 0f && systems.Length > 0)
        {
            for (int i = 0; i < systems.Length; i++)
            {
                systems[i].Stop(false, ParticleSystemStopBehavior.StopEmitting);
            }

            // 씬이 닫히는 중(이펙트 오브젝트도 함께 지워지는 중)에는 부품을 붙이지 않습니다.
            if (fadeRemaining && inst.scene.isLoaded)
            {
                StatusVfxParticleFader.Begin(inst, fade);
            }

            // 안전장치: 어떤 경우에도 fadeOutTime이 지나면 지워지도록 기존과 같은 예약을 함께 겁니다
            // (투명하게 지우는 부품이 먼저 지우면 이 예약은 아무 일도 하지 않습니다). 투명하게 지우는 중이면
            // 마지막 프레임까지 다 옅어질 수 있도록 0.1초 여유를 둡니다.
            Destroy(inst, fadeRemaining ? fade + 0.1f : fade);
        }
        else
        {
            Destroy(inst);
        }
    }

    // 사라지는 연출(DissolveAndVanish가 이 컴포넌트를 끔)이 시작되거나 적 오브젝트가 없어질 때, 적을
    // 따라다니던 이펙트가 혼자 남지 않도록 전부 지웁니다.
    void OnDisable()
    {
        for (int i = 0; i < entries.Count; i++)
        {
            ReleaseEntry(entries[i]);
        }
        entries.Clear();
    }
}
