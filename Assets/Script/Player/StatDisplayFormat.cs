using System.Globalization;

// [스탯 화면 표시 규칙 - 한 곳에서 관리]
// 실제 스탯 값(PlayerStats/아이템 StatModifier에 입력하는 값, 게임 계산)은 그대로 두고, "플레이어가 보는
// 화면의 숫자"만 바꾸는 규칙입니다. 좌하단 스탯 패널(PlayerStatDisplay)과 아이템 설명칸/호버 툴팁
// (ItemInfoPanelView)이 모두 이 규칙을 함께 읽으므로, 두 화면의 표기가 항상 같습니다.
//
// [지금 정해둔 규칙 - 형빈 확정]
// - 총알 크기: 화면 배율 x100 (실제 0.05 -> 화면 5, 아이템 +0.03 -> +3). 기본 구체 총알의 실제 크기 값이
//   매우 작아서 화면에서 너무 미미하게 보이던 문제 해결용.
// - 게마트리아 확률 / 샬레 확률: 화면 배율 x100 + "%" (0.05 -> 5%, 아이템 +0.05 -> +5%).
// - 그 외 스탯: 배율 1, 단위 없음(원래 표기 그대로).
// 다른 스탯의 표기를 바꾸고 싶으면 아래 GetMultiplier()/GetSuffix()에 한 줄씩 추가하면 됩니다.
//
// [곱하기(Multiplicative) 효과 - 증감 퍼센트로 표시, 형빈 확정]
// 아이템 설명칸의 곱하기 효과는 "x1.2" 대신 증감 비율로 보여줍니다: 1.2 -> "+20%", 0.8 -> "-20%",
// 1.25 -> "+25%". 비율이라 위의 스탯별 화면 배율/단위는 적용하지 않습니다(FormatMultiplicative 참고).
public static class StatDisplayFormat
{
    // 화면에 보여줄 때 실제 값에 곱하는 배율입니다.
    public static float GetMultiplier(StatType type)
    {
        switch (type)
        {
            case StatType.BulletSize: return 100f;
            case StatType.GematriaChance: return 100f;
            case StatType.SchaleChance: return 100f;
            default: return 1f;
        }
    }

    // 화면 숫자 뒤에 붙이는 단위입니다(없으면 빈 문자열).
    public static string GetSuffix(StatType type)
    {
        switch (type)
        {
            case StatType.GematriaChance:
            case StatType.SchaleChance:
                return "%";
            default:
                return "";
        }
    }

    // 스탯 패널용: 현재 스탯 값 -> 화면 문자열 (예: 0.05 -> "5", 0.05 확률 -> "5%").
    // numberFormat은 숫자 형식입니다(예: "0.#" 소수 한 자리, "0" 정수).
    public static string FormatValue(StatType type, float value, string numberFormat)
    {
        float shown = value * GetMultiplier(type);
        return shown.ToString(numberFormat, CultureInfo.InvariantCulture) + GetSuffix(type);
    }

    // 아이템 설명칸용: 곱하기 효과 배율 -> 증감 퍼센트 문자열 (예: 1.2 -> "+20%", 0.8 -> "-20%").
    // 소수 첫째 자리까지 표시합니다(예: 1.333 -> "+33.3%").
    public static string FormatMultiplicative(float multiplier, string numberFormat = "0.#")
    {
        float percent = (multiplier - 1f) * 100f;
        // 1.2f 같은 값의 부동소수 오차(19.9999...)를 정리합니다.
        percent = (float)System.Math.Round(percent, 2);
        string sign = percent >= 0f ? "+" : "";
        return sign + percent.ToString(numberFormat, CultureInfo.InvariantCulture) + "%";
    }

    // 아이템 설명칸용: 더하기 효과 값 -> 부호 포함 화면 문자열 (예: 0.03 -> "+3", 0.05 확률 -> "+5%").
    public static string FormatAdditive(StatType type, float value, string numberFormat = "0.##")
    {
        float shown = value * GetMultiplier(type);
        string sign = shown >= 0f ? "+" : "";
        return sign + shown.ToString(numberFormat, CultureInfo.InvariantCulture) + GetSuffix(type);
    }
}
