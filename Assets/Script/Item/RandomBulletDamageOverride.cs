using System.Collections.Generic;
using UnityEngine;

// 총알 한 발에 붙는 23번(30색 물감 세트) 랜덤 추가 피해 정보입니다. PlayerShooting이 총알을 만들 때 굴려서
// Projectile.Launch()에 넘깁니다.
// - offset: 이 총알의 최종 피해에 더해질 값(이미 분열탄 효과 배율이 곱해진 값). 음수일 수 있습니다.
// - minDamage: 더한 뒤의 최종 피해 하한선(기본 1).
// - color: 굴린 값에 해당하는 무지개 색. 총알/꼬리/4번 폭발 불꽃에 입힙니다.
public struct BulletDamageRoll
{
    public float offset;
    public float minDamage;
    public Color color;
}

// [23번 패시브 - 30색 물감 세트 등록소]
// "총알마다 공격력에 -30~+30 중 하나를 랜덤으로 더하고, 굴린 값에 따라 총알 색이 빨주노초파남보 중 하나가 된다."
// SplitShotOverride(19번)/ProjectileColorOverride와 같은 정적 등록소 패턴입니다. Item23effect가 획득 시 자기 설정을
// 등록해두면, PlayerShooting이 총알을 만들 때마다 TryRoll()로 굴려서 그 결과를 총알에 넘깁니다.
//
// [굴리기 - 형빈 확정]
// - -범위 ~ +범위(기본 -30 ~ +30) 사이의 정수 61가지가 같은 확률로 나옵니다.
// - 분열탄(5번)은 새로 굴리고, 굴린 값에 그 탄의 효과 배율(Projectile.EffectScale - 1세대 0.5, 2세대 0.25)을 곱합니다.
//   예) 원본 ±30, 1세대 분열탄 ±15, 2세대 분열탄 ±7.5. 색은 배율을 곱하기 전의 굴린 값(-30~+30)으로 정합니다.
// - [행운 연동 자리] 나중에 행운이 랜덤 피해에 관여하게 되면, 아래 RollInteger() 한 곳만 고치면 됩니다.
//
// [색 구간 - 형빈 확정]
// 범위를 같은 폭의 7구간으로 나누고, 초록(가운데)이 0을 중심으로 오게 합니다. 범위 30일 때:
// 빨 -30~-22 / 주 -21~-13 / 노 -12~-5 / 초 -4~+4 / 파 +5~+12 / 남 +13~+21 / 보 +22~+30
//
// [피해 계산 순서 - 형빈 확정]
// 최종 피해 = 최대값(11번 거리 보정/35번 튕김 보너스 등이 전부 반영된 피해 + offset, minDamage).
// 계산은 Projectile.Damage에서 합니다(그래서 4번 폭발처럼 그 값을 읽는 효과에도 자동 반영).
//
// [색 우선순위 - 형빈 확정]
// 23번 색은 다른 모든 총알 색 아이템(ProjectileColorOverride - 38번 보라색 등)보다 항상 우선합니다.
//
// [등록자(플레이어)가 사라졌을 때]
// 주인 오브젝트(플레이어)가 파괴되면(런 재시작 등) 그 등록은 자동으로 무시/정리됩니다. Play 시작 때도 비웁니다.
public static class RandomBulletDamageOverride
{
    private class Entry
    {
        public Object owner;
        public RandomBulletDamageEffect settings;
        public long order;
    }

    private static readonly Dictionary<string, Entry> entries = new Dictionary<string, Entry>();
    private static readonly List<string> deadKeys = new List<string>();
    private static long nextOrder;

    // 에디터에서 도메인 리로드를 끈 상태로 Play를 다시 눌러도 이전 Play의 등록이 남지 않도록, Play 시작 때마다 비웁니다.
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetOnPlayStart()
    {
        entries.Clear();
        nextOrder = 0;
    }

    // 등록합니다. 설정 객체를 그대로 참조하므로 Play 중에 아이템 에셋의 값을 바꾸면 다음 총알부터 바로 반영됩니다.
    public static void Register(string sourceId, Object owner, RandomBulletDamageEffect settings)
    {
        if (string.IsNullOrEmpty(sourceId) || settings == null) return;

        PruneDead();
        entries[sourceId] = new Entry { owner = owner, settings = settings, order = ++nextOrder };
    }

    // 등록을 해제합니다. 등록된 적이 없는 sourceId여도 안전합니다.
    public static void Unregister(string sourceId)
    {
        if (string.IsNullOrEmpty(sourceId)) return;

        entries.Remove(sourceId);
    }

    // 살아있는 등록이 하나라도 있으면 true입니다.
    public static bool IsActive => GetActiveSettings() != null;

    // 총알 한 발에 쓸 랜덤 추가 피해를 굴립니다. 23번이 없으면 false.
    // effectScale: 그 총알의 아이템 효과 배율입니다(원본 1, 분열탄은 5번의 effectRatio가 세대마다 곱해진 값).
    public static bool TryRoll(float effectScale, out BulletDamageRoll roll)
    {
        roll = default;

        RandomBulletDamageEffect settings = GetActiveSettings();
        if (settings == null) return false;

        int range = Mathf.Max(0, settings.rollRange);
        int value = RollInteger(range);

        roll.offset = value * effectScale;
        roll.minDamage = settings.minDamage;
        roll.color = GetBandColor(settings, value, range);
        return true;
    }

    // -range ~ +range 사이 정수를 같은 확률로 굴립니다(양 끝 포함).
    // [행운 연동 자리] 나중에 행운이 관여하게 되면 여기서 확률을 조정하세요.
    private static int RollInteger(int range)
    {
        if (range <= 0) return 0;
        return Random.Range(-range, range + 1); // 정수 Random.Range는 최댓값을 포함하지 않으므로 +1
    }

    // 굴린 값을 같은 폭의 7구간(초록이 0 중심)으로 나눠 색을 고릅니다.
    // 구간 번호 = 0(초록)에서 몇 칸 떨어졌는지 = 반올림(값 x 3.5 / 범위), -3~+3.
    private static Color GetBandColor(RandomBulletDamageEffect settings, int value, int range)
    {
        int step = 0;
        if (range > 0)
        {
            float scaled = Mathf.Abs(value) * 3.5f / range;
            step = Mathf.Min(3, Mathf.FloorToInt(scaled + 0.5f));
            if (value < 0) step = -step;
        }

        switch (step)
        {
            case -3: return settings.redColor;
            case -2: return settings.orangeColor;
            case -1: return settings.yellowColor;
            case 0: return settings.greenColor;
            case 1: return settings.blueColor;
            case 2: return settings.indigoColor;
            default: return settings.violetColor;
        }
    }

    // 살아있는 등록 중 가장 나중에 등록된 설정입니다. 없으면 null.
    private static RandomBulletDamageEffect GetActiveSettings()
    {
        PruneDead();

        Entry best = null;
        foreach (var pair in entries)
        {
            if (best == null || pair.Value.order > best.order) best = pair.Value;
        }
        return best?.settings;
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

        deadKeys.Clear();
    }
}
