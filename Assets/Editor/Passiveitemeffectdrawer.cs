#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using UnityEditor;
using UnityEngine;

// ItemDefinition.specialEffects([SerializeReference] List<IPassiveItemEffect>) 리스트의 각
// 항목 인스펙터 표시를 담당하는 에디터 전용 드로어입니다. 반드시 폴더 이름이 "Editor"인
// 곳에 둬야 합니다 (예: Assets/Editor/PassiveItemEffectDrawer.cs) - 그래야 Unity가 빌드에서
// 자동으로 제외합니다. #if UNITY_EDITOR로도 한 번 더 감싸서, 혹시 다른 폴더에 놓이더라도
// 빌드 에러가 나지 않도록 안전장치를 뒀습니다. EnemyAttackConfigDrawer.cs와 완전히 동일한
// 구조/이유입니다 - 자세한 설명은 그 파일 상단 주석 참고.
//
// [CustomPropertyDrawer(typeof(IPassiveItemEffect))]로 인터페이스 자체를 타겟팅했으므로,
// List<IPassiveItemEffect>의 각 원소에도 자동으로 이 드로어가 적용됩니다 - 리스트 컨테이너용
// 드로어를 따로 만들 필요가 없습니다.
//
// [타입 목록에서 StatModifierEffect가 안 보이는 이유]
// StatModifierEffect는 생성자가 List<StatModifier>를 요구해서(매개변수 없는 생성자가 없음)
// Activator.CreateInstance(type)으로 새로 만들 수 없습니다. 그래서 아래 후보 목록을 "매개변수
// 없는 public 생성자가 있는 타입"으로만 필터링해뒀습니다 - 이렇게 하면 StatModifierEffect처럼
// specialEffects 자리에 올 수 없는 타입은 자동으로 걸러지고, 앞으로 추가할 특수 효과 클래스는
// 항상 매개변수 없는 생성자를 갖도록(DashAttackConfig/ShootAttackConfig와 동일한 방식, 즉
// public 필드 + 기본값만으로 구성) 만들면 자동으로 여기 나타납니다.
[CustomPropertyDrawer(typeof(IPassiveItemEffect))]
public class PassiveItemEffectDrawer : PropertyDrawer
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
            .Where(t => typeof(IPassiveItemEffect).IsAssignableFrom(t) && !t.IsInterface && !t.IsAbstract &&
                        t.GetConstructor(Type.EmptyTypes) != null)
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