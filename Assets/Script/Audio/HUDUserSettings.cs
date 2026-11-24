using System.Collections.Generic;
using System.Globalization;
using UnityEngine;

// [플레이어 HUD 설정 - 설정 창 HUD 탭(SettingsUI/SettingsHUDTab)이 바꾸고, 스탯 창(PlayerStatDisplay)이 읽음]
// 값은 PlayerPrefs에 저장되어 게임을 다시 켜도 유지됩니다(GameAudio의 볼륨과 같은 방식 - 바꾸는 즉시 PlayerPrefs에 넣고,
// 설정 창을 닫을 때 Save로 디스크에 기록). 시작 화면에서 바꿔도 게임에 그대로 적용됩니다.
//
// [불투명도 - 형빈 확정] 100% = 선명, 0% = 안 보임. 모두 1% 단위로 저장합니다(0%/100% 판정이 정확하도록).
// - WindowOpacity: 스탯 뒤 흰 창.
// - 숫자를 흰색으로(+ 검은 그림자): 전투 화면/비전투 화면을 따로 고릅니다(WhiteTextCombat/WhiteTextNormal - 전투 중 창 불투명도가
//   평소와 다를 수 있어서, 형빈 확정). 창 불투명도와 상관없이 언제든 켤 수 있습니다. 전투 화면 = 클리어 전 전투 방(자동 반투명을
//   꺼도 이 체크는 따로 적용). 예전 한 칸짜리 저장값(MobArchive.HUD.WhiteText)이 있으면 두 칸의 처음 값으로 씁니다.
// - ContentOpacity: 아이콘 + 숫자. 0%면 스탯 창이 아예 없는 것으로 보고 흰 창/접기 버튼까지 통째로 숨깁니다.
// - 전투 중 자동 반투명(CombatFade): 전투 방(클리어 전)에서는 CombatOpacity를 씁니다. 모드 = 배경 창만 / 전체.
//   전투에 들어가서 더 진해지는 일이 없도록 각 요소 = min(평소 값, 전투 값).
//   전투 값의 상한(CombatOpacityCap) = 배경 창만이면 창 불투명도, 전체면 창/아이콘 중 높은 쪽. 상한보다 올릴 수 없고(설정 창 손잡이가
//   멈춤), 평소 값을 내려 상한이 내려가면 전투 값도 같이 내려갑니다.
// [인벤토리 HUD - 형빈 확정] 슬롯 인벤토리(PlayerSlotItemDisplay) + 패시브 인벤토리(PlayerInventoryDisplay)
// - InventoryWindowOpacity: 두 인벤토리의 흰 창을 한꺼번에.
// - SlotSubFrameOpacity: 슬롯 보조틀(장신구/일회용/음식 + Q/E 그림) + 슬롯 칸 틀.
// - SlotIconOpacity / PassiveIconOpacity: 아이콘(따로따로). 0%면 그 인벤토리를 창/접기 버튼까지 통째로 숨김.
// - 전투 중 자동 반투명은 스탯과 따로: 모드 [배경 창만] = 흰 창 + 보조틀/칸 틀, [전체] = 여기에 아이콘 둘 다.
//   상한 = 영향받는 값 중 가장 높은 값, 각 요소 = min(평소 값, 전투 값).
public enum HUDCombatFadeMode
{
    BackgroundOnly = 0, // 흰 창만 반투명
    All = 1,            // 흰 창 + 아이콘/숫자 모두
}

