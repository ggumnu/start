using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Serialization;
using UnityEngine.UI;

// PlayerHUDManager가 만들어둔 HeartContainer 안에, 아이작 리펜던스 스타일의 체력 칸을
// 채우고 실시간으로 갱신하는 스크립트입니다. 매 프레임 PlayerHealth.CurrentHealth와
// PlayerStats.MaxHealth를 "읽기만" 해서 그립니다.
//
// [하트 1칸 = 체력 1, 반 칸 = 0.5, 빈 칸 = 최대체력 상 남아있지만 현재 0]
// 형빈님 기획대로 스프라이트 3종(full/half/empty)만 사용합니다.
//
// [최대 체력이 어중간한 값(예: 6.5)일 때 처리]
// 체력 관련 아이템은 전부 0.5 단위 가감(곱연산 배율 없음)으로 만들기로 합의했기 때문에,
// MaxHealth는 항상 0.5의 배수입니다. 다만 그 결과 "마지막 한 칸"이 딱 0.5만큼만 채워질
// 수 있는 칸이 될 수 있습니다(예: MaxHealth 6.5 -> 7칸인데 7번째 칸은 최대 0.5까지만
// 채워짐). 아래 로직은 "이 칸에서 채워진 양"만 계산해서 0.5 단위로 반올림한 뒤
// (>=1 -> full, >=0.5 -> half, 그 외 -> empty) 판정하기 때문에, 이 마지막 칸이 실제로는
// 0.5가 한계여도 자동으로 half/empty 사이에서만 표시되고 절대 full로 표시되지 않습니다.
//
// [칸 폭 줄이기 - 체력 UI 개편, 형빈 확정 (예전 두 줄 표시를 대체)]
// 최대 체력은 PlayerStats.maxHealthCap(기본 10)을 넘지 않고, 체력 칸은 항상 한 줄입니다.
// - 칸이 fullSizeSlotCount(기본 5)개 이하이면 원래 크기(heartWidth) 칸이 왼쪽부터 하나씩 늘어납니다.
// - 6칸 이상이면(최대 체력 5.5도 6칸) 전체 폭은 "원래 크기 5칸 너비" 그대로 두고, 칸 하나의 폭만 줄여서 딱 맞게 채웁니다.
//   칸 사이 간격(heartSpacing)과 칸 높이는 그대로입니다(형빈 확정). 예) 10칸이면 칸 폭이 약 절반.
// - 칸 묶음의 세로 중심은 HeartContainer 세로 중앙입니다.
// - HeartContainer의 자동 가로 정렬(HorizontalLayoutGroup)에서 빠지는 묶음(HeartBlock)을 하나 만들고 칸 위치를 직접 계산합니다.
//   PlayerHUDManager는 고치지 않았습니다.
//
// [9-slice - 테두리 두께 고정]
// 칸 스프라이트는 흰 테두리가 그려진 그림이라, 높이만 반으로 줄이면 위아래 테두리만 얇아집니다. 그래서 스프라이트에
// 9-slice 테두리 값(Sprite Editor의 Border, 3종 모두 8px)을 넣고 Image를 Sliced로 그려서, 칸 크기와 상관없이 테두리 두께가
// heartBorderThickness로 일정하게 보이도록 합니다. 스프라이트에 Border 값이 없으면 예전처럼 그냥 늘여서 그립니다.
//
// [보호막 바 - 형빈 확정]
// 보호막(PlayerHealth.CurrentShield)이 1개 이상이면 체력 칸 줄 위에 하늘색 바를 그립니다.
// - 너비는 체력 칸 줄 너비에 딱 맞습니다(최대 체력 3이면 3칸 너비, 5칸 이상이면 항상 5칸 너비). 칸은 그대로 있고 바만 위에 붙습니다.
// - 보호막이 1개면 바 하나가 꽉 차고, 2개 이상이면 흰 세로선으로 개수만큼 고르게 나눕니다. 바 둘레에도 흰 테두리를 두릅니다.
// - 바 그림(UI_PlayerShield_Full)은 긴 삼각무늬라, 바가 그림보다 짧으면 늘이지 않고 왼쪽부터 잘라 써서 무늬가 찌그러지지 않습니다.
[RequireComponent(typeof(RectTransform))]
public class PlayerHeartDisplay : MonoBehaviour
{
    [Header("하트 스프라이트 (3종 필수)")]
    public Sprite fullHeartSprite;
    public Sprite halfHeartSprite;
    public Sprite emptyHeartSprite;

