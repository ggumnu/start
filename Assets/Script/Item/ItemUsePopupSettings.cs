using UnityEngine;

// 일회용/음식 사용 연출 설정입니다(없어도 동작 - ItemUsePopup.cs 참고). Resources 폴더에 "ItemUsePopupSettings" 이름으로 두세요.
[CreateAssetMenu(fileName = "ItemUsePopupSettings", menuName = "MobArchive/Item Use Popup Settings")]
public class ItemUsePopupSettings : ScriptableObject
{
    [Tooltip("아이콘 크기/떠오르는 거리/시간/발광 크기/입자 수 등 연출 설정입니다(54번과 같은 항목). Icon 칸과 Glow Color/Sparkle Color는 " +
             "쓰이지 않습니다 - 아이콘은 사용한 아이템의 아이콘, 색은 그 아이템의 등급 색입니다.")]
    public CreditPopupSettings popup = new CreditPopupSettings();

    [Tooltip("발광의 진하기(알파)입니다. 색은 등급 색을 씁니다.")]
    [Range(0f, 1f)] public float glowAlpha = 0.85f;

    [Tooltip("빛 입자의 진하기(알파)입니다. 색은 등급 색을 씁니다.")]
    [Range(0f, 1f)] public float sparkleAlpha = 1f;
}
