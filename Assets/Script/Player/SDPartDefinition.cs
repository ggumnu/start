using UnityEngine;

// SD 캐릭터의 교체 파츠가 들어가는 자리(슬롯)입니다.
// 같은 슬롯에는 파츠가 하나만 붙고, 새 파츠를 붙이면 이전 파츠는 떼어집니다.
public enum SDPartSlot
{
    Hair,           // 머리카락
    Hat,            // 모자
    HairAccessory,  // 머리 장신구 (머리끈, 머리핀 등)
    Torso,          // 몸통 (옷) - 알바디의 Body_Torso를 대신함
    Hands,          // 손 (장갑) - 알바디의 Body_Hand_L / Body_Hand_R을 대신함
    Feet,           // 발 (신발) - 알바디의 Body_Foot_L / Body_Foot_R을 대신함
    Back,           // 등 (날개, 가방 등)
}

// SD 캐릭터 교체 파츠 하나의 정의입니다(ScriptableObject).
//
// [만드는 법]
// Project 창 빈 곳 우클릭 -> Create -> MobArchive -> SD 캐릭터 -> 파츠 정의
// 슬롯을 고르고, 3ds Max에서 내보낸 파츠 FBX(예: SD_Hair_Reisa01)를 "파츠 모델"에 넣습니다.
//
// [파츠 FBX 조건]
// - 파츠 메시가 바이패드 본에 스키닝되어 있고, 바이패드 본 전체가 함께 들어 있어야 합니다.
//   (본 이름으로 캐릭터 뼈대에 다시 연결하기 때문입니다.)
// - 머리카락 흔들림용 본처럼 캐릭터 뼈대에 없는 본은, 파일 안에서의 부모 본(예: Bip001 Head) 아래에
//   그대로 옮겨 붙입니다. 그래서 알바디를 수정하지 않고도 전용 본을 가진 파츠를 추가할 수 있습니다.
// - 가져오기 설정의 Rig는 "Generic"으로 둡니다(Avatar Definition은 No Avatar로 둬도 됩니다).
//   None으로 두면 스키닝 정보가 빠져 파츠가 붙지 않을 수 있습니다.
//
// 나중에 아이템 외형 시스템이 생기면 아이템 데이터가 이 에셋을 가리키게 됩니다.
[CreateAssetMenu(fileName = "SDPart_", menuName = "MobArchive/SD 캐릭터/파츠 정의")]
public class SDPartDefinition : ScriptableObject
{
    [Tooltip("이 파츠가 들어갈 자리입니다. 같은 자리의 이전 파츠는 자동으로 떼어집니다.")]
    public SDPartSlot slot = SDPartSlot.Hair;

    [Tooltip("3ds Max에서 내보낸 파츠 FBX(또는 그 FBX로 만든 프리팹)입니다.")]
    public GameObject modelPrefab;

    [Tooltip("이 파츠를 붙일 때 추가로 숨길 알바디 오브젝트 이름입니다(예: Body_Head). 보통은 비워둡니다.")]
    public string[] extraHiddenBodyParts = new string[0];
}
