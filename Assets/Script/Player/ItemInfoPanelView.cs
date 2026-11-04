﻿using System.Linq;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

// "아이템 설명칸"(아이콘+이름+분류+등급+능력치+특수효과+설명)을 실제로 만들고 채우고 배치하는
// 로직을 담은 일반 C# 클래스입니다(MonoBehaviour 아님). 원래 PlayerItemInfoDisplay.cs 안에
// 있던 BuildPanel/ShowItem/ReflowContent 등을 그대로 옮겨왔습니다(v1.38 - 아이템 호버
// 툴팁(ItemTooltipDisplay.cs) 추가를 위한 리팩토링).
//
// [왜 이렇게 분리했는가]
// 호버 툴팁도 이 설명칸과 완전히 같은 모양으로 떠야 하는데, 겹치지 않는 별개의 화면 위치에
// 동시에 떠 있어야 할 수도 있어서(예: 상자 근처에서 액티브 아이템에 커서를 올리면 두 설명칸이
// 나란히 보여야 함) 서로 다른 GameObject/RectTransform을 가진 두 개의 "인스턴스"가 필요합니다.
// 그렇다고 스타일 값(색상/크기/폰트/오프셋 등)을 담는 필드까지 이 클래스로 옮기면, 이미
// PlayerItemInfoDisplay 인스펙터에 넣어두신 등급 스프라이트·색상 값들이 저장 경로가 바뀌면서
// 초기화될 위험이 있습니다. 그래서 값(스타일)은 여전히 PlayerItemInfoDisplay가 들고 있고,
// 이 클래스는 매 프레임 그 값을 ItemInfoPanelStyle로 건네받아서 그리기만 담당합니다 -
// PlayerItemInfoDisplay.GetStyle()이 그 값 묶음을 반환하고, ItemTooltipDisplay도 매 프레임
// 같은 GetStyle()을 읽어서 쓰기 때문에 두 설명칸은 항상 자동으로 같은 모양을 유지합니다.
//
// [사용법]
// 1. Build(parent)를 한 번 호출해 UI를 만듭니다(자동으로 비활성 상태로 시작).
// 2. 보여줄 아이템이 바뀔 때 ShowItem(item, style)을 호출합니다.
// 3. 패널이 떠 있는 동안 매 프레임 Reflow(style)을 호출해서 위치/크기/색을 다시 계산합니다
//    (스타일 값을 Play 중 바꿔도 즉시 반영되도록, 원래 ReflowContent와 동일한 방식).
// 4. 더 이상 보여줄 게 없으면 Hide()를 호출합니다.
public class ItemInfoPanelView
{
    private GameObject panelRoot;
    private RectTransform panelRect;
    private Image panelImage;

    private RectTransform iconRect;
    private Image iconImage;
    private RectTransform nameRect;
    private TextMeshProUGUI nameText;
    private RectTransform categoryRect;
    private TextMeshProUGUI categoryText;
    private RectTransform tierRect;
    private Image tierImage;
    private RectTransform statRect;
    private TextMeshProUGUI statText;
    private RectTransform specialEffectRect;
    private TextMeshProUGUI specialEffectText;
    private RectTransform descriptionRect;
    private TextMeshProUGUI descriptionText;

    private ItemDefinition currentItem;
    private ItemDefinition.ItemRarity currentTier;
    private ItemDefinition.ItemCategory currentCategory;

    public GameObject PanelRoot => panelRoot;
    public RectTransform PanelRect => panelRect;
    public bool IsActive => panelRoot != null && panelRoot.activeSelf;

    // ------------------------------------------------------------------
    // 오브젝트 생성 (한 번만)
    // ------------------------------------------------------------------