    [Header("스프라이트를 아직 안 만들었을 때 보여줄 대체 색상 (참고용, 스프라이트 넣으면 무시됨)")]
    public Color fallbackFullColor = new Color(0.85f, 0.15f, 0.15f, 1f);
    public Color fallbackHalfColor = new Color(0.85f, 0.15f, 0.15f, 0.5f);
    public Color fallbackEmptyColor = new Color(0.3f, 0.1f, 0.1f, 0.4f);

    [Header("참조 (비워두면 씬에서 자동으로 찾습니다)")]
    public PlayerHUDManager hudManager;
    public PlayerHealth playerHealth;

    [Header("체력 칸 크기/간격 (기준 해상도 1920x1080 px, Play 중에도 즉시 반영됩니다)")]
    [Tooltip("체력 한 칸의 가로 길이입니다. 체력바 조각 스프라이트는 가로:세로 2:1로 만들어져 있습니다.")]
    public float heartWidth = 64f;
    [Tooltip("체력 한 칸의 세로 길이입니다(칸 수와 상관없이 항상 이 높이).")]
    public float heartHeight = 32f;
    [Tooltip("같은 줄에서 칸과 칸 사이의 가로 간격입니다.")]
    public float heartSpacing = 6f;

    [Header("칸 폭 줄이기")]
    [Tooltip("원래 크기로 보여줄 최대 칸 수입니다(기본 5). 칸이 이보다 많으면 전체 폭은 이 칸 수만큼의 너비로 두고 칸 하나의 폭을 줄입니다.")]
    [FormerlySerializedAs("heartsPerRow")]
    [Min(1)] public int fullSizeSlotCount = 5;

    [Header("칸 테두리 (스프라이트에 9-slice Border가 설정돼 있을 때만 적용)")]
    [Tooltip("칸의 흰 테두리 두께(px)입니다. 칸 크기와 상관없이 이 두께로 일정하게 그려집니다. " +
             "기본 3.4 = 예전(64x32 칸에 스프라이트를 그대로 줄여 그렸을 때)과 같은 두께.")]
    [Min(0.1f)] public float heartBorderThickness = 3.4f;

    [Header("보호막 바")]
    [Tooltip("보호막 바 그림입니다(UI_PlayerShield_Full). 비워두면 아래 대체 색 단색으로 그립니다.")]
    public Sprite shieldSprite;
    [Tooltip("보호막 그림이 없을 때 쓰는 색입니다(UI_Spacebar 왼쪽 하늘색).")]
    public Color shieldFallbackColor = new Color(102f / 255f, 204f / 255f, 1f, 1f);
    [Tooltip("보호막 바 높이(px, 흰 테두리 포함)입니다.")]
    public float shieldBarHeight = 16f;
    [Tooltip("윗줄 칸 위쪽 끝과 보호막 바 아래쪽 끝 사이 간격입니다. 음수로 하면 칸 테두리와 겹쳐 붙습니다.")]
    public float shieldGap = 0f;
    [Tooltip("보호막 바 둘레 흰 테두리와 칸을 나누는 흰 세로선의 두께(px)입니다.")]
    [Min(0f)] public float shieldBorderThickness = 3.4f;
    public Color shieldBorderColor = Color.white;

    private PlayerStats playerStats;
    private RectTransform heartContainer;

