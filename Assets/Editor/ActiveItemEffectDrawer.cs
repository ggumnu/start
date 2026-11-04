#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using UnityEditor;
using UnityEngine;

// ItemDefinition.activeEffect([SerializeReference] IActiveItemEffect) 필드의 인스펙터 표시를
// 담당하는 에디터 전용 드로어입니다. PassiveItemEffectDrawer.cs와 완전히 동일한 구조입니다 -
// 자세한 설명은 그 파일 상단 주석 참고. 반드시 폴더 이름이 "Editor"인 곳에 둬야 합니다
// (예: Assets/Editor/ActiveItemEffectDrawer.cs).
//
// [CustomPropertyDrawer(typeof(IActiveItemEffect))]로 인터페이스 자체를 타겟팅했으므로,
// 지금은 단일 필드(activeEffect)에만 쓰이지만 나중에 List<IActiveItemEffect>가 생기더라도
// 각 원소에 자동으로 적용됩니다.
[CustomPropertyDrawer(typeof(IActiveItemEffect))]
public class ActiveItemEffectDrawer : PropertyDrawer
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

    private void ShowTypeMenu(SerializedProperty property)
    {
        var so = property.serializedObject;
        string path = property.propertyPath;
        var currentType = property.managedReferenceValue?.GetType();

        var menu = new GenericMenu();

        menu.AddItem(new GUIContent(NoneLabel), currentType == null, () => ApplySelection(so, path, null));

        var candidateTypes = AppDomain.CurrentDomain.GetAssemblies()
            .SelectMany(SafeGetTypes)
            .Where(t => typeof(IActiveItemEffect).IsAssignableFrom(t) && !t.IsInterface && !t.IsAbstract &&
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