    public void Build(Transform parent, string rootName = "ItemInfoPanel")
    {
        panelRoot = new GameObject(rootName);
        panelRoot.transform.SetParent(parent, false);

        panelRect = panelRoot.AddComponent<RectTransform>();
        panelRect.anchorMin = new Vector2(0f, 1f);
        panelRect.anchorMax = new Vector2(0f, 1f);
        panelRect.pivot = new Vector2(0f, 1f);

        panelImage = panelRoot.AddComponent<Image>();

        // 아이콘
        var iconGO = new GameObject("Icon");
        iconGO.transform.SetParent(panelRoot.transform, false);
        iconRect = iconGO.AddComponent<RectTransform>();
        iconRect.anchorMin = new Vector2(0f, 1f);
        iconRect.anchorMax = new Vector2(0f, 1f);
        iconRect.pivot = new Vector2(0f, 1f);
        iconImage = iconGO.AddComponent<Image>();
        iconImage.preserveAspect = true;

        // 이름
        var nameGO = new GameObject("NameText");
        nameGO.transform.SetParent(panelRoot.transform, false);
        nameRect = nameGO.AddComponent<RectTransform>();
        nameRect.anchorMin = new Vector2(0f, 1f);
        nameRect.anchorMax = new Vector2(0f, 1f);
        nameRect.pivot = new Vector2(0f, 1f);
        // [TMP] 옛 방식 Text는 화면 배율에 따라 흐릿하게 보여서 TextMeshPro로 교체했습니다(HUDText 참고).
        // 줄바꿈이 필요한 칸(이름/능력치/특수 효과/설명)은 줄바꿈을 켜고, 높이는 예전처럼 LayoutUtility로 잽니다.
        nameText = HUDText.AddTo(nameGO, TextAnchor.UpperLeft, true);

        // 분류 태그 (등급 스프라이트 왼쪽 - "[패시브]" 등)
        var categoryGO = new GameObject("CategoryText");
        categoryGO.transform.SetParent(panelRoot.transform, false);
        categoryRect = categoryGO.AddComponent<RectTransform>();
        categoryRect.anchorMin = new Vector2(0f, 1f);
        categoryRect.anchorMax = new Vector2(0f, 1f);
        categoryRect.pivot = new Vector2(0f, 1f);
        categoryText = HUDText.AddTo(categoryGO, TextAnchor.MiddleLeft);

        // 등급 스프라이트
        var tierGO = new GameObject("TierIcon");
        tierGO.transform.SetParent(panelRoot.transform, false);
        tierRect = tierGO.AddComponent<RectTransform>();
        tierRect.anchorMin = new Vector2(0f, 1f);
        tierRect.anchorMax = new Vector2(0f, 1f);
        tierRect.pivot = new Vector2(0f, 1f);
        tierImage = tierGO.AddComponent<Image>();
        tierImage.preserveAspect = true;
        tierImage.raycastTarget = false;

        // 능력치 목록
        var statGO = new GameObject("StatText");
        statGO.transform.SetParent(panelRoot.transform, false);
        statRect = statGO.AddComponent<RectTransform>();
        statRect.anchorMin = new Vector2(0f, 1f);
        statRect.anchorMax = new Vector2(0f, 1f);
        statRect.pivot = new Vector2(0f, 1f);
        statText = HUDText.AddTo(statGO, TextAnchor.UpperLeft, true);

        // 특수 효과 설명 (스토리 설명과 별개 - 아이템이 실제로 뭘 하는지)
        var specialEffectGO = new GameObject("SpecialEffectText");
        specialEffectGO.transform.SetParent(panelRoot.transform, false);
        specialEffectRect = specialEffectGO.AddComponent<RectTransform>();
        specialEffectRect.anchorMin = new Vector2(0f, 1f);
        specialEffectRect.anchorMax = new Vector2(0f, 1f);
        specialEffectRect.pivot = new Vector2(0f, 1f);
        specialEffectText = HUDText.AddTo(specialEffectGO, TextAnchor.UpperLeft, true);

        // 스토리 설명
        var descGO = new GameObject("DescriptionText");
        descGO.transform.SetParent(panelRoot.transform, false);
        descriptionRect = descGO.AddComponent<RectTransform>();
        descriptionRect.anchorMin = new Vector2(0f, 1f);
        descriptionRect.anchorMax = new Vector2(0f, 1f);
        descriptionRect.pivot = new Vector2(0f, 1f);
        descriptionText = HUDText.AddTo(descGO, TextAnchor.UpperLeft, true);

        panelRoot.SetActive(false); // 보여줄 아이템이 있을 때만 켜집니다.
    }

