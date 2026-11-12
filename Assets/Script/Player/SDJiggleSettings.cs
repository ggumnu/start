using System.Collections.Generic;
using UnityEngine;

// SD 파츠의 흔들림 본(이름이 "Jiggle_"로 시작하는 본) 설정입니다. SDJiggleBones가 매 프레임 읽으므로 Play 중에 바꾸면 바로 반영됩니다.
// 에셋 위치: Assets/Resources/SDJiggleSettings (없으면 코드 기본값)
// 만들기: Project 창 Assets/Resources 폴더 우클릭 -> Create -> MobArchive -> SD 캐릭터 -> 흔들림 설정 (이름 SDJiggleSettings 그대로)
//
// 본 이름별 설정: 아래 "본 이름별 설정" 목록에서 본 이름이 "이름 시작"으로 시작하면 그 값을 씁니다(여러 개가 맞으면 가장 긴 것).
// 목록에 맞는 것이 없으면 맨 위의 기본값(가방 인형 등)을 씁니다.
[CreateAssetMenu(fileName = "SDJiggleSettings", menuName = "MobArchive/SD 캐릭터/흔들림 설정")]
public class SDJiggleSettings : ScriptableObject
{
    [System.Serializable]
    public class Profile
    {
        [Tooltip("이 이름으로 시작하는 흔들림 본에 적용합니다. 예) Jiggle_Tail -> Jiggle_Tail_L, Jiggle_Tail_R")]
        public string namePrefix = "Jiggle_";

        [Tooltip("제자리로 돌아가려는 힘입니다. 클수록 빠르고 짧게 흔들립니다.")]
        [Min(1f)] public float stiffness = 260f;

        [Tooltip("흔들림이 잦아드는 정도입니다. 클수록 금방 멈춥니다(탁 하고 멈춤). 작으면 여러 번 출렁입니다.")]
        [Min(0f)] public float damping = 16f;

        [Tooltip("캐릭터 움직임에 뒤처지는 정도(관성)입니다. 1 = 그대로 뒤처짐, 0 = 전혀 안 흔들림.")]
        [Range(0f, 1.5f)] public float inertia = 1f;

        [Tooltip("아래로 처지는 힘입니다(본 길이 기준). 0이면 모델링한 자세 그대로 매달립니다.")]
        [Min(0f)] public float gravity = 0.3f;

        [Tooltip("모델링한 자세에서 최대로 기울어질 수 있는 각도(도)입니다. 몸을 뚫는 게 보이면 줄입니다.")]
        [Range(0f, 90f)] public float maxAngle = 40f;
    }

    [Header("기본값 (아래 목록에 맞는 이름이 없는 본 - 가방 인형 등)")]
    [Tooltip("제자리로 돌아가려는 힘입니다. 클수록 빠르고 짧게 흔들립니다.")]
    [Min(1f)] public float stiffness = 260f;

    [Tooltip("흔들림이 잦아드는 정도입니다. 클수록 금방 멈춥니다(탁 하고 멈춤). 작으면 여러 번 출렁입니다.")]
    [Min(0f)] public float damping = 16f;

    [Tooltip("캐릭터 움직임에 뒤처지는 정도(관성)입니다. 1 = 그대로 뒤처짐, 0 = 전혀 안 흔들림.")]
    [Range(0f, 1.5f)] public float inertia = 1f;

    [Tooltip("아래로 처지는 힘입니다(본 길이 기준). 0이면 모델링한 자세 그대로 매달립니다.")]
    [Min(0f)] public float gravity = 0.3f;

    [Tooltip("모델링한 자세에서 최대로 기울어질 수 있는 각도(도)입니다. 가방/몸을 뚫는 게 보이면 줄입니다.")]
    [Range(0f, 90f)] public float maxAngle = 40f;

    [Header("본 이름별 설정")]
    public List<Profile> profiles = new List<Profile>
    {
        // 아호게: 위로 솟아 있으니 처짐 없이, 탱탱하게 여러 번 출렁
        new Profile { namePrefix = "Jiggle_Ahoge", stiffness = 320f, damping = 9f, inertia = 1f, gravity = 0f, maxAngle = 35f },
        // 트윈테일: 길고 무거운 느낌으로 천천히, 몸을 덜 뚫도록 각도 제한
        new Profile { namePrefix = "Jiggle_Tail", stiffness = 140f, damping = 11f, inertia = 0.9f, gravity = 0.5f, maxAngle = 30f },
    };

    private Profile defaultProfile;

    // 본 이름에 맞는 설정을 돌려줍니다(가장 긴 이름 시작이 맞는 것, 없으면 기본값).
    public Profile For(string boneName)
    {
        Profile best = null;
        if (profiles != null)
        {
            foreach (Profile p in profiles)
            {
                if (p == null || string.IsNullOrEmpty(p.namePrefix) || !boneName.StartsWith(p.namePrefix)) continue;
                if (best == null || p.namePrefix.Length > best.namePrefix.Length) best = p;
            }
        }
        if (best != null) return best;
        if (defaultProfile == null) defaultProfile = new Profile();
        defaultProfile.namePrefix = "";
        defaultProfile.stiffness = stiffness;
        defaultProfile.damping = damping;
        defaultProfile.inertia = inertia;
        defaultProfile.gravity = gravity;
        defaultProfile.maxAngle = maxAngle;
        return defaultProfile;
    }

    private static SDJiggleSettings cached;
    private static bool searched;

    public static SDJiggleSettings Get()
    {
        if (!searched)
        {
            searched = true;
            cached = Resources.Load<SDJiggleSettings>("SDJiggleSettings");
            if (cached == null) cached = CreateInstance<SDJiggleSettings>();
        }
        return cached;
    }
}