public static class HUDUserSettings
{
    private const string HiddenStatsKey = "MobArchive.HUD.HiddenStats";
    private const string WindowOpacityKey = "MobArchive.HUD.WindowOpacity";
    private const string LegacyWhiteTextKey = "MobArchive.HUD.WhiteText";
    private const string WhiteTextNormalKey = "MobArchive.HUD.WhiteTextNormal";
    private const string WhiteTextCombatKey = "MobArchive.HUD.WhiteTextCombat";
    private const string ContentOpacityKey = "MobArchive.HUD.ContentOpacity";
    private const string CombatFadeKey = "MobArchive.HUD.CombatFade";
    private const string CombatModeKey = "MobArchive.HUD.CombatMode";
    private const string CombatOpacityKey = "MobArchive.HUD.CombatOpacity";
    private const string InvWindowOpacityKey = "MobArchive.HUD.Inv.WindowOpacity";
    private const string SlotSubFrameOpacityKey = "MobArchive.HUD.Inv.SlotSubFrameOpacity";
    private const string SlotIconOpacityKey = "MobArchive.HUD.Inv.SlotIconOpacity";
    private const string PassiveIconOpacityKey = "MobArchive.HUD.Inv.PassiveIconOpacity";
    private const string InvCombatFadeKey = "MobArchive.HUD.Inv.CombatFade";
    private const string InvCombatModeKey = "MobArchive.HUD.Inv.CombatMode";
    private const string InvCombatOpacityKey = "MobArchive.HUD.Inv.CombatOpacity";

    // 기본값(형빈 확정)
    public const float DefaultWindowOpacity = 0.8f;
    public const float DefaultContentOpacity = 1f;
    public const float DefaultCombatOpacity = 0.3f;
    // 전투 중 자동 반투명은 처음부터 켜짐(스탯/인벤토리 둘 다 - 형빈 확정)
    public const bool DefaultCombatFade = true;

    private static bool loaded;
    private static readonly HashSet<int> hiddenStats = new HashSet<int>();
    private static float windowOpacity;
    private static bool whiteTextNormal;
    private static bool whiteTextCombat;
    private static float contentOpacity;
    private static bool combatFade;
    private static HUDCombatFadeMode combatMode;
    private static float combatOpacity;
    private static float invWindowOpacity;
    private static float slotSubFrameOpacity;
    private static float slotIconOpacity;
    private static float passiveIconOpacity;
    private static bool invCombatFade;
    private static HUDCombatFadeMode invCombatMode;
    private static float invCombatOpacity;

    // 값이 바뀔 때마다 1씩 오릅니다(보는 쪽이 "바뀌었나"를 싸게 확인하는 용도).
    public static int Version { get; private set; }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetStatics()
    {
        loaded = false;
        hiddenStats.Clear();
        Version = 0;
    }

    private static void EnsureLoaded()
    {
        if (loaded) return;
        loaded = true;

        hiddenStats.Clear();
        string hidden = PlayerPrefs.GetString(HiddenStatsKey, "");
        foreach (string part in hidden.Split(','))
        {
            if (int.TryParse(part, NumberStyles.Integer, CultureInfo.InvariantCulture, out int id)) hiddenStats.Add(id);
        }

        windowOpacity = Round01(PlayerPrefs.GetFloat(WindowOpacityKey, DefaultWindowOpacity));
        int legacyWhite = PlayerPrefs.GetInt(LegacyWhiteTextKey, 0);
        whiteTextNormal = PlayerPrefs.GetInt(WhiteTextNormalKey, legacyWhite) != 0;
        whiteTextCombat = PlayerPrefs.GetInt(WhiteTextCombatKey, legacyWhite) != 0;
        contentOpacity = Round01(PlayerPrefs.GetFloat(ContentOpacityKey, DefaultContentOpacity));
        combatFade = PlayerPrefs.GetInt(CombatFadeKey, DefaultCombatFade ? 1 : 0) != 0;
        combatMode = PlayerPrefs.GetInt(CombatModeKey, 0) == 1 ? HUDCombatFadeMode.All : HUDCombatFadeMode.BackgroundOnly;
        combatOpacity = Round01(PlayerPrefs.GetFloat(CombatOpacityKey, DefaultCombatOpacity));
        combatOpacity = Mathf.Min(combatOpacity, CombatOpacityCap); // 저장값이 규칙을 어겨도 바로잡음

        invWindowOpacity = Round01(PlayerPrefs.GetFloat(InvWindowOpacityKey, DefaultWindowOpacity));
        slotSubFrameOpacity = Round01(PlayerPrefs.GetFloat(SlotSubFrameOpacityKey, 1f));
        slotIconOpacity = Round01(PlayerPrefs.GetFloat(SlotIconOpacityKey, 1f));
        passiveIconOpacity = Round01(PlayerPrefs.GetFloat(PassiveIconOpacityKey, 1f));
        invCombatFade = PlayerPrefs.GetInt(InvCombatFadeKey, DefaultCombatFade ? 1 : 0) != 0;
        invCombatMode = PlayerPrefs.GetInt(InvCombatModeKey, 0) == 1 ? HUDCombatFadeMode.All : HUDCombatFadeMode.BackgroundOnly;
        invCombatOpacity = Round01(PlayerPrefs.GetFloat(InvCombatOpacityKey, DefaultCombatOpacity));
        invCombatOpacity = Mathf.Min(invCombatOpacity, InventoryCombatOpacityCap);
    }

