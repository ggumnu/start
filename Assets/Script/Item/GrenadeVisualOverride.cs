using System.Collections.Generic;
using UnityEngine;

// [수류탄 외형 등록소 - 아이템에 따라 수류탄 몸체 색 / 폭발 이펙트가 바뀜]
// 총알 색 등록소(ProjectileColorOverride)와 같은 정적 등록소 패턴입니다. 아이템이 "출처 이름 + 외형 + 우선순위"를
// 등록해두면, PlayerGrenadeThrower가
//   1) 수류탄을 던질 때(ReleaseGrenade) 그 수류탄 몸체에 색을 입히고
//   2) 폭발 이펙트를 만들 때(NotifyExploded) 대체 폭발 프리팹으로 바꾸거나, 생성된 이펙트에 색을 덧씌웁니다.
// 첫 사용처는 57번(토끼꼬리 수류탄 - 흰 수류탄 + 흰 폭발).
//
// [어떤 외형이 이기나 - 총알 색과 같은 규칙, 형빈 확정]
// 등록된 것 중 우선순위(priority)가 가장 높은 것을 씁니다. 우선순위가 같으면 나중에 얻은(나중에 등록된) 아이템이
// 이깁니다. 등록된 게 없으면 원래 수류탄/원래 폭발 그대로입니다.
//
// [수류탄 몸체 - 그 수류탄 하나에만 색을 덮어씀]
// 모든 수류탄이 같은 재질(Grenade_Mat)을 함께 쓰므로, 재질 색을 바꾸면 모든 수류탄이 한꺼번에 바뀌고 게임을 다시
// 시작해도 재질에 색이 남습니다. 그래서 MaterialPropertyBlock으로 그 수류탄 하나의 색(_BaseColor/_Color)만
// 덮어씁니다. 꼬리(TrailRenderer) 같은 다른 렌더러는 건드리지 않고 모델(MeshRenderer/SkinnedMeshRenderer)만 칠합니다.
//
// [폭발 이펙트 색 덧씌우기 - 4번 폭발의 23번 색과 같은 방식]
// 생성된 그 폭발 하나의 파티클 시작 색(Start Color)과 수명에 따른 색(Color over Lifetime)을 바꿉니다. 원본 프리팹은
// 건드리지 않고, 투명도는 원래 값을 유지합니다.
// - 기본: 색이 있는(채도가 있는) 파티클만 바꾸고 흰 섬광/검은 그을음 같은 무채색은 그대로 둡니다.
// - tintAchromatic을 켜면 무채색 파티클(검은 그을음 등)까지 전부 그 색으로 바꿉니다(57번은 켬 - 그을음도 흰색).
// - 파티클 재질의 텍스처 자체에 색이 그려진 이펙트는 색을 덧씌워도 원래 색이 남을 수 있습니다. 그럴 때는 원본 이펙트를
//   복제해 직접 수정한 프리팹을 replacementExplosionPrefab(대체 폭발 프리팹)에 넣으세요. 대체 프리팹을 쓰면 색은
//   덧씌우지 않습니다(이미 원하는 색으로 만든 프리팹이라고 봄). 크기/위치/소멸 시간은 원래 폭발 설정을 그대로 씁니다.
//
// [등록자(플레이어)가 사라졌을 때]
// 주인 오브젝트(보통 플레이어)가 파괴되면(런 재시작 등) 그 등록은 자동으로 무시/정리됩니다. Play를 새로 시작할 때도 비웁니다.
//
// [나중에 생길 수류탄 계열 - 분열 수류탄 등]
// 새 수류탄 오브젝트를 만들 때 ApplyToGrenade(), 그 폭발 이펙트를 만들 때 SpawnExplosionVfx()와 같은 처리를 거치게 하면
// 같은 외형이 됩니다.
public static class GrenadeVisualOverride
{
    // 한 아이템이 등록하는 수류탄 외형 묶음입니다. 아이템 효과 클래스의 필드로 두면 인스펙터에 그대로 보이고, 등록은 이 묶음을
    // 그대로 가리키므로 Play 중에 값을 바꾸면 다음에 던지는 수류탄부터 바로 반영됩니다.
    [System.Serializable]
    public class Style
    {
        [Tooltip("켜면 날아가는 수류탄 모델에 아래 색을 입힙니다.")]
        public bool tintBody = true;
        [Tooltip("수류탄 모델 색입니다.")]
        public Color bodyColor = Color.white;

