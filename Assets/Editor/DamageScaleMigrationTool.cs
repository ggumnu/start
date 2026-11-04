using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

// 공격력/적 체력 숫자 스케일을 한 번에 키우기 위한 에디터 전용 일괄 변환 도구입니다.
// (플레이어 기본 공격력 3, 적 체력 6~10 수준을 x10 해서 "실제 플레이는 동일하고 숫자만 큰" 상태로
// 만들기 위한 도구입니다.) 게임 코드(런타임)와는 완전히 분리되어 있고, 빌드에는 포함되지 않습니다.
//
// [사용 순서]
// 1) 상단 메뉴 MobArchive > 공격력-체력 스케일 변환 으로 창을 엽니다.
// 2) [스캔]을 누르면 변환 대상을 찾아 표로 보여줍니다. 이 단계에서는 아무것도 바뀌지 않습니다.
// 3) 표에서 체크된 줄만 [선택한 항목 적용]으로 x배율(기본 10) 됩니다.
// 4) 적용한 항목은 기록 파일(Assets/Editor/DamageScaleMigrationLog.json)에 남고, 같은 항목이
//    두 번 변환되는 사고(x100)를 막아줍니다. [기록된 변환 전부 되돌리기]로 원래 값으로 복구할 수
//    있습니다.
//
// [자동 변환 대상]
// - 모든 EnemyDefinition 에셋의 maxHealth
// - BossAIController가 붙은 보스 프리팹의 EnemyStats.maxHealth (보스는 EnemyDefinition을 거치지
//   않기 때문에 따로 찾습니다)
// - PlayerStats(프리팹, 현재 열려 있는 씬)의 기본 공격력 damage
// - 모든 ItemDefinition의 공격력(Damage) 덧셈(Additive) 모디파이어 (곱셈은 배율이라 그대로)
// - 알려진 아이템 특수효과의 공격력 수치: 11번 DistanceDamageEffect(damagePerStep/
//   damageLossPerShortfallStep/fallbackMinDamage), 34번 MaxHealthAttackFoodEffect
//   (attackPerMaxHealth), 20번 CreditAbsorptionTrinketEffect(attackPowerPerCredit)
//
// [검토만 하는 대상 - 자동으로 바꾸지 않고 목록으로만 알려줍니다]
// - PlayerStats.statFloors에서 공격력이 Absolute(고정 숫자) 모드인 항목
// - ItemDefinition 특수효과 필드 중 이름에 damage/attack/power가 들어가는 숫자 필드(위 알려진
//   목록에 없는 것 - 직접 확인하지 못한 아이템의 숨은 수치를 잡아내기 위한 안전망입니다)
// - description / specialEffectDescription 문구 중 "공격력" 근처에 숫자가 있는 것(손으로 고쳐야
//   하는 설명 문구 목록)
// - 보스 프리팹의 체력이 원본(부모) 프리팹에서 상속된 값이라 자동 대상에 못 들어온 경우
//
// [건드리지 않는 것]
// 플레이어 체력, 적이 플레이어에게 주는 피해(attackPower/rangedDamage - 하트 단위), 스크립트
// 안의 기본값(3.5, 10 등 - 이미 저장된 에셋에는 영향이 없고 새로 만드는 개체의 폴백일 뿐입니다).
//
// [구현 메모 - 타입을 이름 문자열로만 다루는 이유]
// EnemyDefinition/ItemDefinition/PlayerStats/EnemyStats/BossAIController를 코드에서 직접 참조하지
// 않고 타입 이름 문자열과 SerializedObject/SerializedProperty로만 다룹니다. 그러면 나중에 이
// 클래스들의 구조가 조금 바뀌어도 이 도구 때문에 컴파일 오류가 나지 않고, 필드를 못 찾으면 그
// 항목만 조용히 건너뜁니다. 값 수정도 SerializedObject를 통하므로 Unity의 되돌리기(Ctrl+Z)와
// 프리팹 오버라이드 기록이 정상적으로 동작합니다.
//
// [씬 항목]
// 현재 열려 있는(로드된) 씬만 스캔합니다. 씬 안의 값을 바꾸면 씬이 "수정됨" 상태가 되므로,
// 적용 후 Ctrl+S로 씬을 꼭 저장해주세요. 프리팹 인스턴스는 "직접 값을 덮어쓴(Override) 경우"만
// 대상에 넣습니다(덮어쓰지 않았다면 원본 프리팹 항목이 이미 대상에 들어 있습니다).
public class DamageScaleMigrationTool : EditorWindow
{
    private const string LogPath = "Assets/Editor/DamageScaleMigrationLog.json";

    // ---- 데이터 구조 -------------------------------------------------------------------------

    // 값이 들어 있는 위치를 "다시 찾아갈 수 있게" 적어둔 주소입니다. 스캔 결과, 적용, 되돌리기
    // 모두 이 주소로 대상을 다시 찾기 때문에, 창을 닫았다 열어도(또는 스크립트가 다시 컴파일돼도)
    // 기록 파일만 있으면 되돌릴 수 있습니다.
    [Serializable]
    internal class Locator
    {
        public string kind;         // "asset"(ScriptableObject) | "prefab" | "scene"
        public string path;         // 에셋/프리팹/씬 경로
        public string objectPath;   // 프리팹/씬일 때: 루트부터 대상 오브젝트까지의 이름 경로
        public string typeName;     // 프리팹/씬일 때: 대상 컴포넌트의 타입 이름
        public string propertyPath; // SerializedProperty 경로