    // 보호막 바 오브젝트들(처음 필요할 때 만듦)
    private RectTransform shieldRoot;
    private Image shieldFrame;
    private RectTransform shieldFillRect;
    private RawImage shieldFill;
    private readonly List<Image> shieldDividers = new List<Image>();
    private RectTransform heartBlock;
    private readonly List<Image> heartImages = new List<Image>();

    // 지금 그려진 칸 묶음의 맨 위 y(HeartBlock 기준, 세로 중심 = 0)와 너비입니다. 보호막 바처럼 칸 묶음 위에 붙는 UI가 씁니다.
    public float RowsTopY { get; private set; }
    public float TopRowWidth { get; private set; }

    void Update()
    {
        if (!EnsureReferences()) return;

        float maxHealth = playerStats.MaxHealth;
        float currentHealth = playerHealth.CurrentHealth;

        // [테스트 - 체력 상한 해제] 상한을 넘는 최대 체력이어도 칸은 상한(10)까지만 그립니다(넘는 체력은 꽉 찬 칸으로 보임).
        if (playerStats.maxHealthCap > 0f) maxHealth = Mathf.Min(maxHealth, playerStats.maxHealthCap);
        int slotCount = CalculateSlotCount(maxHealth);
        EnsureSlotCount(slotCount);

        // 부동소수점 오차 방지를 위해 0.5 단위로 한 번 반올림하고 시작합니다.
        float roundedCurrent = Mathf.Round(currentHealth * 2f) / 2f;

        LayoutSlots(slotCount);

        for (int i = 0; i < slotCount; i++)
        {
            float filled = Mathf.Clamp(roundedCurrent - i, 0f, 1f);
            ApplyHeartState(heartImages[i], filled);
        }

        UpdateShieldBar(slotCount);
    }

    // ---------------- 보호막 바 ----------------

    private void UpdateShieldBar(int slotCount)
    {
        // 보호막 바는 원래 최대 개수(maxShield, 5)칸까지만 나눠 그립니다(테스트용 상한 해제로 더 많아도 5칸으로 보임).
        int shield = Mathf.Min(playerHealth.CurrentShield, Mathf.Max(0, playerHealth.maxShield));
        bool show = shield > 0 && slotCount > 0 && TopRowWidth > 0f;

        if (!show)
        {
            if (shieldRoot != null && shieldRoot.gameObject.activeSelf) shieldRoot.gameObject.SetActive(false);
            return;
        }

        EnsureShieldBar();
        if (!shieldRoot.gameObject.activeSelf) shieldRoot.gameObject.SetActive(true);

        // 보호막 바는 항상 체력 칸보다 앞(위)에 그려지도록 맨 마지막 자식으로 둡니다. 최대 체력이 늘어 칸이 새로 만들어지면 그 칸이
        // 맨 뒤에 붙어 보호막 바를 덮어버리므로, 매 프레임 확인해서 순서가 바뀌었을 때만 다시 맨 뒤로 옮깁니다(형빈 확정).
        Transform parent = shieldRoot.parent;
        if (parent != null && shieldRoot.GetSiblingIndex() != parent.childCount - 1) shieldRoot.SetAsLastSibling();

        float width = TopRowWidth;
        float height = Mathf.Max(1f, shieldBarHeight);
        float t = Mathf.Clamp(shieldBorderThickness, 0f, height * 0.5f);

        // 바 아래쪽 끝 = 윗줄 위쪽 끝 + 간격
        shieldRoot.anchoredPosition = new Vector2(0f, RowsTopY + shieldGap);
        shieldRoot.sizeDelta = new Vector2(width, height);

        // 흰 테두리 = 바 전체를 흰색으로 칠한 뒤, 안쪽을 그림으로 덮습니다.
        shieldFrame.color = shieldBorderColor;

        float innerWidth = Mathf.Max(0f, width - t * 2f);
        float innerHeight = Mathf.Max(0f, height - t * 2f);
        shieldFillRect.anchoredPosition = new Vector2(t, -t);
        shieldFillRect.sizeDelta = new Vector2(innerWidth, innerHeight);
        ApplyShieldFill(innerWidth, innerHeight);

        // 개수 - 1개만큼 흰 세로선으로 고르게 나눕니다.
        int dividerCount = shield - 1;
        while (shieldDividers.Count < dividerCount) shieldDividers.Add(CreateShieldDivider());
        for (int k = 0; k < shieldDividers.Count; k++)
        {
            var divider = shieldDividers[k];
            bool active = k < dividerCount && t > 0f;
            if (divider.gameObject.activeSelf != active) divider.gameObject.SetActive(active);
            if (!active) continue;

            float centerX = t + innerWidth * (k + 1) / shield;
            var rect = divider.rectTransform;
            rect.anchoredPosition = new Vector2(centerX - t * 0.5f, 0f);
            rect.sizeDelta = new Vector2(t, height);
            divider.color = shieldBorderColor;
        }
    }

