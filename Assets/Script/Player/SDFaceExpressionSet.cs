using System.Collections.Generic;
using UnityEngine;

// SD 캐릭터의 표정 종류입니다. 게임 상황(피격, 아이템 획득 등)과 1:1로 대응합니다.
// 새 표정이 필요하면 여기에 이름을 추가하고, 표정 세트 에셋에 칸 번호를 넣으면 됩니다.
public enum SDFaceExpression
{
    Default,     // 기본 (평소)
    Hit,         // 피격 (피격 직후 ~ 무적 시간 동안)
    ItemPickup,  // 플레이어 아이템 획득 (획득 애니메이션 동안)
    Reload,      // 장전 (장전 애니메이션 동안)
    Death,       // 사망 (사망 이후 계속)
    Run,         // 비전투 뛰기 (뛰기 모션 동안) - 에셋에 숫자로 저장되므로 새 표정은 항상 맨 끝에 추가
}

// SD 캐릭터 표정 세트(ScriptableObject)입니다. "표정 -> 눈 칸 / 입 칸" 대응표와 눈 깜빡임 설정을 담습니다.
//
// [만드는 법]
// Project 창 빈 곳 우클릭 -> Create -> MobArchive -> SD 캐릭터 -> 표정 세트
// 새로 만들면 아래 기본 표정표가 자동으로 채워집니다(형빈 님이 정한 값).
//
//   표정          눈 칸  입 칸  우선순위  깜빡임
//   기본            1     1       0       O
//   뛰기            4     1       5       X
//   장전            3     1      10       X
//   아이템 획득     4     4      20       X
//   피격            5     5      30       X
//   사망            5     5     100       X
//   깜빡임: 눈 2번 칸 (입은 그때 표정의 입 그대로 = 기본 표정이면 입 1번)
//
// [우선순위]
// 표정이 동시에 여러 개 요청되면(예: 장전 중에 피격) 우선순위 숫자가 가장 큰 표정이 보입니다.
// 높은 표정이 끝나면 아직 남아 있는 다음 표정으로 자동으로 돌아갑니다.
[CreateAssetMenu(fileName = "SDFaceSet_", menuName = "MobArchive/SD 캐릭터/표정 세트")]
public class SDFaceExpressionSet : ScriptableObject
{
    [System.Serializable]
    public class Entry
    {
        [Tooltip("어떤 상황의 표정인지")]
        public SDFaceExpression expression;

        [Tooltip("눈 시트에서 쓸 칸 번호 (1~9, 왼쪽 위부터)")]
        [Range(1, 9)] public int eyeCell = 1;

        [Tooltip("입 시트에서 쓸 칸 번호 (1~9, 왼쪽 위부터)")]
        [Range(1, 9)] public int mouthCell = 1;

        [Tooltip("여러 표정이 겹칠 때 숫자가 큰 표정이 보입니다.")]
        public int priority;

        [Tooltip("이 표정일 때 눈을 깜빡일지 (눈 모양이 정해진 표정은 끕니다)")]
        public bool allowBlink;
    }

    [Tooltip("표정별 눈/입 칸 번호입니다. 목록에 없는 표정은 기본 표정으로 보입니다.")]
    public List<Entry> expressions = new List<Entry>();

    [Header("눈 깜빡임")]
    [Tooltip("감은 눈으로 쓸 눈 시트 칸 번호")]
    [Range(1, 9)] public int blinkEyeCell = 2;

    [Tooltip("깜빡임 사이 간격의 최소값 (초)")]
    public float blinkIntervalMin = 2f;

    [Tooltip("깜빡임 사이 간격의 최대값 (초). 최소~최대 사이에서 무작위로 정해집니다.")]
    public float blinkIntervalMax = 5f;

    [Tooltip("눈을 감고 있는 시간 (초)")]
    public float blinkDuration = 0.1f;

    // 그 표정의 설정을 찾습니다. 목록에 없으면 null.
    public Entry Find(SDFaceExpression expression)
    {
        for (int i = 0; i < expressions.Count; i++)
        {
            if (expressions[i] != null && expressions[i].expression == expression) return expressions[i];
        }
        return null;
    }

    // 에셋을 처음 만들 때(또는 인스펙터 ⋮ 메뉴의 Reset) 기본 표정표를 채웁니다.
    void Reset()
    {
        expressions = new List<Entry>
        {
            new Entry { expression = SDFaceExpression.Default,    eyeCell = 1, mouthCell = 1, priority = 0,   allowBlink = true },
            new Entry { expression = SDFaceExpression.Run,        eyeCell = 4, mouthCell = 1, priority = 5,   allowBlink = false },
            new Entry { expression = SDFaceExpression.Reload,     eyeCell = 3, mouthCell = 1, priority = 10,  allowBlink = false },
            new Entry { expression = SDFaceExpression.ItemPickup, eyeCell = 4, mouthCell = 4, priority = 20,  allowBlink = false },
            new Entry { expression = SDFaceExpression.Hit,        eyeCell = 5, mouthCell = 5, priority = 30,  allowBlink = false },
            new Entry { expression = SDFaceExpression.Death,      eyeCell = 5, mouthCell = 5, priority = 100, allowBlink = false },
        };
        blinkEyeCell = 2;
        blinkIntervalMin = 2f;
        blinkIntervalMax = 5f;
        blinkDuration = 0.1f;
    }

    void OnValidate()
    {
        if (blinkIntervalMin < 0.1f) blinkIntervalMin = 0.1f;
        if (blinkIntervalMax < blinkIntervalMin) blinkIntervalMax = blinkIntervalMin;
        if (blinkDuration < 0.02f) blinkDuration = 0.02f;
    }
}