        public string Key()
        {
            return kind + "|" + path + "|" + objectPath + "|" + typeName + "|" + propertyPath;
        }
    }

    [Serializable]
    internal class LogEntry
    {
        public Locator loc;
        public float oldValue;
        public float newValue;
        public string label;
        public string time;
    }

    [Serializable]
    internal class LogFile
    {
        public List<LogEntry> entries = new List<LogEntry>();
    }

    internal class Entry
    {
        public Locator loc;
        public string category;
        public string targetLabel;
        public string fieldLabel;
        public float oldValue;      // 자동 변환 항목만 사용
        public string valueText;    // 검토 항목의 현재 값/문구 표시용
        public string note;         // 검토 항목 설명
        public bool selected;
        public bool alreadyConverted;
    }

    private struct Resolved
    {
        public SerializedObject so;
        public SerializedProperty prop;
        public UnityEngine.Object owner;
        public bool isScene;
        public Scene scene;
        public string error;
    }

    // ---- 알려진 아이템 특수효과 필드 / 검토용 이름 규칙 -------------------------------------------

    // 타입 이름 -> 변환할 공격력 수치 필드 이름들
    private static readonly Dictionary<string, string[]> KnownEffectFields = new Dictionary<string, string[]>
    {
        { "DistanceDamageEffect", new[] { "damagePerStep", "damageLossPerShortfallStep", "fallbackMinDamage" } },
        { "MaxHealthAttackFoodEffect", new[] { "attackPerMaxHealth" } },
        { "CreditAbsorptionTrinketEffect", new[] { "attackPowerPerCredit" } },
    };

    // 검토 대상으로 잡을 필드 이름 / 검토에서 제외할 필드 이름(배율, 시간, 거리처럼 공격력 절대값이 아닌 것)
    private static readonly Regex ReviewNameRegex =
        new Regex("damage|dmg|attack|power", RegexOptions.IgnoreCase);
    private static readonly Regex SkipNameRegex =
        new Regex("ratio|multiplier|chance|duration|delay|interval|radius|speed|range|percent|rate|cost|count",
                  RegexOptions.IgnoreCase);

    // 설명 문구에서 "공격력 ... 숫자" 또는 "숫자 ... 공격력" 패턴 찾기
    private static readonly Regex DescriptionRegex =
        new Regex(@"공격력[^\r\n]{0,12}?\d+(?:\.\d+)?|\d+(?:\.\d+)?[^\r\n]{0,12}?공격력");

    private const string CatEnemy = "1. 일반 적 체력 (EnemyDefinition.maxHealth)";
    private const string CatBoss = "2. 보스 체력 (보스 프리팹 EnemyStats.maxHealth)";
    private const string CatPlayer = "3. 플레이어 기본 공격력 (PlayerStats.damage)";
    private const string CatItemStat = "4. 아이템 공격력 덧셈 모디파이어 (statModifiers)";
    private const string CatItemEffect = "5. 아이템 특수효과 공격력 수치";
    private const string CatReviewFloor = "A. 검토 - 공격력 하한선(Absolute 모드)";
    private const string CatReviewField = "B. 검토 - 특수효과 필드 (이름에 damage/attack/power 포함)";
    private const string CatReviewText = "C. 검토 - 설명 문구 속 공격력 숫자";
    private const string CatReviewBossInherited = "D. 검토 - 원본 프리팹에서 상속된 보스 체력";

    // ---- 창 상태 -----------------------------------------------------------------------------

    private float scale = 10f;
    private Vector2 scrollPos;
    private string status = "";
    private bool scanned = false;
    private List<Entry> autoEntries = new List<Entry>();
    private List<Entry> reviewEntries = new List<Entry>();
    private readonly HashSet<string> seenKeys = new HashSet<string>();
    private HashSet<string> loggedKeys = new HashSet<string>();

    [MenuItem("MobArchive/공격력-체력 스케일 변환")]
    public static void Open()
    {
        var window = GetWindow<DamageScaleMigrationTool>("공격력·체력 스케일 변환");
        window.minSize = new Vector2(820f, 460f);
        window.Show();
    }

    // ---- GUI ---------------------------------------------------------------------------------

