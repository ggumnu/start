using System.Collections.Generic;
using UnityEngine;

// n.5 레벨(선택방 레벨)의 슬롯별 후보 풀을 담는 설정 데이터입니다.
// RoomGenerationConfig의 storeVariants/consumptionVariants 등과 동일한 패턴입니다.
//
// 지금은 서쪽 슬롯 = 게마트리아, 동쪽 슬롯 = 샬레로 사실상 고정되어 있지만(각 리스트에 항목 1개),
// 나중에 같은 자리에 다른 후보를 추가하고 싶다면 이 에셋의 리스트에 RoomVariant를
// 하나 더 추가하기만 하면 됩니다 (코드 수정 불필요, 가중치 기반으로 자동 랜덤 선택됨).
//
// 위/아래 슬롯이 추가로 필요해지면, 이 클래스에 northVariants/southVariants를
// 같은 방식으로 추가하고 ChoiceLevelGenerator에서 동일한 패턴으로 처리하면 됩니다.
// 배경 재질(backgroundMaterial)은 GameFlowManager가 n.5 레벨을 만들 때 LevelBackground로 적용합니다.
[CreateAssetMenu(fileName = "ChoiceLevelConfig", menuName = "MobArchive/Choice Level Config")]
public class ChoiceLevelConfig : ScriptableObject
{
    [Header("서쪽 슬롯 후보 풀 (현재 기본: 게마트리아)")]
    [Tooltip("확률 판정에 성공하면 이 풀에서 가중치 기반으로 하나를 뽑아 서쪽에 배치합니다.")]
    public List<RoomVariant> westVariants;

    [Header("동쪽 슬롯 후보 풀 (현재 기본: 샬레)")]
    [Tooltip("확률 판정에 성공하면 이 풀에서 가중치 기반으로 하나를 뽑아 동쪽에 배치합니다.")]
    public List<RoomVariant> eastVariants;

    [Header("배경 (비워두면 씬의 원래 배경 - 1레벨 파란 하늘)")]
    [Tooltip("n.5 레벨의 화면 배경 재질(ScreenBackground 셰이더). 재질의 초상화 칸 색으로 HUD 초상화 하늘도 바뀝니다.")]
    public Material backgroundMaterial;
}