    // ------------------------------------------------------------------
    // 내용 채우기 / 배치
    // ------------------------------------------------------------------

    public void ShowItem(ItemDefinition item, ItemInfoPanelStyle style)
    {
        currentItem = item;

        if (item.icon != null)
        {
            iconImage.sprite = item.icon;
            iconImage.color = Color.white;
        }
        else
        {
            iconImage.sprite = null;
            iconImage.color = new Color(1f, 1f, 1f, 0.15f); // 아이콘이 아직 없는 아이템용 옅은 자리표시
        }

        nameText.text = item.displayName;
        currentTier = item.rarity;
        currentCategory = item.category;

        string statLine = FormatStatModifiers(item);
        statText.text = statLine;
        statText.gameObject.SetActive(!string.IsNullOrEmpty(statLine));

        string specialEffectLine = item.specialEffectDescription;
        specialEffectText.text = specialEffectLine;
        specialEffectText.gameObject.SetActive(!string.IsNullOrEmpty(specialEffectLine));

        descriptionText.text = item.description;

        panelRoot.SetActive(true);
    }

    // 같은 Canvas 안에서는 하이어라키상 아래(뒤)에 있는 자식일수록 화면에서 위에 그려집니다.
    // 다른 HUD가 이 패널보다 나중에 만들어지면 그쪽이 이 패널을 덮어버리므로, 패널이 떠 있는
    // 동안에는 항상 Canvas의 마지막 자식(가장 위)으로 유지하고 싶을 때 호출합니다.
    public void BringToFront()
    {
        var parent = panelRoot.transform.parent;
        if (parent == null) return;

        if (panelRoot.transform.GetSiblingIndex() != parent.childCount - 1)
        {
            panelRoot.transform.SetAsLastSibling();
        }
    }

    public void Hide()
    {
        currentItem = null;

        if (panelRoot != null && panelRoot.activeSelf)
        {
            panelRoot.SetActive(false);
        }
    }