    private void OnGUI()
    {
        EditorGUILayout.HelpBox(
            "공격력/적 체력 숫자를 한 번에 x배율 하는 도구입니다.\n" +
            "1) [스캔]으로 대상을 찾고  2) 표를 확인한 뒤(체크 해제 가능)  3) [선택한 항목 적용]을 누르세요.\n" +
            "스캔 단계에서는 아무것도 바뀌지 않습니다. 적용 전에 프로젝트 백업(또는 Git 저장)을 권장합니다.",
            MessageType.Info);

        scale = EditorGUILayout.FloatField("배율 (기본 10)", scale);

        EditorGUILayout.BeginHorizontal();
        if (GUILayout.Button("스캔", GUILayout.Height(28f)))
        {
            EditorApplication.delayCall += Scan;
        }
        using (new EditorGUI.DisabledScope(!scanned))
        {
            if (GUILayout.Button("전체 선택", GUILayout.Height(28f), GUILayout.Width(90f))) SetAllSelected(true);
            if (GUILayout.Button("전체 해제", GUILayout.Height(28f), GUILayout.Width(90f))) SetAllSelected(false);
            if (GUILayout.Button("선택한 항목 적용", GUILayout.Height(28f)))
            {
                EditorApplication.delayCall += ApplySelected;
            }
        }
        if (GUILayout.Button("기록된 변환 전부 되돌리기", GUILayout.Height(28f)))
        {
            EditorApplication.delayCall += RevertAll;
        }
        EditorGUILayout.EndHorizontal();

        if (!string.IsNullOrEmpty(status))
        {
            EditorGUILayout.HelpBox(status, MessageType.None);
        }

        if (scanned)
        {
            int selectedCount = autoEntries.Count(e => e.selected && !e.alreadyConverted);
            int convertedCount = autoEntries.Count(e => e.alreadyConverted);
            EditorGUILayout.LabelField(
                $"자동 변환 대상 {autoEntries.Count}개 (선택 {selectedCount}개 / 이미 변환됨 {convertedCount}개)   |   검토 필요 {reviewEntries.Count}개",
                EditorStyles.boldLabel);
        }

        scrollPos = EditorGUILayout.BeginScrollView(scrollPos);

        if (scanned)
        {
            DrawAutoEntries();
            DrawReviewEntries();
        }
        else
        {
            EditorGUILayout.LabelField("[스캔] 버튼을 눌러 변환 대상을 찾아보세요.");
        }

        EditorGUILayout.EndScrollView();
    }

    private void DrawAutoEntries()
    {
        string lastCategory = null;
        foreach (var e in autoEntries)
        {
            if (e.category != lastCategory)
            {
                lastCategory = e.category;
                EditorGUILayout.Space(6f);
                EditorGUILayout.LabelField(e.category, EditorStyles.boldLabel);
            }

            EditorGUILayout.BeginHorizontal();

            using (new EditorGUI.DisabledScope(e.alreadyConverted))
            {
                e.selected = EditorGUILayout.Toggle(e.alreadyConverted ? false : e.selected, GUILayout.Width(20f));
            }

            EditorGUILayout.LabelField(e.targetLabel, GUILayout.Width(250f));
            EditorGUILayout.LabelField(e.fieldLabel, GUILayout.Width(230f));

            if (e.alreadyConverted)
            {
                EditorGUILayout.LabelField("이미 변환됨 (기록 있음)", GUILayout.Width(170f));
            }
            else
            {
                EditorGUILayout.LabelField(
                    FormatNumber(e.oldValue) + "  →  " + FormatNumber(Scaled(e.oldValue)), GUILayout.Width(170f));
            }

            if (GUILayout.Button("위치", GUILayout.Width(44f)))
            {
                PingEntry(e);
            }

            EditorGUILayout.EndHorizontal();
        }
    }

    private void DrawReviewEntries()
    {
        if (reviewEntries.Count == 0) return;

        EditorGUILayout.Space(10f);
        EditorGUILayout.LabelField("검토 필요 (자동 변환 안 함 - 필요하면 직접 수정)", EditorStyles.boldLabel);

        string lastCategory = null;
        foreach (var e in reviewEntries)
        {
            if (e.category != lastCategory)
            {
                lastCategory = e.category;
                EditorGUILayout.Space(6f);
                EditorGUILayout.LabelField(e.category, EditorStyles.boldLabel);
            }

            EditorGUILayout.BeginHorizontal();
            GUILayout.Space(24f);
            EditorGUILayout.LabelField(e.targetLabel, GUILayout.Width(250f));
            EditorGUILayout.LabelField(e.fieldLabel, GUILayout.Width(230f));
            EditorGUILayout.LabelField(e.valueText, GUILayout.Width(170f));
            if (GUILayout.Button("위치", GUILayout.Width(44f)))
            {
                PingEntry(e);
            }
            EditorGUILayout.EndHorizontal();

            if (!string.IsNullOrEmpty(e.note))
            {
                EditorGUILayout.LabelField("      " + e.note, EditorStyles.miniLabel);
            }
        }
    }

    private void SetAllSelected(bool value)
    {
        foreach (var e in autoEntries)
        {
            if (!e.alreadyConverted) e.selected = value;
        }
    }

    // ---- 스캔 --------------------------------------------------------------------------------

    private void Scan()
    {
        autoEntries.Clear();
        reviewEntries.Clear();
        seenKeys.Clear();

        var log = LoadLog();
        loggedKeys = new HashSet<string>();
        foreach (var le in log.entries)
        {
            if (le != null && le.loc != null) loggedKeys.Add(le.loc.Key());
        }

        var notes = new List<string>();

        try
        {
            ScanEnemyDefinitions();
            ScanItemDefinitions();
            ScanPrefabs();
            ScanOpenScenes(notes);
        }
        catch (Exception ex)
        {
            Debug.LogError("[스케일 변환] 스캔 중 오류: " + ex);
            notes.Add("스캔 중 오류가 발생했습니다(콘솔 확인): " + ex.Message);
        }
        finally
        {
            EditorUtility.ClearProgressBar();
        }

        autoEntries = autoEntries
            .OrderBy(e => e.category, StringComparer.Ordinal)
            .ThenBy(e => e.targetLabel, StringComparer.Ordinal)
            .ThenBy(e => e.fieldLabel, StringComparer.Ordinal)
            .ToList();
        reviewEntries = reviewEntries
            .OrderBy(e => e.category, StringComparer.Ordinal)
            .ThenBy(e => e.targetLabel, StringComparer.Ordinal)
            .ThenBy(e => e.fieldLabel, StringComparer.Ordinal)
            .ToList();

        scanned = true;
        status = "스캔 완료. 표를 확인한 뒤 [선택한 항목 적용]을 누르세요." +
                 (notes.Count > 0 ? "\n" + string.Join("\n", notes) : "");
        Repaint();
    }

