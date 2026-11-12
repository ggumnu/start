using UnityEngine;

// SD 캐릭터 헤일로 하나의 정의입니다(ScriptableObject). SDHaloController가 이 에셋을 받아 헤일로 그림을 바꿉니다.
//
// [만드는 법]
// Project 창 빈 곳 우클릭 -> Create -> MobArchive -> SD 캐릭터 -> 헤일로 정의
// "그림"에 헤일로 PNG(Texture Type = Sprite (2D and UI))를 넣습니다. 헤일로마다 크기/위치가 다르면 아래 보정 칸을 씁니다.
// 나중에 아이템 외형 시스템이 생기면 아이템 데이터가 이 에셋을 가리키게 됩니다.
[CreateAssetMenu(fileName = "SDHalo_", menuName = "MobArchive/SD 캐릭터/헤일로 정의")]
public class SDHaloDefinition : ScriptableObject
{
    [Tooltip("헤일로 그림입니다(Texture Type = Sprite (2D and UI)).")]
    public Sprite sprite;

    [Tooltip("그림에 곱할 색입니다. 흰색 = 그림 그대로.")]
    public Color color = Color.white;

    [Tooltip("기본 자리(프리팹에 놓인 헤일로 위치)에서 옮길 거리(m)입니다. 축은 헤일로 그림 기준: X = 그림 오른쪽, Y = 그림 위쪽, Z = 그림을 뚫는 방향.")]
    public Vector3 positionOffset = Vector3.zero;

    [Tooltip("기본 크기(프리팹에 놓인 헤일로 크기)에 곱할 배율입니다. 1 = 그대로.")]
    [Min(0.01f)] public float scale = 1f;
}