    // 아이콘/이름/능력치/특수효과/설명 다섯 요소의 위치와 크기, 폰트/색상을 매 프레임 직접
    // 계산해서 넣습니다. Layout Group을 쓰지 않기 때문에 유니티의 자동 재배치와 경쟁할 일이
    // 없고, 계산 순서가 코드에 그대로 드러나 있어 결과를 예측하기 쉽습니다.
    public void Reflow(ItemInfoPanelStyle style)
    {
        float contentWidth = style.panelWidth - style.paddingLeft - style.paddingRight;
        float cursorY = -style.paddingTop; // 패널 안쪽 기준, 위에서 아래로 내려가며 쌓습니다.

        // ---- 헤더: 아이콘 + 이름 ----
        iconRect.sizeDelta = new Vector2(style.iconSize, style.iconSize);
        iconRect.anchoredPosition = new Vector2(style.paddingLeft, cursorY) + style.iconOffset;

        float nameWidth = Mathf.Max(0f, contentWidth - style.iconSize - style.iconToNameSpacing);
        nameText.fontSize = style.nameFontSize;
        nameText.color = GetNameColor(style, currentTier);
        nameRect.sizeDelta = new Vector2(nameWidth, nameRect.sizeDelta.y); // 높이 계산 전, 폭부터 확정
        float namePreferredHeight = LayoutUtility.GetPreferredHeight(nameRect);
        float nameHeight = Mathf.Max(style.iconSize, namePreferredHeight);
        nameRect.sizeDelta = new Vector2(nameWidth, nameHeight);
        nameRect.anchoredPosition = new Vector2(style.paddingLeft + style.iconSize + style.iconToNameSpacing, cursorY) + style.nameOffset;

        // ---- 분류 태그 + 등급 스프라이트 (이름 글자 바로 아래, 이름과 같은 열) ----
        float tierRowX = style.paddingLeft + style.iconSize + style.iconToNameSpacing;
        float tierTop = cursorY - namePreferredHeight - style.tierGapBelowName;

        categoryText.text = GetCategoryLabel(currentCategory);
        categoryText.fontSize = style.categoryFontSize;
        categoryText.color = style.categoryTextColor;
        float categoryWidth = LayoutUtility.GetPreferredWidth(categoryRect);
        categoryRect.sizeDelta = new Vector2(categoryWidth, style.tierIconSize.y);
        categoryRect.anchoredPosition = new Vector2(tierRowX, tierTop) + style.categoryOffset;

        ApplyTierSprite(style);
        tierRect.sizeDelta = style.tierIconSize;
        tierRect.anchoredPosition = new Vector2(tierRowX + categoryWidth + style.categoryToTierSpacing, tierTop) + style.tierOffset;
        float nameColumnHeight = namePreferredHeight + style.tierGapBelowName + style.tierIconSize.y;

        // 헤더 높이는 아이콘 / 이름 칸 / (이름 글자 + 등급 스프라이트) 중 가장 큰 값입니다.
        float headerHeight = Mathf.Max(Mathf.Max(style.iconSize, nameHeight), nameColumnHeight);
        cursorY -= headerHeight;

        // ---- 능력치 목록 (아이템에 능력치가 없으면 이 구역 자체를 건너뜁니다) ----
        if (statText.gameObject.activeSelf)
        {
            cursorY -= style.sectionSpacing;

            // 능력치 목록 색도 이름 색과 동일하게 등급(Tier0~3)에 따라 바뀝니다
            // (useTierNameColor가 꺼져 있으면 이름과 마찬가지로 nameTextColor 하나로 통일됩니다).
            statText.fontSize = style.statFontSize;
            statText.color = GetNameColor(style, currentTier);
            statRect.sizeDelta = new Vector2(contentWidth, statRect.sizeDelta.y);
            float statHeight = LayoutUtility.GetPreferredHeight(statRect);
            statRect.sizeDelta = new Vector2(contentWidth, statHeight);
            statRect.anchoredPosition = new Vector2(style.paddingLeft, cursorY) + style.statOffset;

            cursorY -= statHeight;
        }

        // ---- 특수 효과 설명 (아이템에 특수효과 설명이 없으면 이 구역 자체를 건너뜁니다) ----
        if (specialEffectText.gameObject.activeSelf)
        {
            // [능력치와 붙여 쓰기 - 형빈 요청] 바로 위에 능력치 목록이 있으면 구역 간격(sectionSpacing)을 두지 않고
            // 바로 이어 붙입니다 - 능력치 칸 높이가 이미 "줄 수 x 한 줄 높이"라, 같은 글자 크기면 Enter로 줄을 바꾼 것과
            // 같은 간격이 됩니다. 능력치가 없는 아이템은 예전처럼 헤더(아이콘/이름) 아래에 구역 간격을 둡니다.
            if (!statText.gameObject.activeSelf) cursorY -= style.sectionSpacing;

            // 특수 효과 설명 색은 이름 색과 동일하게 등급(Tier0~3)에 따라 바뀝니다
            // (useTierNameColor가 꺼져 있으면 이름과 마찬가지로 nameTextColor 하나로 통일됩니다).
            specialEffectText.fontSize = style.specialEffectFontSize;
            specialEffectText.color = GetNameColor(style, currentTier);
            specialEffectRect.sizeDelta = new Vector2(contentWidth, specialEffectRect.sizeDelta.y);
            float specialEffectHeight = LayoutUtility.GetPreferredHeight(specialEffectRect);
            specialEffectRect.sizeDelta = new Vector2(contentWidth, specialEffectHeight);
            specialEffectRect.anchoredPosition = new Vector2(style.paddingLeft, cursorY) + style.specialEffectOffset;

            cursorY -= specialEffectHeight;
        }

        // ---- 스토리 설명 ----
        cursorY -= style.sectionSpacing;

        descriptionText.fontSize = style.descriptionFontSize;
        descriptionText.color = style.descriptionTextColor;
        descriptionRect.sizeDelta = new Vector2(contentWidth, descriptionRect.sizeDelta.y);
        float descriptionHeight = LayoutUtility.GetPreferredHeight(descriptionRect);
        descriptionRect.sizeDelta = new Vector2(contentWidth, descriptionHeight);
        descriptionRect.anchoredPosition = new Vector2(style.paddingLeft, cursorY) + style.descriptionOffset;

        cursorY -= descriptionHeight;

        // ---- 패널 전체 높이 (최소 높이 보장) ----
        float totalContentHeight = style.paddingTop + (-cursorY) + style.paddingBottom;
        float panelHeight = Mathf.Max(style.panelMinHeight, totalContentHeight);

        panelRect.sizeDelta = new Vector2(style.panelWidth, panelHeight);
        ApplyPanelBackground(style);
    }