    private void ScanEnemyDefinitions()
    {
        string[] guids = AssetDatabase.FindAssets("t:EnemyDefinition");
        foreach (string guid in guids)
        {
            string path = AssetDatabase.GUIDToAssetPath(guid);
            var asset = AssetDatabase.LoadAssetAtPath<ScriptableObject>(path);
            if (asset == null || asset.GetType().Name != "EnemyDefinition") continue;

            using (var so = new SerializedObject(asset))
            {
                var p = so.FindProperty("maxHealth");
                if (p == null || p.propertyType != SerializedPropertyType.Float) continue;

                AddAuto(new Locator { kind = "asset", path = path, objectPath = "", typeName = asset.GetType().Name, propertyPath = "maxHealth" },
                        CatEnemy, asset.name, "maxHealth", p.floatValue);
            }
        }
    }

    private void ScanItemDefinitions()
    {
        string[] guids = AssetDatabase.FindAssets("t:ItemDefinition");
        foreach (string guid in guids)
        {
            string path = AssetDatabase.GUIDToAssetPath(guid);
            var asset = AssetDatabase.LoadAssetAtPath<ScriptableObject>(path);
            if (asset == null || asset.GetType().Name != "ItemDefinition") continue;

            using (var so = new SerializedObject(asset))
            {
                ScanItemStatModifiers(so, asset, path);
                ScanItemEffectFields(so, asset, path);
                ScanDescriptionText(so, asset, path, "description");
                ScanDescriptionText(so, asset, path, "specialEffectDescription");
            }
        }
    }

    // 아이템의 statModifiers 중 "공격력(Damage) + 덧셈(Additive)"만 자동 변환 대상입니다.
    private void ScanItemStatModifiers(SerializedObject so, UnityEngine.Object asset, string path)
    {
        var mods = so.FindProperty("statModifiers");
        if (mods == null || !mods.isArray) return;

        for (int i = 0; i < mods.arraySize; i++)
        {
            var el = mods.GetArrayElementAtIndex(i);
            var statType = el.FindPropertyRelative("statType");
            var modType = el.FindPropertyRelative("modifierType");
            var value = el.FindPropertyRelative("value");
            if (statType == null || modType == null || value == null) continue;
            if (value.propertyType != SerializedPropertyType.Float) continue;

            if (EnumName(statType) != "Damage") continue;
            if (EnumName(modType) != "Additive") continue;

            AddAuto(new Locator { kind = "asset", path = path, objectPath = "", typeName = asset.GetType().Name, propertyPath = value.propertyPath },
                    CatItemStat, asset.name, "statModifiers[" + i + "].value (공격력 +)", value.floatValue);
        }
    }

    // 아이템 특수효과(SerializeReference) 안의 숫자 필드를 훑어서, 알려진 공격력 수치는 자동 변환
    // 대상으로, 이름이 공격력스러운 나머지는 검토 대상으로 분류합니다.
    private void ScanItemEffectFields(SerializedObject so, UnityEngine.Object asset, string path)
    {
        var it = so.GetIterator();
        bool enter = true;
        while (it.Next(enter))
        {
            enter = true;
            string propPath = it.propertyPath;

            if (propPath == "m_Script" || propPath == "statModifiers")
            {
                enter = false; // 이 항목의 내부는 훑지 않음
                continue;
            }

            if (it.propertyType != SerializedPropertyType.Float && it.propertyType != SerializedPropertyType.Integer)
            {
                continue;
            }

            string ownerType = FindManagedOwnerTypeName(so, propPath);
            if (ownerType == null) continue; // 아이템 최상위 필드(weight, activeCost 등)는 대상 아님

            string fieldName = propPath.Substring(propPath.LastIndexOf('.') + 1);
            string[] knownFields;

            if (KnownEffectFields.TryGetValue(ownerType, out knownFields) && Array.IndexOf(knownFields, fieldName) >= 0)
            {
                if (it.propertyType != SerializedPropertyType.Float) continue;

                AddAuto(new Locator { kind = "asset", path = path, objectPath = "", typeName = asset.GetType().Name, propertyPath = propPath },
                        CatItemEffect, asset.name + "  /  " + ownerType, fieldName, it.floatValue);
            }
            else if (ReviewNameRegex.IsMatch(fieldName) && !SkipNameRegex.IsMatch(fieldName))
            {
                string valueText = it.propertyType == SerializedPropertyType.Float
                    ? FormatNumber(it.floatValue)
                    : it.intValue.ToString(CultureInfo.InvariantCulture);

                AddReview(new Locator { kind = "asset", path = path, objectPath = "", typeName = asset.GetType().Name, propertyPath = propPath },
                          CatReviewField, asset.name + "  /  " + ownerType, fieldName, valueText,
                          "공격력 절대 수치라면 직접 x배율이 필요합니다(체력/하트 단위 값이면 그대로 두세요).");
            }
        }
    }