        [Tooltip("켜면 폭발 이펙트의 파티클 색을 아래 색으로 덧씌웁니다(대체 폭발 프리팹을 쓰면 덧씌우지 않음).")]
        public bool tintExplosion = true;
        [Tooltip("폭발 이펙트에 덧씌울 색입니다. 투명도는 원래 파티클 값을 유지합니다.")]
        public Color explosionTint = Color.white;
        [Tooltip("켜면 흰 섬광/검은 그을음 같은 무채색 파티클까지 전부 위 색으로 바꿉니다. 끄면 색이 있는 파티클만 바꿉니다.")]
        public bool tintAchromatic = true;

        [Tooltip("비워두면 원래 폭발 이펙트에 위 색을 덧씌웁니다. 채우면 폭발 이펙트를 이 프리팹으로 통째로 바꿉니다(색은 덧씌우지 않음). " +
                 "텍스처 자체에 색이 그려져 있어 덧씌우기로 원하는 색이 안 나올 때, 원본 이펙트를 복제해 직접 수정한 프리팹을 넣으세요.")]
        public GameObject replacementExplosionPrefab;
    }

    private class Entry
    {
        public Object owner;
        public Style style;
        public int priority;
        public long order; // 등록 순서(클수록 나중에 얻음)
    }

    private static readonly Dictionary<string, Entry> entries = new Dictionary<string, Entry>();
    private static readonly List<string> deadKeys = new List<string>();
    private static long nextOrder;
    private static MaterialPropertyBlock sharedBlock;

    private static readonly int BaseColorId = Shader.PropertyToID("_BaseColor");
    private static readonly int ColorId = Shader.PropertyToID("_Color");

    // 무채색 판정 기준(4번 폭발의 23번 색 덧씌우기와 같은 값)
    private const float TintMinSaturation = 0.2f; // 이 채도 미만은 무채색
    private const float TintMinValue = 0.1f;      // 이 밝기 미만(거의 검정)도 무채색

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetOnPlayStart()
    {
        entries.Clear();
        nextOrder = 0;
    }

    // 외형을 등록합니다. 같은 sourceId로 다시 부르면 내용/우선순위를 갱신하고 "가장 나중에 얻은 것"으로 취급합니다.
    public static void SetStyle(string sourceId, Object owner, Style style, int priority)
    {
        if (string.IsNullOrEmpty(sourceId) || style == null) return;

        PruneDead();
        entries[sourceId] = new Entry
        {
            owner = owner,
            style = style,
            priority = priority,
            order = ++nextOrder,
        };
    }

    // 등록을 해제합니다. 등록된 적이 없는 sourceId여도 안전합니다.
    public static void RemoveStyle(string sourceId)
    {
        if (string.IsNullOrEmpty(sourceId)) return;
        entries.Remove(sourceId);
    }

    // 지금 적용할 외형입니다(우선순위가 가장 높은 것, 같으면 나중에 얻은 것). 등록된 게 없으면 false.
    public static bool TryGetStyle(out Style style)
    {
        PruneDead();

        style = null;
        Entry best = null;
        foreach (var pair in entries)
        {
            Entry e = pair.Value;
            if (best == null || e.priority > best.priority || (e.priority == best.priority && e.order > best.order))
            {
                best = e;
            }
        }

        if (best == null) return false;
        style = best.style;
        return true;
    }

    // [던질 때] 날아가는 수류탄 하나의 모델 렌더러에만 몸체 색을 덮어씁니다. 등록이 없으면 아무 일도 하지 않습니다.
    public static void ApplyToGrenade(GameObject grenade)
    {
        if (grenade == null) return;
        if (!TryGetStyle(out Style style) || !style.tintBody) return;

        if (sharedBlock == null) sharedBlock = new MaterialPropertyBlock();

        Renderer[] renderers = grenade.GetComponentsInChildren<Renderer>();
        for (int i = 0; i < renderers.Length; i++)
        {
            Renderer r = renderers[i];
            if (r == null) continue;
            // 모델만 칠합니다(꼬리 TrailRenderer, 표시용 LineRenderer, 파티클 등은 원래 색 그대로).
            if (!(r is MeshRenderer) && !(r is SkinnedMeshRenderer)) continue;

            r.GetPropertyBlock(sharedBlock);
            sharedBlock.SetColor(BaseColorId, style.bodyColor); // URP 재질(Grenade_Mat 등)
            sharedBlock.SetColor(ColorId, style.bodyColor);     // 그 외 셰이더
            r.SetPropertyBlock(sharedBlock);
            sharedBlock.Clear();
        }
    }

