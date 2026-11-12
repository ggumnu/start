using UnityEngine;

// SD 캐릭터가 손에 드는 총 하나의 정의입니다(ScriptableObject). SDGunController가 이 에셋을 받아 총을 바꿉니다.
//
// [만드는 법]
// 1) 총 FBX를 SD_Player의 오른손 뼈 아래에 놓고 손에 맞게 위치/회전/크기를 맞춘 뒤, 총 아래에 Create Empty로
//    "MuzzleDirection"(총구 방향 표시 - 파란 Z축 = 총열 방향, 총구 끝 위치)를 만듭니다.
// 2) 그 총 오브젝트를 Project 창으로 끌어다 놓아 총 프리팹을 만듭니다(Original Prefab).
//    프리팹 맨 위 오브젝트의 위치/회전/크기 = 손 뼈 기준 자리 그대로 쓰입니다.
// 3) Project 창 빈 곳 우클릭 -> Create -> MobArchive -> SD 캐릭터 -> 총 정의, "총 프리팹"에 2)의 프리팹을 넣습니다.
// 나중에 아이템 외형 시스템이 생기면 아이템 데이터가 이 에셋을 가리키게 됩니다.
[CreateAssetMenu(fileName = "SDGun_", menuName = "MobArchive/SD 캐릭터/총 정의")]
public class SDGunDefinition : ScriptableObject
{
    [Tooltip("손 뼈 아래에 놓을 총 프리팹입니다. 맨 위 오브젝트의 위치/회전/크기가 손 뼈 기준 자리로 그대로 쓰입니다.")]
    public GameObject gunPrefab;

    [Tooltip("총 프리팹 안의 총구 방향 표시 오브젝트 이름입니다(파란 Z축 = 총열 방향). 조준할 때 총구를 정면으로 맞추는 기준(HumanoidAimLookAt)으로 씁니다.")]
    public string muzzleObjectName = "MuzzleDirection";

    [Tooltip("총알이 나오는 위치(플레이어의 FirePoint)를 기본 자리에서 옮길 거리(m, FirePoint의 부모 기준 X/Y/Z)입니다. 총 길이가 지금 총과 비슷하면 0으로 둡니다.")]
    public Vector3 firePointOffset = Vector3.zero;
}
