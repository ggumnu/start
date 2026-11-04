#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using UnityEditor;
using UnityEngine;

// EnemyDefinition.attackConfig([SerializeReference] IEnemyAttackConfig) 필드의 인스펙터
// 표시를 담당하는 에디터 전용 드로어입니다. 반드시 폴더 이름이 "Editor"인 곳에 둬야
// 합니다 (예: Assets/Editor/EnemyAttackConfigDrawer.cs) - 그래야 Unity가 빌드에서
// 자동으로 제외합니다. #if UNITY_EDITOR로도 한 번 더 감싸서, 혹시 다른 폴더에 놓이더라도
// 빌드 에러가 나지 않도록 안전장치를 뒀습니다(AppearanceContextDrawer.cs와 동일한 규칙).
//
// [왜 필요한가 - "표지만 보이고 아무것도 안 뜨는" 증상의 원인]
// 유니티 기본 인스펙터는 [SerializeReference] 필드가 null일 때 "타입을 새로 선택"하는
// UI를 자동으로 그려주지 않습니다. 값이 이미 들어있는 필드라면 그 타입의 세부 필드들을
// 보여주지만, "지금 IEnemyAttackConfig 자리에 DashAttackConfig를 넣을지 ShootAttackConfig를
// 넣을지"를 고르는 드롭다운/버튼은 유니티가 기본 제공하지 않아서, 처음 만든 상태(=값이
// null)에서는 라벨만 보이고 그 아래에 아무것도 표시되지 않는 게 정상 동작입니다.
// 이 드로어가 그 빠진 UI(타입 선택 버튼 + 고른 타입의 세부 필드 표시)를 직접 그립니다.
//
// [CustomPropertyDrawer(typeof(IEnemyAttackConfig))]로 인터페이스 자체를 타겟팅했으므로,
// 필드 선언 타입이 IEnemyAttackConfig인 곳이면 전부(지금은 EnemyDefinition.attackConfig
// 하나) 자동으로 이 드로어가 적용됩니다. 나중에 같은 인터페이스 필드가 늘어나도 이
// 드로어를 다시 지정하거나 수정할 필요가 없습니다.
//
// [세부 필드를 그릴 때 왜 EditorGUI.PropertyField(property, ...)를 그대로 쓰지 않는가]
// property 자체가 바로 이 드로어가 담당하는 그 SerializeReference 필드이기 때문에,
// 그 자리에서 다시 EditorGUI.PropertyField(property, ...)를 호출하면 유니티가 같은
// 드로어를 또 불러 무한 재귀(스택 오버플로우)에 빠집니다. 그래서 property의 "자식
// 필드들"(DashAttackConfig/ShootAttackConfig 내부의 attackRange 같은 float들)만
// SerializedProperty.NextVisible()로 직접 순회하며 하나씩 그립니다 - 자식 필드들은
// IEnemyAttackConfig 타입이 아니므로 이 드로어가 다시 호출되지 않습니다.
[CustomPropertyDrawer(typeof(IEnemyAttackConfig))]
public class EnemyAttackConfigDrawer : PropertyDrawer
{
    private const string NoneLabel = "(선택 안 됨)";

    public override void OnGUI(Rect position, SerializedProperty property, GUIContent label)
    {
        EditorGUI.BeginProperty(position, label, property);

        Rect dropdownRect = new Rect(position.x, position.y, position.width, EditorGUIUtility.singleLineHeight);
        string currentTypeName = property.managedReferenceValue == null
            ? NoneLabel
            : property.managedReferenceValue.GetType().Name;

        if (EditorGUI.DropdownButton(dropdownRect, new GUIContent($"{label.text} : {currentTypeName}"), FocusType.Keyboard))
        {
            ShowTypeMenu(property);
        }

        if (property.managedReferenceValue != null)
        {
            EditorGUI.indentLevel++;
            Rect childArea = new Rect(
                position.x,
                dropdownRect.yMax + EditorGUIUtility.standardVerticalSpacing,
                position.width,
                position.height - dropdownRect.height - EditorGUIUtility.standardVerticalSpacing);
            DrawChildren(childArea, property);
            EditorGUI.indentLevel--;
        }

        EditorGUI.EndProperty();
    }