    // 바 그림을 안쪽 칸에 채웁니다. 바가 그림보다 가로로 짧으면 그림 왼쪽부터 그 비율만큼만 잘라 써서(uvRect) 무늬가 안 찌그러지게 하고,
    // 더 길면 그냥 늘입니다.
    private void ApplyShieldFill(float innerWidth, float innerHeight)
    {
        if (shieldSprite == null || shieldSprite.texture == null)
        {
            shieldFill.texture = null;
            shieldFill.color = shieldFallbackColor;
            shieldFill.uvRect = new Rect(0f, 0f, 1f, 1f);
            return;
        }

        Texture tex = shieldSprite.texture;
        Rect r = shieldSprite.textureRect;
        float u0 = r.x / tex.width, v0 = r.y / tex.height;
        float uw = r.width / tex.width, vh = r.height / tex.height;

        float spriteAspect = r.height > 0f ? r.width / r.height : 1f;
        float barAspect = innerHeight > 0f ? innerWidth / innerHeight : spriteAspect;
        if (barAspect < spriteAspect) uw *= barAspect / spriteAspect;

        shieldFill.texture = tex;
        shieldFill.color = Color.white;
        shieldFill.uvRect = new Rect(u0, v0, uw, vh);
    }

    private void EnsureShieldBar()
    {
        if (shieldRoot != null) return;

        var rootGO = new GameObject("ShieldBar");
        rootGO.transform.SetParent(heartBlock, false);
        shieldRoot = rootGO.AddComponent<RectTransform>();
        shieldRoot.anchorMin = shieldRoot.anchorMax = new Vector2(0f, 0.5f);
        shieldRoot.pivot = new Vector2(0f, 0f); // 아래쪽 왼쪽 기준 - 윗줄 위에 얹힘

        shieldFrame = rootGO.AddComponent<Image>();
        shieldFrame.raycastTarget = false;

        var fillGO = new GameObject("Fill");
        fillGO.transform.SetParent(rootGO.transform, false);
        shieldFillRect = fillGO.AddComponent<RectTransform>();
        shieldFillRect.anchorMin = shieldFillRect.anchorMax = new Vector2(0f, 1f);
        shieldFillRect.pivot = new Vector2(0f, 1f);
        shieldFill = fillGO.AddComponent<RawImage>();
        shieldFill.raycastTarget = false;
    }

    private Image CreateShieldDivider()
    {
        var go = new GameObject("Divider_" + shieldDividers.Count);
        go.transform.SetParent(shieldRoot, false);
        var rect = go.AddComponent<RectTransform>();
        rect.anchorMin = rect.anchorMax = new Vector2(0f, 0f);
        rect.pivot = new Vector2(0f, 0f);
        var image = go.AddComponent<Image>();
        image.raycastTarget = false;
        return image;
    }