    private void ScanDescriptionText(SerializedObject so, UnityEngine.Object asset, string path, string fieldName)
    {
        var p = so.FindProperty(fieldName);
        if (p == null || p.propertyType != SerializedPropertyType.String) return;

        string text = p.stringValue;
        if (string.IsNullOrEmpty(text)) return;

        foreach (Match m in DescriptionRegex.Matches(text))
        {
            string snippet = m.Value.Trim();
            AddReview(new Locator { kind = "asset", path = path, objectPath = "", typeName = asset.GetType().Name, propertyPath = fieldName + "#" + snippet },
                      CatReviewText, asset.name, fieldName, "\"" + snippet + "\"",
                      "설명 문구 속 공격력 수치입니다. 아이템 값을 x배율 했다면 문구도 직접 고쳐주세요.");
        }
    }

    private void ScanPrefabs()
    {
        string[] guids = AssetDatabase.FindAssets("t:Prefab", new[] { "Assets" });
        for (int i = 0; i < guids.Length; i++)
        {
            string path = AssetDatabase.GUIDToAssetPath(guids[i]);

            if (i % 10 == 0)
            {
                EditorUtility.DisplayProgressBar("프리팹 스캔 중", path, (float)i / Mathf.Max(1, guids.Length));
            }

            var root = AssetDatabase.LoadAssetAtPath<GameObject>(path);
            if (root == null) continue;

            ScanGameObjectTree(root, "prefab", path);
        }
    }

    private void ScanOpenScenes(List<string> notes)
    {
        for (int i = 0; i < SceneManager.sceneCount; i++)
        {
            Scene scene = SceneManager.GetSceneAt(i);
            if (!scene.isLoaded) continue;

            if (string.IsNullOrEmpty(scene.path))
            {
                notes.Add("저장되지 않은 씬 '" + scene.name + "'은(는) 건너뛰었습니다. 씬을 저장한 뒤 다시 스캔해주세요.");
                continue;
            }

            foreach (var root in scene.GetRootGameObjects())
            {
                ScanGameObjectTree(root, "scene", scene.path);
            }
        }
    }

    // 오브젝트 트리 안에서 PlayerStats(플레이어 기본 공격력)와 BossAIController(보스 체력)를 찾습니다.
    private void ScanGameObjectTree(GameObject root, string kind, string path)
    {
        Component[] comps = root.GetComponentsInChildren<Component>(true);
        foreach (var c in comps)
        {
            if (c == null) continue; // Missing Script 컴포넌트는 null로 나옵니다

            string typeName = c.GetType().Name;

            if (typeName == "PlayerStats")
            {
                HandlePlayerStats(c, kind, path);
            }
            else if (typeName == "BossAIController")
            {
                HandleBossStats(c, kind, path);
            }
        }
    }

    private void HandlePlayerStats(Component c, string kind, string path)
    {
        using (var so = new SerializedObject(c))
        {
            var damage = so.FindProperty("damage");
            if (damage != null && damage.propertyType == SerializedPropertyType.Float && ShouldInclude(c, damage))
            {
                AddAuto(MakeLocator(kind, path, c, "damage"), CatPlayer, DescribeTarget(kind, path, c), "damage", damage.floatValue);
            }

            // 공격력 하한선이 Absolute(고정 숫자) 모드면 공격력을 키울 때 같이 키워야 할 수 있어 검토 대상으로만 알립니다.
            var floors = so.FindProperty("statFloors");
            if (floors != null && floors.isArray)
            {
                for (int i = 0; i < floors.arraySize; i++)
                {
                    var el = floors.GetArrayElementAtIndex(i);
                    var statType = el.FindPropertyRelative("statType");
                    var mode = el.FindPropertyRelative("mode");
                    var value = el.FindPropertyRelative("value");
                    if (statType == null || mode == null || value == null) continue;
                    if (EnumName(statType) != "Damage" || EnumName(mode) != "Absolute") continue;

                    AddReview(MakeLocator(kind, path, c, value.propertyPath), CatReviewFloor,
                              DescribeTarget(kind, path, c), "statFloors[" + i + "] (공격력)",
                              FormatNumber(value.floatValue) + " (Absolute)",
                              "공격력 하한선이 고정 숫자입니다. 공격력을 x배율 하면 이 값도 함께 키워야 할 수 있습니다.");
                }
            }
        }
    }

    private void HandleBossStats(Component bossController, string kind, string path)
    {
        foreach (var sc in bossController.gameObject.GetComponents<Component>())
        {
            if (sc == null || sc.GetType().Name != "EnemyStats") continue;

            using (var so = new SerializedObject(sc))
            {
                var maxHealth = so.FindProperty("maxHealth");
                if (maxHealth == null || maxHealth.propertyType != SerializedPropertyType.Float) continue;

                if (!ShouldInclude(sc, maxHealth))
                {
                    // 보스 체력이 원본(부모) 프리팹에서 그대로 물려받은 값이면, 그 원본 프리팹은 보스가 아니라서
                    // 자동 대상에 들어오지 않습니다. 놓치지 않도록 검토 목록으로 알려줍니다.
                    AddReview(MakeLocator(kind, path, sc, "maxHealth"), CatReviewBossInherited,
                              DescribeTarget(kind, path, sc), "maxHealth", FormatNumber(maxHealth.floatValue) + " (상속)",
                              "보스 체력이 원본 프리팹에서 상속된 값입니다. 이 프리팹에서 체력을 직접 x배율 해주세요.");
                    continue;
                }

                AddAuto(MakeLocator(kind, path, sc, "maxHealth"), CatBoss, DescribeTarget(kind, path, sc), "maxHealth", maxHealth.floatValue);
            }
        }
    }