    // 1% 단위로 맞춥니다(0%/100% 판정이 소수 오차에 흔들리지 않게).
    private static float Round01(float v)
    {
        return Mathf.Round(Mathf.Clamp01(v) * 100f) / 100f;
    }

    private static void Changed()
    {
        Version++;
    }

    // ---------------- 보이는 스탯 ----------------

    public static bool IsStatHidden(StatType type)
    {
        EnsureLoaded();
        return hiddenStats.Contains((int)type);
    }

    public static void SetStatHidden(StatType type, bool hidden)
    {
        EnsureLoaded();
        bool changed = hidden ? hiddenStats.Add((int)type) : hiddenStats.Remove((int)type);
        if (!changed) return;

        var ids = new List<int>(hiddenStats);
        ids.Sort();
        var parts = new string[ids.Count];
        for (int i = 0; i < ids.Count; i++) parts[i] = ids[i].ToString(CultureInfo.InvariantCulture);
        PlayerPrefs.SetString(HiddenStatsKey, string.Join(",", parts));
        Changed();
    }

    // ---------------- 불투명도 ----------------

    public static float WindowOpacity
    {
        get { EnsureLoaded(); return windowOpacity; }
        set
        {
            EnsureLoaded();
            float v = Round01(value);
            if (Mathf.Approximately(v, windowOpacity)) return;
            windowOpacity = v;
            PlayerPrefs.SetFloat(WindowOpacityKey, v);
            ClampCombatOpacityToCap();
            Changed();
        }
    }

    // 비전투 화면에서 숫자를 흰색으로
    public static bool WhiteTextNormal
    {
        get { EnsureLoaded(); return whiteTextNormal; }
        set
        {
            EnsureLoaded();
            if (whiteTextNormal == value) return;
            whiteTextNormal = value;
            PlayerPrefs.SetInt(WhiteTextNormalKey, value ? 1 : 0);
            Changed();
        }
    }

    // 전투 화면(클리어 전 전투 방)에서 숫자를 흰색으로
    public static bool WhiteTextCombat
    {
        get { EnsureLoaded(); return whiteTextCombat; }
        set
        {
            EnsureLoaded();
            if (whiteTextCombat == value) return;
            whiteTextCombat = value;
            PlayerPrefs.SetInt(WhiteTextCombatKey, value ? 1 : 0);
            Changed();
        }
    }

    // 지금 흰 글자를 쓸지입니다(inCombat = 클리어 전 전투 방인지).
    public static bool GetWhiteText(bool inCombat)
    {
        return inCombat ? WhiteTextCombat : WhiteTextNormal;
    }

