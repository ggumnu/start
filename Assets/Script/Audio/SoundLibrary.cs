using System;
using System.Collections.Generic;
using System.Reflection;
using UnityEngine;

// 효과음 설정 에셋입니다. Assets/Resources/SoundLibrary 에 두면 GameAudio가 자동으로 읽습니다(없으면 소리 없이 동작).
// 만들기 = 메뉴 MobArchive/사운드/사운드 라이브러리 만들기. 소리 종류(SfxId)마다 칸이 자동으로 채워지므로 클립만 끌어다 넣으면 됩니다.
[CreateAssetMenu(menuName = "MobArchive/Sound Library", fileName = "SoundLibrary")]
public class SoundLibrary : ScriptableObject
{
    public const string ResourcePath = "SoundLibrary";

    [Serializable]
    public class Entry
    {
        [Tooltip("표시용 이름입니다(자동으로 채워짐 - 고쳐도 다시 바뀜).")]
        public string name;
        public SfxId id;
        [Tooltip("여러 개를 넣으면 재생할 때마다 무작위로 하나를 고릅니다. 비우면 이 소리는 나지 않습니다.")]
        public AudioClip[] clips = new AudioClip[0];
        [Range(0f, 1f)] public float volume = 1f;
        [Tooltip("재생할 때마다 이 범위 안에서 높낮이를 무작위로 흔듭니다(반복 소리가 덜 질림). 1~1이면 그대로.")]
        public Vector2 pitchRange = new Vector2(0.95f, 1.05f);
        [Tooltip("같은 소리가 이 시간(초) 안에 다시 오면 무시합니다(여러 발이 동시에 나갈 때 겹침 방지). 0이면 제한 없음.")]
        [Min(0f)] public float minInterval = 0.03f;
    }

    [Header("전체 볼륨 (플레이어 설정 창의 볼륨이 여기에 한 번 더 곱해짐)")]
    [Range(0f, 1f)] public float sfxVolume = 1f;

    [Header("배경음악 (BGM - 한 곡을 계속 반복, 씬이 바뀌어도 끊기지 않음)")]
    [Tooltip("게임을 켠 뒤 처음 한 번만 트는 인트로입니다. 끝나는 순간 빈틈 없이 아래 반복 곡으로 이어집니다. 비우면 바로 반복 곡부터 틉니다.")]
    public AudioClip bgmIntroClip;
    [Tooltip("반복 재생할 곡입니다. 비우면 BGM 없음. Play 중에 바꾸면 새 곡으로 처음부터 다시 틉니다.")]
    public AudioClip bgmClip;
    [Range(0f, 1f)] public float bgmVolume = 0.6f;
    [Tooltip("반복 구간 시작(초). 곡 끝(또는 반복 구간 끝)에 닿으면 여기로 돌아갑니다. 0이면 곡 처음.")]
    [Min(0f)] public float bgmLoopStart = 0f;
    [Tooltip("반복 구간 끝(초). 0이면 곡 끝까지 재생한 뒤 반복합니다. AI 곡의 끝부분이 어색하면 자연스럽게 이어지는 지점을 넣으세요.")]
    [Min(0f)] public float bgmLoopEnd = 0f;
    [Tooltip("ESC 중단 화면/게임 오버로 게임이 멈춘 동안 BGM 크기 배율입니다(1이면 그대로).")]
    [Range(0f, 1f)] public float bgmPausedVolumeRatio = 0.4f;

    [Header("보스방 입장 (보스방 입장 경고음과 함께)")]
    [Tooltip("경고음이 나는 순간 BGM이 0까지 줄어드는 시간(초). 0이면 바로 끊깁니다.")]
    [Min(0f)] public float bossBgmCutDuration = 0.15f;
    [Tooltip("BGM은 경고음(보스방 입장 굉음)이 끝날 때까지 꺼져 있습니다. 굉음이 끝난 뒤 여기 적은 시간(초)만큼 더 조용히 있다가 다시 틉니다. 0이면 굉음이 끝나자마자.")]
    [Min(0f)] public float bossBgmSilenceAfterAlert = 0f;
    [Tooltip("켜면 보스방에서 다시 틀 때 인트로부터(곡의 진짜 처음), 끄면 인트로 없이 반복 곡 처음부터 틉니다. 인트로가 비어 있으면 상관없음.")]
    public bool bossBgmRestartWithIntro = true;
    [Tooltip("다시 틀 때 0에서 원래 크기까지 키우는 시간(초)입니다. 0.05면 거의 바로 원래 크기.")]
    [Min(0.05f)] public float bossBgmFadeInDuration = 4f;

    [Header("걸음 소리")]
    [Tooltip("플레이어가 이 거리(유닛)만큼 걸을 때마다 걸음 소리를 한 번 냅니다.")]
    [Min(0.1f)] public float footstepDistance = 1.2f;

    [Header("소리 목록 (종류마다 한 칸 - 자동으로 채워짐)")]
    public List<Entry> entries = new List<Entry>();

    [NonSerialized] private Dictionary<SfxId, Entry> lookup;

    public Entry Find(SfxId id)
    {
        if (lookup == null)
        {
            lookup = new Dictionary<SfxId, Entry>();
            foreach (var e in entries)
            {
                if (e != null && !lookup.ContainsKey(e.id)) lookup.Add(e.id, e);
            }
        }
        return lookup.TryGetValue(id, out var found) ? found : null;
    }