    // 프리팹 인스턴스의 값이 "원본 프리팹에서 그대로 물려받은 값"이면 이미 원본 프리팹 항목이 대상에
    // 들어 있으므로 중복으로 넣지 않습니다. 직접 덮어쓴(Override) 값이거나 프리팹 인스턴스가 아니면 포함합니다.
    private static bool ShouldInclude(Component c, SerializedProperty p)
    {
        if (!PrefabUtility.IsPartOfPrefabInstance(c)) return true;
        return p.prefabOverride;
    }

    private static Locator MakeLocator(string kind, string path, Component c, string propertyPath)
    {
        return new Locator
        {
            kind = kind,
            path = path,
            objectPath = GetTransformPath(c.transform),
            typeName = c.GetType().Name,
            propertyPath = propertyPath,
        };
    }

    private static string DescribeTarget(string kind, string path, Component c)
    {
        string fileName = Path.GetFileNameWithoutExtension(path);
        if (kind == "scene")
        {
            return "[씬] " + fileName + " / " + c.gameObject.name;
        }

        return c.transform.parent == null ? fileName : fileName + " (" + c.gameObject.name + ")";
    }

    private void AddAuto(Locator loc, string category, string target, string field, float value)
    {
        string key = loc.Key();
        if (!seenKeys.Add(key)) return;

        bool converted = loggedKeys.Contains(key);
        autoEntries.Add(new Entry
        {
            loc = loc,
            category = category,
            targetLabel = target,
            fieldLabel = field,
            oldValue = value,
            selected = !converted,
            alreadyConverted = converted,
        });
    }

    private void AddReview(Locator loc, string category, string target, string field, string valueText, string note)
    {
        string key = loc.Key();
        if (!seenKeys.Add(key)) return;

        reviewEntries.Add(new Entry
        {
            loc = loc,
            category = category,
            targetLabel = target,
            fieldLabel = field,
            valueText = valueText,
            note = note,
        });
    }

    // ---- 적용 / 되돌리기 ---------------------------------------------------------------------

    private void ApplySelected()
    {
        if (scale <= 0f || Mathf.Approximately(scale, 1f))
        {
            EditorUtility.DisplayDialog("배율 확인", "배율은 0보다 크고 1이 아닌 값이어야 합니다.", "확인");
            return;
        }

        var targets = autoEntries.Where(e => e.selected && !e.alreadyConverted).ToList();
        if (targets.Count == 0)
        {
            status = "적용할 항목이 없습니다. (선택된 항목이 없거나 전부 이미 변환됨)";
            Repaint();
            return;
        }

        bool hasScene = targets.Any(e => e.loc.kind == "scene");
        string message = targets.Count + "개 항목의 값을 x" + FormatNumber(scale) + " 합니다.\n\n" +
                         "적용 전에 프로젝트 백업(또는 Git 저장)을 해두셨나요?" +
                         (hasScene ? "\n\n씬 항목이 포함되어 있어, 적용 후 씬을 저장(Ctrl+S)해야 합니다." : "");
        if (!EditorUtility.DisplayDialog("변환 적용", message, "적용", "취소")) return;

        var log = LoadLog();
        int ok = 0, changedSinceScan = 0, failed = 0;
        var failMessages = new List<string>();

        try
        {
            for (int i = 0; i < targets.Count; i++)
            {
                var e = targets[i];
                EditorUtility.DisplayProgressBar("변환 적용 중", e.targetLabel + " / " + e.fieldLabel, (float)i / targets.Count);

                Resolved r = Resolve(e.loc);
                if (r.so == null)
                {
                    failed++;
                    failMessages.Add(e.targetLabel + " / " + e.fieldLabel + " - " + r.error);
                    continue;
                }

                try
                {
                    float current = r.prop.floatValue;
                    if (!Near(current, e.oldValue))
                    {
                        // 스캔한 뒤에 값이 바뀌었다면 덮어쓰지 않고 건너뜁니다(다시 스캔하면 새 값이 표에 나옵니다).
                        changedSinceScan++;
                        failMessages.Add(e.targetLabel + " / " + e.fieldLabel + " - 스캔 이후 값이 바뀌어 건너뜀 (다시 스캔해주세요)");
                        continue;
                    }

                    float newValue = Scaled(current);
                    r.prop.floatValue = newValue;
                    r.so.ApplyModifiedProperties();
                    EditorUtility.SetDirty(r.owner);
                    if (r.isScene) EditorSceneManager.MarkSceneDirty(r.scene);

                    log.entries.Add(new LogEntry
                    {
                        loc = e.loc,
                        oldValue = current,
                        newValue = newValue,
                        label = e.targetLabel + " / " + e.fieldLabel,
                        time = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss", CultureInfo.InvariantCulture),
                    });
                    ok++;
                }
                finally
                {
                    r.so.Dispose();
                }
            }
        }
        finally
        {
            EditorUtility.ClearProgressBar();
            SaveLog(log);
            AssetDatabase.SaveAssets();
        }

        Scan();

        status = "적용 완료: " + ok + "개 변환" +
                 (changedSinceScan > 0 ? ", " + changedSinceScan + "개는 스캔 이후 값이 바뀌어 건너뜀" : "") +
                 (failed > 0 ? ", " + failed + "개는 대상을 찾지 못해 실패" : "") + "." +
                 (hasScene ? "\n씬 항목이 있으니 씬을 저장(Ctrl+S)해주세요." : "") +
                 (failMessages.Count > 0 ? "\n" + string.Join("\n", failMessages) : "");
        Debug.Log("[스케일 변환] " + status);
        Repaint();
    }