    public static float ContentOpacity
    {
        get { EnsureLoaded(); return contentOpacity; }
        set
        {
            EnsureLoaded();
            float v = Round01(value);
            if (Mathf.Approximately(v, contentOpacity)) return;
            contentOpacity = v;
            PlayerPrefs.SetFloat(ContentOpacityKey, v);
            ClampCombatOpacityToCap();
            Changed();
        }
    }

    // 아이콘/숫자가 0%면 스탯 창을 통째로 숨깁니다(형빈 확정).
    public static bool HidesWholePanel => ContentOpacity <= 0f;

    // ---------------- 전투 중 자동 반투명 ----------------

    public static bool CombatFade
    {
        get { EnsureLoaded(); return combatFade; }
        set
        {
            EnsureLoaded();
            if (combatFade == value) return;
            combatFade = value;
            PlayerPrefs.SetInt(CombatFadeKey, value ? 1 : 0);
            Changed();
        }
    }

    public static HUDCombatFadeMode CombatMode
    {
        get { EnsureLoaded(); return combatMode; }
        set
        {
            EnsureLoaded();
            if (combatMode == value) return;
            combatMode = value;
            PlayerPrefs.SetInt(CombatModeKey, value == HUDCombatFadeMode.All ? 1 : 0);
            ClampCombatOpacityToCap();
            Changed();
        }
    }

    // 전투 중 불투명도의 상한입니다(파일 상단 설명).
    public static float CombatOpacityCap
    {
        get
        {
            EnsureLoaded();
            return combatMode == HUDCombatFadeMode.All ? Mathf.Max(windowOpacity, contentOpacity) : windowOpacity;
        }
    }

    // 넣는 값은 상한에서 잘립니다(설정 창 손잡이가 상한에서 멈추는 이유).
    public static float CombatOpacity
    {
        get { EnsureLoaded(); return combatOpacity; }
        set
        {
            EnsureLoaded();
            float v = Mathf.Min(Round01(value), CombatOpacityCap);
            if (Mathf.Approximately(v, combatOpacity)) return;
            combatOpacity = v;
            PlayerPrefs.SetFloat(CombatOpacityKey, v);
            Changed();
        }
    }

    private static void ClampCombatOpacityToCap()
    {
        float cap = CombatOpacityCap;
        if (combatOpacity <= cap) return;
        combatOpacity = cap;
        PlayerPrefs.SetFloat(CombatOpacityKey, combatOpacity);
    }

    // 지금 적용할 (흰 창, 아이콘/숫자) 불투명도입니다. inCombat = 전투 방(클리어 전)인지 - 자동 반투명이 꺼져 있으면 무시합니다.
    public static void GetOpacities(bool inCombat, out float windowAlpha, out float contentAlpha)
    {
        EnsureLoaded();
        windowAlpha = windowOpacity;
        contentAlpha = contentOpacity;
        if (!inCombat || !combatFade) return;

        windowAlpha = Mathf.Min(windowOpacity, combatOpacity);
        if (combatMode == HUDCombatFadeMode.All) contentAlpha = Mathf.Min(contentOpacity, combatOpacity);
    }

    // ---------------- 인벤토리 (슬롯 + 패시브) ----------------

    // 불투명도 값 하나를 바꾸는 공통 처리(1% 단위 저장 + 전투 상한 다시 맞춤).
    private static bool SetInvOpacity(ref float field, string key, float value)
    {
        EnsureLoaded();
        float v = Round01(value);
        if (Mathf.Approximately(v, field)) return false;
        field = v;
        PlayerPrefs.SetFloat(key, v);
        ClampInventoryCombatOpacityToCap();
        Changed();
        return true;
    }

    public static float InventoryWindowOpacity
    {
        get { EnsureLoaded(); return invWindowOpacity; }
        set { SetInvOpacity(ref invWindowOpacity, InvWindowOpacityKey, value); }
    }

    public static float SlotSubFrameOpacity
    {
        get { EnsureLoaded(); return slotSubFrameOpacity; }
        set { SetInvOpacity(ref slotSubFrameOpacity, SlotSubFrameOpacityKey, value); }
    }

