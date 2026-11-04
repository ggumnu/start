using System.Collections.Generic;
using UnityEngine;

// 방 하나의 "외형 세트"(바닥 + 벽 + 부모 방 쪽 입구 벽)를 담는 에셋입니다(형빈 확정).
// 특수방 레이아웃 에셋(SpecialRoomLayout > Visual Set)에 연결하면, 그 레이아웃으로 뽑힌 특수방은 이 세트로 지어집니다.
// 레이아웃마다 외형이 달라도 LevelBuilder 인스펙터의 Room Wall Sets 목록을 늘릴 필요 없이, 레이아웃 에셋 하나만 보면
// 그 방의 모든 것(진입 조건/아이템 규칙/외형)을 확인할 수 있게 하려는 구조입니다.
//
// [만들기/연결]
// Project 창에서 Create > MobArchive > Room Visual Set으로 만들고, 특수방 레이아웃 에셋의 Visual Set 칸에 넣습니다.
// 같은 세트를 여러 레이아웃이 함께 써도 됩니다.
//
// [방 내용물은 바닥 프리팹 안에]
// 크레딧 더미, 장식, 상호작용 오브젝트 등 그 방의 내용물은 바닥 프리팹 안에 자식으로 직접 배치합니다(상점 바닥 프리팹에
// ShopNPC를 넣어 둔 것과 같은 방식). 바닥 프리팹은 방 콘텐츠(Content) 아래에 생성되므로, 안에 든 내용물은 자동으로
// 방에 들어올 때만 보이고(RoomVisibilityManager), 그 방 NavMesh 굽기에도 포함됩니다.
//
// [각 칸이 비어 있으면]
// 칸마다 따로 폴백합니다. 예를 들어 바닥만 채우고 벽은 비워 두면, 벽은 예전처럼 LevelBuilder > Room Wall Sets
// (이 레이아웃 ID와 같은 Variant Id → 없으면 Special 카테고리 세트) → 일반방 기본 벽 순서로 찾습니다.
// 칸을 채웠는데 어떤 방향 프리팹이 빠져 있으면 다른 세트로 넘어가지 않고 임시 큐브가 섭니다(빈 자리를 눈에 보이게
// 남기는 원칙) - 빌드할 때 어느 방향이 빠졌는지 경고 로그로 알려줍니다.
[CreateAssetMenu(fileName = "RoomVisualSet", menuName = "MobArchive/Room Visual Set")]
public class RoomVisualSet : ScriptableObject
{
    [Header("바닥 (방 내용물 포함)")]
    [Tooltip("이 방의 바닥 프리팹 후보입니다(가중치로 하나를 뽑음). 방 내용물(크레딧 더미, 장식 등)은 바닥 프리팹 안에 " +
             "자식으로 배치하세요. 비워 두면 Room Wall Sets / 일반방 기본 바닥을 씁니다.")]
    public List<WeightedPrefab> floorPool = new List<WeightedPrefab>();

    [Header("이 방의 벽")]
    [Tooltip("특수방 자신의 벽입니다(방향별 닫힌 벽/열린 벽). 특수방은 막다른 방이라 열린 벽은 부모 방 쪽 하나뿐이고, " +
             "나머지는 닫힌 벽입니다. 북<->서, 남<->동은 한쪽만 채워도 회전해서 함께 씁니다(기본 벽 규칙과 같음). " +
             "비워 두면 Room Wall Sets / 일반방 기본 벽을 씁니다.")]
    public List<DirectionalWallPool> wallPools = new List<DirectionalWallPool>();

    [Header("부모 방 쪽 입구 벽")]
    [Tooltip("부모 방(일반방/시작방)에서 이 특수방을 향한 벽에 쓸 벽입니다. Open Walls만 쓰입니다(Closed Walls는 무시). " +
             "방향은 '부모 방 기준' 방향입니다(특수방이 부모 방 북쪽에 붙으면 North 항목, 북<->서/남<->동 회전 재사용 규칙 동일). " +
             "벽 프리팹 루트의 WallSidePrefab에 전투 바리케이드(Barricade)와, 수류탄/해킹툴 레이아웃이면 특수방 잠금 바리케이드" +
             "(Special Lock Barricade)를 연결해 두세요. 비워 두면 LevelBuilder > Neighbor Facing Wall Sets의 Special 항목을 씁니다.")]
    public List<DirectionalWallPool> parentFacingWallPools = new List<DirectionalWallPool>();
}