    // 빠진 종류의 칸을 기본값으로 추가하고, 이름을 맞추고, 번호 순으로 정렬합니다. 바뀐 것이 있으면 true.
    public bool EnsureEntries()
    {
        bool changed = false;
        var existing = new HashSet<SfxId>();
        foreach (var e in entries)
        {
            if (e != null) existing.Add(e.id);
        }

        foreach (SfxId id in Enum.GetValues(typeof(SfxId)))
        {
            if (id == SfxId.None || existing.Contains(id)) continue;
            entries.Add(new Entry
            {
                id = id,
                volume = DefaultVolume(id),
                pitchRange = DefaultPitch(id),
                minInterval = DefaultInterval(id),
            });
            changed = true;
        }

        if (entries.RemoveAll(e => e == null) > 0) changed = true;

        foreach (var e in entries)
        {
            string label = Label(e.id);
            if (e.name != label) { e.name = label; changed = true; }
        }

        for (int i = 1; i < entries.Count; i++)
        {
            if ((int)entries[i - 1].id > (int)entries[i].id)
            {
                entries.Sort((a, b) => ((int)a.id).CompareTo((int)b.id));
                changed = true;
                break;
            }
        }

        lookup = null;
        return changed;
    }

    public static string Label(SfxId id)
    {
        FieldInfo field = typeof(SfxId).GetField(id.ToString());
        var attr = field != null ? field.GetCustomAttribute<InspectorNameAttribute>() : null;
        return attr != null ? attr.displayName : id.ToString();
    }

    // 자주 나는 소리는 작게, 반복되는 소리는 겹침 방지 간격을 둡니다(처음 만들 때만 쓰는 기본값 - 이후엔 에셋 값을 그대로 씀).
    private static float DefaultVolume(SfxId id)
    {
        switch (id)
        {
            case SfxId.UIHover: return 0.5f;
            case SfxId.Footstep: return 0.4f;
            case SfxId.EnemyHit: return 0.6f;
            case SfxId.GunShot: return 0.7f;
            case SfxId.EnemyShot: return 0.5f;
            case SfxId.BulletHitWall: return 0.4f;
            default: return 1f;
        }
    }

    private static Vector2 DefaultPitch(SfxId id)
    {
        switch (id)
        {
            case SfxId.Footstep:
            case SfxId.GunShot:
            case SfxId.EnemyHit:
            case SfxId.EnemyShot:
            case SfxId.BulletHitWall:
                return new Vector2(0.9f, 1.1f);
            case SfxId.UIConfirm:
            case SfxId.UICancel:
            case SfxId.UIDanger:
            case SfxId.PlayerDeath:
            case SfxId.IncendiaryBurn:
            case SfxId.HeliRotor:
            case SfxId.BossRoomAlert:
            case SfxId.GameOverShow:
            case SfxId.IntroPose:
                return new Vector2(1f, 1f);
            default:
                return new Vector2(0.95f, 1.05f);
        }
    }

    private static float DefaultInterval(SfxId id)
    {
        switch (id)
        {
            case SfxId.Footstep: return 0f;
            case SfxId.EnemyHit: return 0.05f;
            case SfxId.GrenadeExplode: return 0.08f;
            case SfxId.ItemActivate: return 0.1f;
            case SfxId.ChestOpen: return 0.1f;
            case SfxId.ItemPickup: return 0.1f;
            case SfxId.EnemyShot: return 0.06f;
            case SfxId.BulletHitWall: return 0.05f;
            case SfxId.BossShieldHit: return 0.08f;
            case SfxId.ActionFail: return 0.15f;
            case SfxId.PlayerHeal: return 0.1f;
            case SfxId.ShieldGain: return 0.1f;
            default: return 0.03f;
        }
    }

    void OnValidate()
    {
        if (EnsureEntries())
        {
#if UNITY_EDITOR
            UnityEditor.EditorUtility.SetDirty(this);
#endif
        }
        lookup = null;
    }

#if UNITY_EDITOR
    [UnityEditor.MenuItem("MobArchive/사운드/사운드 라이브러리 만들기")]
    private static void CreateOrSelectAsset()
    {
        const string folder = "Assets/Resources";
        const string path = folder + "/" + ResourcePath + ".asset";

        var asset = UnityEditor.AssetDatabase.LoadAssetAtPath<SoundLibrary>(path);
        if (asset == null)
        {
            if (!UnityEditor.AssetDatabase.IsValidFolder(folder))
            {
                UnityEditor.AssetDatabase.CreateFolder("Assets", "Resources");
            }
            asset = CreateInstance<SoundLibrary>();
            asset.EnsureEntries();
            UnityEditor.AssetDatabase.CreateAsset(asset, path);
            UnityEditor.AssetDatabase.SaveAssets();
            Debug.Log("SoundLibrary: " + path + " 를 만들었습니다. 각 칸의 Clips에 소리 파일을 넣으세요.");
        }
        else if (asset.EnsureEntries())
        {
            UnityEditor.EditorUtility.SetDirty(asset);
            UnityEditor.AssetDatabase.SaveAssets();
        }

        UnityEditor.Selection.activeObject = asset;
        UnityEditor.EditorGUIUtility.PingObject(asset);
    }
#endif
}