    // 배경 스프라이트가 있으면 9-slice(Sliced)로 그리고(색은 흰색 = 색 변형 없음), 없으면 단색 반투명 배경(panelColor)을
    // 모서리가 둥근 사각형(panelCornerRadius, 0이면 각진 사각형)으로 그립니다 - 둥근 사각형은 실행 중에 만든 흰색 둥근
    // 텍스처를 9-Slice로 늘려 색을 입히는 방식입니다(상점 창/교체 확인창과 같은 방식).
    private void ApplyPanelBackground(ItemInfoPanelStyle style)
    {
        if (style.backgroundSprite != null)
        {
            panelImage.sprite = style.backgroundSprite;
            panelImage.type = Image.Type.Sliced;
            panelImage.pixelsPerUnitMultiplier = 1f;
            panelImage.color = Color.white;
            return;
        }

        panelImage.color = style.panelColor;
        Vector2 size = panelRect.sizeDelta;
        float radius = Mathf.Min(style.panelCornerRadius, Mathf.Min(size.x, size.y) * 0.5f);
        if (radius < 0.5f)
        {
            panelImage.sprite = null;
            panelImage.type = Image.Type.Simple;
            return;
        }

        panelImage.sprite = GetRoundedSprite();
        panelImage.type = Image.Type.Sliced;
        panelImage.pixelsPerUnitMultiplier = RoundedTexRadius / radius;
    }

    // 둥근 사각형 배경용 흰색 텍스처/스프라이트 - 모든 설명칸(원래 설명칸 + 호버 툴팁)이 하나를 같이 씁니다.
    private const int RoundedTexSize = 96;
    private const int RoundedTexRadius = 32;
    private static Texture2D roundedTexture;
    private static Sprite roundedSprite;

    private static Sprite GetRoundedSprite()
    {
        if (roundedSprite != null) return roundedSprite; // Play 종료 등으로 파괴됐으면 null로 판정되어 다시 만듭니다

        int size = RoundedTexSize;
        float r = RoundedTexRadius;
        roundedTexture = new Texture2D(size, size, TextureFormat.RGBA32, false);
        roundedTexture.wrapMode = TextureWrapMode.Clamp;
        roundedTexture.filterMode = FilterMode.Bilinear;

        var pixels = new Color32[size * size];
        for (int y = 0; y < size; y++)
        {
            for (int x = 0; x < size; x++)
            {
                float cx = Mathf.Clamp(x + 0.5f, r, size - r);
                float cy = Mathf.Clamp(y + 0.5f, r, size - r);
                float dx = x + 0.5f - cx;
                float dy = y + 0.5f - cy;
                float alpha = Mathf.Clamp01(r - Mathf.Sqrt(dx * dx + dy * dy) + 0.5f);
                pixels[y * size + x] = new Color32(255, 255, 255, (byte)Mathf.RoundToInt(alpha * 255f));
            }
        }
        roundedTexture.SetPixels32(pixels);
        roundedTexture.Apply();

        roundedSprite = Sprite.Create(roundedTexture, new Rect(0f, 0f, size, size), new Vector2(0.5f, 0.5f),
                                      100f, 0, SpriteMeshType.FullRect, new Vector4(r, r, r, r));
        return roundedSprite;
    }