    // [터질 때] 지금 쓸 폭발 프리팹을 돌려줍니다. 등록된 외형에 대체 폭발 프리팹이 있으면 그것을, 없으면 원래 프리팹을 씁니다.
    // usedReplacement가 true면 대체 프리팹이므로 색을 덧씌우지 않습니다.
    public static GameObject ResolveExplosionPrefab(GameObject defaultPrefab, out bool usedReplacement)
    {
        usedReplacement = false;
        if (TryGetStyle(out Style style) && style.replacementExplosionPrefab != null)
        {
            usedReplacement = true;
            return style.replacementExplosionPrefab;
        }
        return defaultPrefab;
    }

    // [터질 때] 생성된 폭발 이펙트 하나에 등록된 색을 덧씌웁니다. 등록이 없거나 색 덧씌우기가 꺼져 있으면 아무 일도 하지 않습니다.
    public static void ApplyToExplosion(GameObject vfxInstance)
    {
        if (vfxInstance == null) return;
        if (!TryGetStyle(out Style style) || !style.tintExplosion) return;

        TintParticles(vfxInstance, style.explosionTint, style.tintAchromatic);
    }

    // 이펙트 안 모든 파티클의 시작 색 + 수명에 따른 색을 tint로 바꿉니다(투명도 유지). includeAchromatic이 false면 무채색은 그대로.
    public static void TintParticles(GameObject instance, Color tint, bool includeAchromatic)
    {
        if (instance == null) return;

        var systems = instance.GetComponentsInChildren<ParticleSystem>(true);
        for (int i = 0; i < systems.Length; i++)
        {
            var main = systems[i].main;
            main.startColor = TintMinMaxGradient(main.startColor, tint, includeAchromatic);

            var colorOverLifetime = systems[i].colorOverLifetime;
            if (colorOverLifetime.enabled)
            {
                colorOverLifetime.color = TintMinMaxGradient(colorOverLifetime.color, tint, includeAchromatic);
            }
        }
    }

    private static ParticleSystem.MinMaxGradient TintMinMaxGradient(ParticleSystem.MinMaxGradient source, Color tint, bool includeAchromatic)
    {
        switch (source.mode)
        {
            case ParticleSystemGradientMode.Color:
                return new ParticleSystem.MinMaxGradient(TintColor(source.color, tint, includeAchromatic));

            case ParticleSystemGradientMode.TwoColors:
                return new ParticleSystem.MinMaxGradient(TintColor(source.colorMin, tint, includeAchromatic),
                                                         TintColor(source.colorMax, tint, includeAchromatic));

            case ParticleSystemGradientMode.Gradient:
                return new ParticleSystem.MinMaxGradient(TintGradient(source.gradient, tint, includeAchromatic));

            case ParticleSystemGradientMode.TwoGradients:
                return new ParticleSystem.MinMaxGradient(TintGradient(source.gradientMin, tint, includeAchromatic),
                                                         TintGradient(source.gradientMax, tint, includeAchromatic));

            case ParticleSystemGradientMode.RandomColor:
                var random = new ParticleSystem.MinMaxGradient(TintGradient(source.gradient, tint, includeAchromatic));
                random.mode = ParticleSystemGradientMode.RandomColor;
                return random;

            default:
                return source;
        }
    }

    private static Gradient TintGradient(Gradient source, Color tint, bool includeAchromatic)
    {
        if (source == null) return null;

        GradientColorKey[] colorKeys = source.colorKeys;
        for (int i = 0; i < colorKeys.Length; i++)
        {
            colorKeys[i].color = TintColor(colorKeys[i].color, tint, includeAchromatic);
        }

        var result = new Gradient();
        result.mode = source.mode;
        result.SetKeys(colorKeys, source.alphaKeys);
        return result;
    }

    // 색을 tint로 바꿉니다(투명도 유지). includeAchromatic이 false면 무채색(흰색/회색/검정)은 그대로 돌려줍니다.
    private static Color TintColor(Color source, Color tint, bool includeAchromatic)
    {
        if (!includeAchromatic)
        {
            Color.RGBToHSV(source, out _, out float saturation, out float value);
            if (saturation < TintMinSaturation || value < TintMinValue) return source;
        }

        return new Color(tint.r, tint.g, tint.b, source.a);
    }

    // 주인이 이미 파괴된 등록을 사전에서 치웁니다.
    private static void PruneDead()
    {
        deadKeys.Clear();

        foreach (var pair in entries)
        {
            if (pair.Value.owner == null) deadKeys.Add(pair.Key);
        }

        for (int i = 0; i < deadKeys.Count; i++)
        {
            entries.Remove(deadKeys[i]);
        }
    }
}
