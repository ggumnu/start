using System.Collections.Generic;
using UnityEngine;

// [플레이어 총알 색 등록소 - 아이템에 따라 총알 색이 바뀜 (아이작 리펜던스 방식)]
// 아이템이 "출처 이름 + 색 + 우선순위"를 등록해두면, 플레이어 총알이 발사될 때(Projectile.Launch) 그 총알에 색을
// 입힙니다. WallBounceOverride(35번)/SplitShotOverride(19번)와 같은 정적 등록소 패턴입니다. 지금은 BulletColorEffect
// (아이템 에셋 Special Effects에 넣는 패시브 효과)가 이 등록소를 씁니다 - 첫 사용처는 38번(매료, 보라색 총알).
//
// [어떤 색이 이기나 - 형빈 확정]
// 등록된 것 중 우선순위(priority)가 가장 높은 색을 씁니다. 우선순위가 같으면 나중에 얻은(나중에 등록된) 아이템 색이
// 이깁니다. 등록된 게 없으면 총알 원래 재질 색 그대로입니다.
//
// [총알마다 색만 덮어씀 - 재질은 그대로]
// 모든 플레이어 총알은 같은 재질(Bullet_Player_Mat)을 함께 쓰므로, 재질 색을 바꾸면 모든 총알이 한꺼번에 바뀌고 게임을
// 다시 시작해도 재질에 색이 남습니다. 그래서 MaterialPropertyBlock으로 그 총알 하나의 색(_BaseColor/_Color)만
// 덮어씁니다(재질 복제 없음). 꼬리(BulletTrail)도 이 덮어쓴 색을 먼저 읽어 같은 색 꼬리를 그립니다.
// 분열탄도 Projectile.Launch를 거치므로 자동으로 같은 색이 되고, 적 총알(EnemyProjectile)은 영향이 없습니다.
//
// [등록자(플레이어)가 사라졌을 때]
// 등록할 때 주인 오브젝트(보통 플레이어)를 함께 넘기고, 주인이 파괴되면(런 재시작 등) 그 등록은 자동으로
// 무시/정리됩니다. Play를 새로 시작할 때도 비웁니다.
//
// [사용 방법 - 코드에서]
// ProjectileColorOverride.SetColor("출처_이름", 주인GameObject, 색, 우선순위);
// ProjectileColorOverride.RemoveColor("출처_이름");   // 장신구처럼 뺄 수 있는 아이템이 생기면 뺄 때 호출
public static class ProjectileColorOverride
{
    private class Entry
    {
        public Object owner;
        public Color color;
        public int priority;
        public long order; // 등록 순서(클수록 나중에 얻음)
    }

    private static readonly Dictionary<string, Entry> entries = new Dictionary<string, Entry>();
    private static readonly List<string> deadKeys = new List<string>();
    private static long nextOrder;
    private static MaterialPropertyBlock sharedBlock;

    private static readonly int BaseColorId = Shader.PropertyToID("_BaseColor");
    private static readonly int ColorId = Shader.PropertyToID("_Color");

    // 에디터에서 도메인 리로드를 끈 상태로 Play를 다시 눌러도 이전 Play의 등록이 남지 않도록, Play 시작 때마다 비웁니다.
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetOnPlayStart()
    {
        entries.Clear();
        nextOrder = 0;
    }

    // 총알 색을 등록합니다. 같은 sourceId로 다시 부르면 색/우선순위를 갱신하고 "가장 나중에 얻은 것"으로 취급합니다.
    public static void SetColor(string sourceId, Object owner, Color color, int priority)
    {
        if (string.IsNullOrEmpty(sourceId)) return;

        PruneDead();
        entries[sourceId] = new Entry
        {
            owner = owner,
            color = color,
            priority = priority,
            order = ++nextOrder,
        };
    }

    // 등록을 해제합니다. 등록된 적이 없는 sourceId여도 안전합니다.
    public static void RemoveColor(string sourceId)
    {
        if (string.IsNullOrEmpty(sourceId)) return;

        entries.Remove(sourceId);
    }

    // 지금 적용할 총알 색입니다(우선순위가 가장 높은 것, 같으면 나중에 얻은 것). 등록된 게 없으면 false.
    public static bool TryGetColor(out Color color)
    {
        PruneDead();

        color = Color.white;
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
        color = best.color;
        return true;
    }

    // 플레이어 총알이 발사될 때(Projectile.Launch) 호출합니다. 적용할 색이 있으면 그 총알(과 자식)의 렌더러에만 색을
    // 덮어씁니다. 없으면 아무 일도 하지 않습니다(원래 재질 색 그대로).
    public static void ApplyTo(GameObject projectile)
    {
        if (projectile == null) return;
        if (!TryGetColor(out Color color)) return;

        ApplyColor(projectile, color);
    }

    // 정해진 색을 그 총알(과 자식)의 렌더러에만 덮어씁니다. 등록소와 상관없이 총알마다 색이 다른 효과(23번 30색 물감
    // 세트)가 Projectile.Launch에서 직접 씁니다. 꼬리(BulletTrail)도 이 색을 읽어 같은 색이 됩니다.
    public static void ApplyColor(GameObject projectile, Color color)
    {
        if (projectile == null) return;

        if (sharedBlock == null) sharedBlock = new MaterialPropertyBlock();

        Renderer[] renderers = projectile.GetComponentsInChildren<Renderer>();
        for (int i = 0; i < renderers.Length; i++)
        {
            Renderer r = renderers[i];
            if (r == null) continue;

            r.GetPropertyBlock(sharedBlock);
            sharedBlock.SetColor(BaseColorId, color); // URP 재질(Bullet_Player_Mat 등)
            sharedBlock.SetColor(ColorId, color);     // 그 외 셰이더
            r.SetPropertyBlock(sharedBlock);
            sharedBlock.Clear();
        }
    }

    // 렌더러에 이 등록소가 덮어쓴 색이 있으면 그 색을 돌려줍니다(꼬리 색을 총알 색에 맞출 때 BulletTrail이 사용).
    public static bool TryGetRendererOverrideColor(Renderer renderer, out Color color)
    {
        color = Color.white;
        if (renderer == null || !renderer.HasPropertyBlock()) return false;

        if (sharedBlock == null) sharedBlock = new MaterialPropertyBlock();
        renderer.GetPropertyBlock(sharedBlock);

        bool found = false;
        if (sharedBlock.HasColor(BaseColorId))
        {
            color = sharedBlock.GetColor(BaseColorId);
            found = true;
        }
        else if (sharedBlock.HasColor(ColorId))
        {
            color = sharedBlock.GetColor(ColorId);
            found = true;
        }

        sharedBlock.Clear();
        return found;
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