    public static float SlotIconOpacity
    {
        get { EnsureLoaded(); return slotIconOpacity; }
        set { SetInvOpacity(ref slotIconOpacity, SlotIconOpacityKey, value); }
    }

    public static float PassiveIconOpacity
    {
        get { EnsureLoaded(); return passiveIconOpacity; }
        set { SetInvOpacity(ref passiveIconOpacity, PassiveIconOpacityKey, value); }
    }

    // 아이콘이 0%면 그 인벤토리를 통째로 숨깁니다(형빈 확정 - 아이콘이 안 보이면 창도 필요 없음).
    public static bool HidesSlotPanel => SlotIconOpacity <= 0f;
    public static bool HidesPassivePanel => PassiveIconOpacity <= 0f;

    public static bool InventoryCombatFade
    {
        get { EnsureLoaded(); return invCombatFade; }
        set
        {
            EnsureLoaded();
            if (invCombatFade == value) return;
            invCombatFade = value;
            PlayerPrefs.SetInt(InvCombatFadeKey, value ? 1 : 0);
            Changed();
        }
    }

    public static HUDCombatFadeMode InventoryCombatMode
    {
        get { EnsureLoaded(); return invCombatMode; }
        set
        {
            EnsureLoaded();
            if (invCombatMode == value) return;
            invCombatMode = value;
            PlayerPrefs.SetInt(InvCombatModeKey, value == HUDCombatFadeMode.All ? 1 : 0);
            ClampInventoryCombatOpacityToCap();
            Changed();
        }
    }

    // 인벤토리 전투 중 불투명도의 상한 = 영향받는 값 중 가장 높은 값.
    public static float InventoryCombatOpacityCap
    {
        get
        {
            EnsureLoaded();
            float cap = Mathf.Max(invWindowOpacity, slotSubFrameOpacity);
            if (invCombatMode == HUDCombatFadeMode.All) cap = Mathf.Max(cap, Mathf.Max(slotIconOpacity, passiveIconOpacity));
            return cap;
        }
    }

    public static float InventoryCombatOpacity
    {
        get { EnsureLoaded(); return invCombatOpacity; }
        set
        {
            EnsureLoaded();
            float v = Mathf.Min(Round01(value), InventoryCombatOpacityCap);
            if (Mathf.Approximately(v, invCombatOpacity)) return;
            invCombatOpacity = v;
            PlayerPrefs.SetFloat(InvCombatOpacityKey, v);
            Changed();
        }
    }

    private static void ClampInventoryCombatOpacityToCap()
    {
        float cap = InventoryCombatOpacityCap;
        if (invCombatOpacity <= cap) return;
        invCombatOpacity = cap;
        PlayerPrefs.SetFloat(InvCombatOpacityKey, invCombatOpacity);
    }

    // 지금 적용할 인벤토리 불투명도입니다(흰 창, 보조틀+칸 틀, 슬롯 아이콘, 패시브 아이콘).
    public static void GetInventoryOpacities(bool inCombat, out float window, out float subFrame, out float slotIcon, out float passiveIcon)
    {
        EnsureLoaded();
        window = invWindowOpacity;
        subFrame = slotSubFrameOpacity;
        slotIcon = slotIconOpacity;
        passiveIcon = passiveIconOpacity;
        if (!inCombat || !invCombatFade) return;

        window = Mathf.Min(window, invCombatOpacity);
        subFrame = Mathf.Min(subFrame, invCombatOpacity);
        if (invCombatMode == HUDCombatFadeMode.All)
        {
            slotIcon = Mathf.Min(slotIcon, invCombatOpacity);
            passiveIcon = Mathf.Min(passiveIcon, invCombatOpacity);
        }
    }

    // 설정 창을 닫을 때 디스크에 기록합니다.
    public static void Save()
    {
        PlayerPrefs.Save();
    }
}