    public override float GetPropertyHeight(SerializedProperty property, GUIContent label)
    {
        float height = EditorGUIUtility.singleLineHeight;
        if (property.managedReferenceValue != null)
        {
            height += EditorGUIUtility.standardVerticalSpacing + GetChildrenHeight(property);
        }
        return height;
    }

    // GenericMenu에서 타입을 고르면 이 메서드가 호출되어 실제로 인스턴스를 새로 만들어
    // 대입합니다. GenericMenu 콜백은 메뉴를 띄운 시점과 다른 타이밍(다음 이벤트)에
    // 실행되므로, SerializedProperty를 그대로 캡처해서 쓰면 그 사이 인스펙터가 갱신되며
    // 무효화될 수 있어 안전하지 않습니다. 그래서 SerializedObject + propertyPath만
    // 캡처해뒀다가, 콜백이 실제로 실행되는 시점에 FindProperty로 다시 찾아 적용합니다.
    private void ShowTypeMenu(SerializedProperty property)
    {
        var so = property.serializedObject;
        string path = property.propertyPath;
        var currentType = property.managedReferenceValue?.GetType();

        var menu = new GenericMenu();

        menu.AddItem(new GUIContent(NoneLabel), currentType == null, () => ApplySelection(so, path, null));

        var candidateTypes = AppDomain.CurrentDomain.GetAssemblies()
            .SelectMany(SafeGetTypes)
            .Where(t => typeof(IEnemyAttackConfig).IsAssignableFrom(t) && !t.IsInterface && !t.IsAbstract)
            .OrderBy(t => t.Name)
            .ToList();

        foreach (var type in candidateTypes)
        {
            var capturedType = type;
            menu.AddItem(new GUIContent(capturedType.Name), currentType == capturedType,
                () => ApplySelection(so, path, capturedType));
        }

        menu.ShowAsContext();
    }

    private void ApplySelection(SerializedObject so, string propertyPath, Type type)
    {
        so.Update();
        var prop = so.FindProperty(propertyPath);
        if (prop == null) return;

        prop.managedReferenceValue = type == null ? null : Activator.CreateInstance(type);
        so.ApplyModifiedProperties();
    }

    // property(=SerializeReference 필드 자신)의 "자식" 필드들만 순회하며 그립니다.
    // property.Copy() + NextVisible(true)로 첫 자식으로 들어간 뒤, GetEndProperty()로
    // 얻은 "이 필드의 범위를 벗어나는 지점"에 도달할 때까지 형제 필드를 NextVisible(false)로
    // 순회합니다.
    private void DrawChildren(Rect area, SerializedProperty property)
    {
        var iterator = property.Copy();
        var endProperty = property.GetEndProperty();
        float y = area.y;
        bool enterChildren = true;

        iterator.NextVisible(enterChildren);
        while (!SerializedProperty.EqualContents(iterator, endProperty))
        {
            float height = EditorGUI.GetPropertyHeight(iterator, true);
            EditorGUI.PropertyField(new Rect(area.x, y, area.width, height), iterator, true);
            y += height + EditorGUIUtility.standardVerticalSpacing;

            enterChildren = false;
            iterator.NextVisible(enterChildren);
        }
    }

    private float GetChildrenHeight(SerializedProperty property)
    {
        var iterator = property.Copy();
        var endProperty = property.GetEndProperty();
        float total = 0f;
        bool enterChildren = true;

        iterator.NextVisible(enterChildren);
        while (!SerializedProperty.EqualContents(iterator, endProperty))
        {
            total += EditorGUI.GetPropertyHeight(iterator, true) + EditorGUIUtility.standardVerticalSpacing;
            enterChildren = false;
            iterator.NextVisible(enterChildren);
        }

        // 마지막 줄 뒤에 한 번 더 더해진 여백을 되돌립니다.
        if (total > 0f) total -= EditorGUIUtility.standardVerticalSpacing;
        return total;
    }

    private static IEnumerable<Type> SafeGetTypes(Assembly asm)
    {
        try
        {
            return asm.GetTypes();
        }
        catch (ReflectionTypeLoadException e)
        {
            return e.Types.Where(t => t != null);
        }
    }
}
#endif