    // 칸 위치/크기를 매 프레임 계산합니다(Play 중 인스펙터 값을 바꿔도 바로 반영 - 수치 대입뿐이라 부담 없음).
    // 항상 한 줄이고, 세로 중심이 HeartBlock의 y = 0(= HeartContainer 세로 중앙)에 옵니다.
    // fullSizeSlotCount칸 이하 = 원래 폭, 그보다 많으면 원래 폭 fullSizeSlotCount칸 너비에 맞춰 칸 폭만 줄임(간격은 그대로).
    private void LayoutSlots(int slotCount)
    {
        int fullCount = Mathf.Max(1, fullSizeSlotCount);
        float maxRowWidth = fullCount * heartWidth + (fullCount - 1) * heartSpacing;

        float slotWidth = heartWidth;
        if (slotCount > fullCount)
        {
            slotWidth = (maxRowWidth - (slotCount - 1) * heartSpacing) / slotCount;
            slotWidth = Mathf.Max(1f, slotWidth);
        }

        Vector2 slotSize = new Vector2(slotWidth, heartHeight);
        for (int i = 0; i < slotCount; i++)
        {
            RectTransform rect = heartImages[i].rectTransform;
            rect.sizeDelta = slotSize;
            rect.anchoredPosition = new Vector2(i * (slotWidth + heartSpacing), 0f);
        }

        RowsTopY = slotCount > 0 ? heartHeight * 0.5f : 0f;
        TopRowWidth = slotCount > 0 ? slotCount * slotWidth + (slotCount - 1) * heartSpacing : 0f;
    }

    // hudManager / playerHealth / playerStats / heartContainer를 찾을 때까지 매 프레임
    // 조용히 재시도합니다 (ItemPedestal이 PlayerInventory를 찾는 방식과 동일한 패턴).
    private bool EnsureReferences()
    {
        if (hudManager == null)
        {
            hudManager = Object.FindFirstObjectByType<PlayerHUDManager>();
            if (hudManager == null) return false;
        }

        if (heartContainer == null)
        {
            heartContainer = hudManager.HeartContainer;
            if (heartContainer == null) return false;
        }

        if (playerHealth == null)
        {
            playerHealth = Object.FindFirstObjectByType<PlayerHealth>();
            if (playerHealth == null) return false;
        }

        if (playerStats == null)
        {
            // PlayerHealth는 [RequireComponent(typeof(PlayerStats))]라서 항상 같은
            // 오브젝트에 PlayerStats가 붙어 있습니다.
            playerStats = playerHealth.GetComponent<PlayerStats>();
            if (playerStats == null) return false;
        }

        EnsureHeartBlock();
        return true;
    }

    // HeartContainer의 자동 가로 정렬에서 빠지는 묶음을 하나 만듭니다(칸 위치는 LayoutSlots가 직접 계산).
    // HeartContainer의 왼쪽 세로 중앙에 붙습니다.
    private void EnsureHeartBlock()
    {
        if (heartBlock != null) return;

        var go = new GameObject("HeartBlock");
        go.transform.SetParent(heartContainer, false);
        heartBlock = go.AddComponent<RectTransform>();
        heartBlock.anchorMin = heartBlock.anchorMax = new Vector2(0f, 0.5f);
        heartBlock.pivot = new Vector2(0f, 0.5f);
        heartBlock.anchoredPosition = Vector2.zero;
        heartBlock.sizeDelta = Vector2.zero;

        var ignore = go.AddComponent<LayoutElement>();
        ignore.ignoreLayout = true;
    }

    // 다른 UI(보호막 바 등)가 칸 묶음과 같은 기준점에 붙을 수 있도록 묶음을 공개합니다(아직 없으면 null).
    public RectTransform HeartBlock => heartBlock;