    // 지금 보여주는 아이템의 등급에 맞는 스프라이트를 이미지에 적용합니다.
    private void ApplyTierSprite(ItemInfoPanelStyle style)
    {
        Sprite sprite = GetTierSprite(style, currentTier);

        if (sprite != null)
        {
            tierImage.sprite = sprite;
            tierImage.color = Color.white;
        }
        else
        {
            tierImage.sprite = null;
            tierImage.color = style.tierPlaceholderColor;
        }
    }

    private Sprite GetTierSprite(ItemInfoPanelStyle style, ItemDefinition.ItemRarity rarity)
    {
        switch (rarity)
        {
            case ItemDefinition.ItemRarity.Tier0: return style.tier0Sprite;
            case ItemDefinition.ItemRarity.Tier1: return style.tier1Sprite;
            case ItemDefinition.ItemRarity.Tier2: return style.tier2Sprite;
            case ItemDefinition.ItemRarity.Tier3: return style.tier3Sprite;
            default: return null;
        }
    }

    // ItemDefinition.category -> 화면에 보여줄 "[패시브]" 형태의 분류 태그 문구.
    private string GetCategoryLabel(ItemDefinition.ItemCategory category)
    {
        switch (category)
        {
            case ItemDefinition.ItemCategory.Passive: return "[패시브]";
            case ItemDefinition.ItemCategory.Active: return "[액티브]";
            case ItemDefinition.ItemCategory.Trinket: return "[장신구]";
            case ItemDefinition.ItemCategory.Disposable: return "[일회용]";
            case ItemDefinition.ItemCategory.Food: return "[음식]";
            default: return string.Empty;
        }
    }

    // 지금 보여주는 아이템 이름에 쓸 색을 정합니다. 등급별 색을 끄면 nameTextColor 하나로 통일됩니다.
    private Color GetNameColor(ItemInfoPanelStyle style, ItemDefinition.ItemRarity rarity)
    {
        if (!style.useTierNameColor) return style.nameTextColor;

        switch (rarity)
        {
            case ItemDefinition.ItemRarity.Tier0: return style.tier0NameColor;
            case ItemDefinition.ItemRarity.Tier1: return style.tier1NameColor;
            case ItemDefinition.ItemRarity.Tier2: return style.tier2NameColor;
            case ItemDefinition.ItemRarity.Tier3: return style.tier3NameColor;
            default: return style.nameTextColor;
        }
    }

    // 예: "공격력 +0.1\n이동속도 -0.3\n장전 시간 -10%" (곱하기 효과 x0.9 -> -10%)
    private string FormatStatModifiers(ItemDefinition item)
    {
        if (item.statModifiers == null || item.statModifiers.Count == 0) return string.Empty;

        return string.Join("\n", item.statModifiers.Select(FormatSingleModifier));
    }

    private string FormatSingleModifier(StatModifier mod)
    {
        string statName = GetStatDisplayName(mod.statType);

        if (mod.modifierType == ModifierType.Additive)
        {
            // 화면 표시 배율/단위(총알 크기 x100, 확률 스탯 x100 + "%" 등)는 StatDisplayFormat 한 곳에서
            // 정합니다 - 좌하단 스탯 패널(PlayerStatDisplay)과 같은 규칙을 씁니다.
            return $"{statName} {StatDisplayFormat.FormatAdditive(mod.statType, mod.value)}";
        }

        // 곱하기 효과는 증감 퍼센트로 표시합니다(x1.2 -> +20%, x0.8 -> -20%, 형빈 확정).
        return $"{statName} {StatDisplayFormat.FormatMultiplicative(mod.value)}";
    }