    private void RevertAll()
    {
        var log = LoadLog();
        if (log.entries.Count == 0)
        {
            status = "되돌릴 기록이 없습니다.";
            Repaint();
            return;
        }

        if (!EditorUtility.DisplayDialog("되돌리기",
                "기록된 " + log.entries.Count + "개 항목을 변환 전 값으로 되돌립니다.\n(씬 항목은 해당 씬이 열려 있어야 되돌릴 수 있습니다.)",
                "되돌리기", "취소"))
        {
            return;
        }

        var remaining = new List<LogEntry>();
        var failMessages = new List<string>();
        int ok = 0;
        bool touchedScene = false;

        try
        {
            for (int i = log.entries.Count - 1; i >= 0; i--)
            {
                var le = log.entries[i];
                EditorUtility.DisplayProgressBar("되돌리는 중", le.label, (float)(log.entries.Count - i) / log.entries.Count);

                Resolved r = Resolve(le.loc);
                if (r.so == null)
                {
                    remaining.Add(le);
                    failMessages.Add(le.label + " - " + r.error);
                    continue;
                }

                try
                {
                    if (!Near(r.prop.floatValue, le.newValue))
                    {
                        // 적용 이후 사람이 값을 직접 다시 바꿨다면 덮어쓰지 않습니다.
                        remaining.Add(le);
                        failMessages.Add(le.label + " - 현재 값이 변환 후 값과 달라 건너뜀 (직접 수정하신 것 같습니다)");
                        continue;
                    }

                    r.prop.floatValue = le.oldValue;
                    r.so.ApplyModifiedProperties();
                    EditorUtility.SetDirty(r.owner);
                    if (r.isScene)
                    {
                        EditorSceneManager.MarkSceneDirty(r.scene);
                        touchedScene = true;
                    }
                    ok++;
                }
                finally
                {
                    r.so.Dispose();
                }
            }
        }
        finally
        {
            EditorUtility.ClearProgressBar();
        }

        remaining.Reverse();
        log.entries = remaining;
        SaveLog(log);
        AssetDatabase.SaveAssets();

        if (scanned) Scan();

        status = "되돌리기 완료: " + ok + "개 복구" +
                 (remaining.Count > 0 ? ", " + remaining.Count + "개는 복구하지 못해 기록에 남겨둠" : "") + "." +
                 (touchedScene ? "\n씬 항목이 있으니 씬을 저장(Ctrl+S)해주세요." : "") +
                 (failMessages.Count > 0 ? "\n" + string.Join("\n", failMessages) : "");
        Debug.Log("[스케일 변환] " + status);
        Repaint();
    }

    // ---- 대상 다시 찾기 ------------------------------------------------------------------------

    private static Resolved Resolve(Locator loc)
    {
        var r = new Resolved();

        try
        {
            if (loc.kind == "asset")
            {
                var asset = AssetDatabase.LoadAssetAtPath<ScriptableObject>(loc.path);
                if (asset == null)
                {
                    r.error = "에셋을 찾을 수 없음";
                    return r;
                }
                r.owner = asset;
            }
            else
            {
                Transform rootTransform = null;

                if (loc.kind == "prefab")
                {
                    var prefabRoot = AssetDatabase.LoadAssetAtPath<GameObject>(loc.path);
                    if (prefabRoot == null)
                    {
                        r.error = "프리팹을 찾을 수 없음";
                        return r;
                    }
                    rootTransform = prefabRoot.transform;
                }
                else if (loc.kind == "scene")
                {
                    Scene scene = SceneManager.GetSceneByPath(loc.path);
                    if (!scene.IsValid() || !scene.isLoaded)
                    {
                        r.error = "씬이 열려 있지 않음 (해당 씬을 연 뒤 다시 시도)";
                        return r;
                    }

                    string rootName = loc.objectPath.Split('/')[0];
                    foreach (var go in scene.GetRootGameObjects())
                    {
                        if (go.name == rootName)
                        {
                            rootTransform = go.transform;
                            break;
                        }
                    }

                    if (rootTransform == null)
                    {
                        r.error = "씬에서 오브젝트를 찾을 수 없음";
                        return r;
                    }

                    r.isScene = true;
                    r.scene = scene;
                }
                else
                {
                    r.error = "알 수 없는 종류";
                    return r;
                }

                Transform target = FindTransform(rootTransform, loc.objectPath);
                if (target == null)
                {
                    r.error = "오브젝트를 찾을 수 없음 (" + loc.objectPath + ")";
                    return r;
                }

                Component found = null;
                foreach (var comp in target.GetComponents<Component>())
                {
                    if (comp != null && comp.GetType().Name == loc.typeName)
                    {
                        found = comp;
                        break;
                    }
                }

                if (found == null)
                {
                    r.error = "컴포넌트를 찾을 수 없음 (" + loc.typeName + ")";
                    return r;
                }

                r.owner = found;
            }

            r.so = new SerializedObject(r.owner);
            r.prop = r.so.FindProperty(loc.propertyPath);
            if (r.prop == null || r.prop.propertyType != SerializedPropertyType.Float)
            {
                r.error = "필드를 찾을 수 없음 (" + loc.propertyPath + ")";
                r.so.Dispose();
                r.so = null;
                r.prop = null;
            }
        }
        catch (Exception ex)
        {
            r.error = ex.Message;
            if (r.so != null)
            {
                r.so.Dispose();
                r.so = null;
            }
        }

        return r;
    }

