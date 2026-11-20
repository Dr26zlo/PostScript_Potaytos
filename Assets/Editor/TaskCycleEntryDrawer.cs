using UnityEditor;
using UnityEngine;

[CustomPropertyDrawer(typeof(TaskCycle.Entry))]
public sealed class TaskCycleEntryDrawer : PropertyDrawer
{
    private static readonly GUIContent TypeLabel = new GUIContent("Тип задания");
    private static readonly GUIContent GuestLabel = new GUIContent("Гость");
    private static readonly GUIContent DialogueLabel = new GUIContent("Диалог");
    private static readonly GUIContent DelayLabel = new GUIContent("Пауза (сек)", "Время до следующего задания");

    private static bool IsTypewriter(SerializedProperty type) =>
        !type.hasMultipleDifferentValues && type.intValue == (int)TaskCycle.TaskType.Typewriter;

    public override float GetPropertyHeight(SerializedProperty property, GUIContent label)
    {
        int rows = IsTypewriter(property.FindPropertyRelative("type")) ? 4 : 3;
        return rows * EditorGUIUtility.singleLineHeight + (rows - 1) * EditorGUIUtility.standardVerticalSpacing;
    }

    public override void OnGUI(Rect position, SerializedProperty property, GUIContent label)
    {
        EditorGUI.BeginProperty(position, label, property);

        var type = property.FindPropertyRelative("type");
        var row = new Rect(position.x, position.y, position.width, EditorGUIUtility.singleLineHeight);
        float stride = row.height + EditorGUIUtility.standardVerticalSpacing;
        EditorGUI.PropertyField(row, type, TypeLabel);

        row.y += stride;
        if (type.hasMultipleDifferentValues)
        {
            EditorGUI.LabelField(row, "Выберите общий тип задания");
        }
        else if (IsTypewriter(type))
        {
            EditorGUI.PropertyField(row, property.FindPropertyRelative("guest"), GuestLabel);
            row.y += stride;
            EditorGUI.PropertyField(row, property.FindPropertyRelative("rewardItem"), new GUIContent("Награда"));
        }
        else
        {
            EditorGUI.PropertyField(row, property.FindPropertyRelative("dialogue"), DialogueLabel);
        }

        row.y += stride;
        EditorGUI.PropertyField(row, property.FindPropertyRelative("delayToNextTask"), DelayLabel);

        EditorGUI.EndProperty();
    }
}

[CustomEditor(typeof(TaskCycle))]
[CanEditMultipleObjects]
public sealed class TaskCycleInspector : Editor
{
    public override void OnInspectorGUI()
    {
        serializedObject.Update();
        EditorGUILayout.PropertyField(serializedObject.FindProperty("tasks"), true);
        EditorGUILayout.PropertyField(serializedObject.FindProperty("repeat"), new GUIContent("Повторять цикл"));
        EditorGUILayout.PropertyField(serializedObject.FindProperty("guestDoor"), new GUIContent("Дверь гостей"));
        serializedObject.ApplyModifiedProperties();
    }
}