    // MaxHealth(0.5 단위)를 하트 칸 개수로 변환합니다.
    // 예: 6 -> 6칸, 6.5 -> 7칸(마지막 칸은 0.5 용량), 5.5 -> 6칸
    private int CalculateSlotCount(float maxHealth)
    {
        int halfSteps = Mathf.RoundToInt(maxHealth * 2f);
        return Mathf.Max(0, (halfSteps + 1) / 2);
    }

    // 칸 개수를 slotCount에 맞춰 늘리거나 줄입니다.
    // 아이템으로 최대 체력이 바뀌는 순간에만 실제로 개수가 바뀌고, 평소에는 그대로입니다.
    private void EnsureSlotCount(int slotCount)
    {
        while (heartImages.Count < slotCount)
        {
            heartImages.Add(CreateHeartImage());
        }

        while (heartImages.Count > slotCount)
        {
            int lastIndex = heartImages.Count - 1;
            if (heartImages[lastIndex] != null)
            {
                Destroy(heartImages[lastIndex].gameObject);
            }
            heartImages.RemoveAt(lastIndex);
        }
    }

    private Image CreateHeartImage()
    {
        var go = new GameObject("Heart_" + heartImages.Count);
        go.transform.SetParent(heartBlock, false);

        var rect = go.AddComponent<RectTransform>();
        rect.anchorMin = rect.anchorMax = new Vector2(0f, 0.5f);
        rect.pivot = new Vector2(0f, 0.5f);
        rect.sizeDelta = new Vector2(heartWidth, heartHeight);

        var image = go.AddComponent<Image>();
        image.preserveAspect = false;
        image.raycastTarget = false;

        return image;
    }

    // filled: 이 칸이 채워진 정도 (0 ~ 1). 0.5 단위로만 들어온다고 가정합니다.
    private void ApplyHeartState(Image image, float filled)
    {
        if (filled >= 0.99f)
        {
            SetHeart(image, fullHeartSprite, fallbackFullColor);
        }
        else if (filled >= 0.49f)
        {
            SetHeart(image, halfHeartSprite, fallbackHalfColor);
        }
        else
        {
            SetHeart(image, emptyHeartSprite, fallbackEmptyColor);
        }
    }

    private void SetHeart(Image image, Sprite sprite, Color fallbackColor)
    {
        if (sprite != null)
        {
            image.sprite = sprite;
            image.color = Color.white;
            ApplySlicing(image, sprite);
        }
        else
        {
            // 스프라이트를 아직 안 만들었을 때: 색상만으로 구분되게 임시 표시합니다.
            // 나중에 스프라이트 3종을 인스펙터에 꽂으면 자동으로 교체됩니다.
            image.sprite = null;
            image.type = Image.Type.Simple;
            image.color = fallbackColor;
        }
    }

    // 스프라이트에 9-slice Border가 있으면 Sliced로 그리고, 테두리가 heartBorderThickness(px)로 보이도록 배율을 맞춥니다.
    // 화면에 보이는 테두리 두께 = Border(px) x (캔버스 기준 PPU / 스프라이트 PPU) / pixelsPerUnitMultiplier 이므로 거꾸로 계산합니다.
    private void ApplySlicing(Image image, Sprite sprite)
    {
        Vector4 border = sprite.border;
        float borderPx = Mathf.Max(Mathf.Max(border.x, border.y), Mathf.Max(border.z, border.w));
        if (borderPx <= 0f)
        {
            if (image.type != Image.Type.Simple) image.type = Image.Type.Simple;
            return;
        }

        if (image.type != Image.Type.Sliced) image.type = Image.Type.Sliced;
        image.fillCenter = true;

        float referencePpu = image.canvas != null ? image.canvas.referencePixelsPerUnit : 100f;
        float spritePpu = sprite.pixelsPerUnit > 0f ? sprite.pixelsPerUnit : 100f;
        float multiplier = borderPx * referencePpu / (spritePpu * Mathf.Max(heartBorderThickness, 0.1f));
        image.pixelsPerUnitMultiplier = Mathf.Max(0.01f, multiplier);
    }
}