    // objectPath는 "루트/자식/손자" 형태입니다. 첫 조각(루트 이름)은 rootTransform 자신이므로 그 뒤부터 찾습니다.
    private static Transform FindTransform(Transform rootTransform, string objectPath)
    {
        int slash = objectPath.IndexOf('/');
        if (slash < 0) return rootTransform;

        return rootTransform.Find(objectPath.Substring(slash + 1));
    }

    private static string GetTransformPath(Transform t)
    {
        var names = new List<string>();
        while (t != null)
        {
            names.Add(t.name);
            t = t.parent;
        }
        names.Reverse();
        return string.Join("/", names);
    }

    private static void PingEntry(Entry e)
    {
        Resolved r = Resolve(e.loc);
        try
        {
            if (r.owner == null) return;

            UnityEngine.Object toSelect = r.owner;
            var comp = r.owner as Component;
            if (comp != null) toSelect = comp.gameObject;

            Selection.activeObject = toSelect;
            EditorGUIUtility.PingObject(toSelect);
        }
        finally
        {
            if (r.so != null) r.so.Dispose();
        }
    }

    // ---- 기록 파일 -----------------------------------------------------------------------------

    private static LogFile LoadLog()
    {
        try
        {
            if (File.Exists(LogPath))
            {
                var log = JsonUtility.FromJson<LogFile>(File.ReadAllText(LogPath, Encoding.UTF8));
                if (log != null && log.entries != null) return log;
            }
        }
        catch (Exception ex)
        {
            Debug.LogWarning("[스케일 변환] 기록 파일을 읽지 못해 빈 기록으로 시작합니다: " + ex.Message);
        }

        return new LogFile();
    }

    private static void SaveLog(LogFile log)
    {
        try
        {
            string dir = Path.GetDirectoryName(LogPath);
            if (!string.IsNullOrEmpty(dir)) Directory.CreateDirectory(dir);

            File.WriteAllText(LogPath, JsonUtility.ToJson(log, true), new UTF8Encoding(false));
            AssetDatabase.ImportAsset(LogPath);
        }
        catch (Exception ex)
        {
            Debug.LogError("[스케일 변환] 기록 파일 저장 실패 - 되돌리기가 어려울 수 있습니다: " + ex.Message);
        }
    }

    // ---- 유틸 ----------------------------------------------------------------------------------

    private float Scaled(float value)
    {
        // 0.1 x 10 = 1.0000000149 같은 부동소수점 잡음을 지웁니다.
        return Mathf.Round(value * scale * 10000f) / 10000f;
    }

    private static bool Near(float a, float b)
    {
        return Mathf.Abs(a - b) <= 0.0001f * Mathf.Max(1f, Mathf.Max(Mathf.Abs(a), Mathf.Abs(b)));
    }

    private static string FormatNumber(float value)
    {
        return value.ToString("0.####", CultureInfo.InvariantCulture);
    }

    private static string EnumName(SerializedProperty p)
    {
        if (p.propertyType != SerializedPropertyType.Enum) return "";

        int index = p.enumValueIndex;
        string[] names = p.enumNames;
        return (index >= 0 && index < names.Length) ? names[index] : "";
    }

    // propertyPath의 상위 경로를 거슬러 올라가며 가장 가까운 [SerializeReference] 소유자를 찾아 그
    // 타입 이름을 돌려줍니다(없으면 null - 아이템 최상위 필드 등).
    private static string FindManagedOwnerTypeName(SerializedObject so, string propertyPath)
    {
        string p = propertyPath;
        while (true)
        {
            int dot = p.LastIndexOf('.');
            if (dot <= 0) return null;

            p = p.Substring(0, dot);
            SerializedProperty owner = so.FindProperty(p);
            if (owner != null && owner.propertyType == SerializedPropertyType.ManagedReference)
            {
                string full = owner.managedReferenceFullTypename;
                if (string.IsNullOrEmpty(full)) return null;
                return ShortTypeName(full);
            }
        }
    }

    // "Assembly-CSharp Namespace.TypeName" -> "TypeName"
    private static string ShortTypeName(string full)
    {
        int space = full.LastIndexOf(' ');
        string t = space >= 0 ? full.Substring(space + 1) : full;

        int slash = t.LastIndexOf('/');
        if (slash >= 0) t = t.Substring(slash + 1);

        int dot = t.LastIndexOf('.');
        if (dot >= 0) t = t.Substring(dot + 1);

        return t;
    }
}