    // StatType(영문 enum) -> 화면에 보여줄 한글 이름. StatType에 새 스탯이 추가되면
    // 여기도 같이 추가해주세요 (StatModifier.cs 상단 주석에도 같은 안내가 있습니다).
    private string GetStatDisplayName(StatType type)
    {
        switch (type)
        {
            case StatType.MaxHealth: return "최대 체력";
            case StatType.MoveSpeed: return "이동 속도";
            case StatType.AttackSpeed: return "공격 속도";
            case StatType.ReloadTime: return "장전 시간";
            case StatType.Luck: return "행운";
            case StatType.CostRecovery: return "코스트 회복";
            case StatType.Damage: return "공격력";
            case StatType.Range: return "사거리";
            case StatType.MagazineSize: return "탄창 크기";
            case StatType.BulletSize: return "총알 크기";
            case StatType.BulletSpeed: return "총알 속도";
            case StatType.Multishot: return "연사";
            case StatType.MultishotInterval: return "연사 간격";
            case StatType.GematriaChance: return "게마트리아 확률";
            case StatType.SchaleChance: return "샬레 확률";
            case StatType.GrenadeDamage: return "수류탄 데미지";
            case StatType.GrenadeSelfDamage: return "수류탄 자해 데미지";
            case StatType.GrenadeRadius: return "수류탄 폭발 반경";
            case StatType.GrenadeRange: return "수류탄 사거리";
            case StatType.HackDamage: return "해킹 데미지";
            case StatType.HackRadius: return "해킹 범위";
            case StatType.HackDuration: return "해킹 지속시간";
            default: return type.ToString();
        }
    }
}

// ItemInfoPanelView.Reflow/ShowItem에 매 프레임 건네주는 "지금 이 순간의 스타일 값" 묶음입니다.
// PlayerItemInfoDisplay.GetStyle()이 자신의 인스펙터 필드 값으로 이 객체를 채워서 반환하고,
// ItemTooltipDisplay도 같은 GetStyle()을 매 프레임 읽어서 쓰기 때문에, 두 설명칸(원래 설명칸 +
// 호버 툴팁)은 항상 같은 모양을 유지합니다. 인스펙터에 직접 노출되는 클래스가 아니라 그냥
// 프레임마다 만들어지는 값 전달용 객체입니다.
public class ItemInfoPanelStyle
{
    public float panelWidth;
    public float panelMinHeight;
    public Sprite backgroundSprite;
    public Color panelColor;
    public float panelCornerRadius;
    public float paddingLeft;
    public float paddingRight;
    public float paddingTop;
    public float paddingBottom;
    public float sectionSpacing;

    public float iconSize;
    public float iconToNameSpacing;
    public int nameFontSize;
    public bool useTierNameColor;
    public Color nameTextColor;
    public Color tier0NameColor;
    public Color tier1NameColor;
    public Color tier2NameColor;
    public Color tier3NameColor;

    public int categoryFontSize;
    public Color categoryTextColor;
    public float categoryToTierSpacing;

    public Sprite tier0Sprite;
    public Sprite tier1Sprite;
    public Sprite tier2Sprite;
    public Sprite tier3Sprite;
    public Vector2 tierIconSize;
    public float tierGapBelowName;
    public Color tierPlaceholderColor;

    // 능력치 목록의 색도 더 이상 별도 필드가 아니라 이름 색(GetNameColor)을 그대로
    // 씁니다 - ItemInfoPanelView.Reflow() 참고.
    public int statFontSize;

    // 특수 효과 설명의 색은 더 이상 별도 필드가 아니라 이름 색(GetNameColor)을 그대로
    // 씁니다 - ItemInfoPanelView.Reflow() 참고.
    public int specialEffectFontSize;

    public int descriptionFontSize;
    public Color descriptionTextColor;

    public Vector2 iconOffset;
    public Vector2 nameOffset;
    public Vector2 categoryOffset;
    public Vector2 tierOffset;
    public Vector2 statOffset;
    public Vector2 specialEffectOffset;
    public Vector2 descriptionOffset;
}
