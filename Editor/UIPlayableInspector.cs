using System.Collections.Generic;
using UnityEngine;
using UnityEditor;
using UnityEditorInternal;

[CustomEditor(typeof(UIPlayable))]
public class UIPlayableInspector : Editor
{

    UIPlayable playable => target as UIPlayable;
    ReorderableList m_ReorderableList;

    public void OnEnable()
    {
        m_ReorderableList = new ReorderableList(playable.states, typeof(UIPlayable.State), true, true, true, true);
        m_ReorderableList.drawHeaderCallback = OnDrawHeader;
        m_ReorderableList.drawElementCallback = OnDrawElement;
        m_ReorderableList.elementHeightCallback = GetElementHeight;
        m_ReorderableList.onAddCallback = OnAddElement;
    }

    public override void OnInspectorGUI()
    {
        DrawDefaultState();
        m_ReorderableList.DoLayoutList();
        serializedObject.ApplyModifiedProperties();
    }

    void DrawDefaultState()
    {
        UIPlayable.State defaultState = playable.defaultState;
        int defaultStateIndex = playable.states.IndexOf(defaultState);
        List<string> options = new List<string> { "None" };
        foreach (UIPlayable.State state in playable.states)
        {
            if (string.IsNullOrEmpty(state.name))
            {
                options.Add("");
                options.Add("");
            }
            else
            {
                options.Add(state.name);
                options.Add(state.name + " (End)");
            }
        }

        int currentIndex = 0;
        if (defaultStateIndex >= 0)
        {
            currentIndex = defaultStateIndex * 2 + 1;
            if (playable.defaultStateAnimation == UIPlayable.StateAnimationType.Loop)
                currentIndex += 1;
        }

        int newIndex = EditorGUILayout.Popup("Default State", currentIndex, options.ToArray());
        if (newIndex != currentIndex)
        {
            if (newIndex == 0)
            {
                playable.defaultState = null;
                playable.defaultStateAnimation = UIPlayable.StateAnimationType.Enter;
            }
            else
            {
                UIPlayable.State state = playable.states[(newIndex - 1) / 2];
                playable.defaultState = state;
                playable.defaultStateAnimation = newIndex % 2 == 1 ? UIPlayable.StateAnimationType.Enter : UIPlayable.StateAnimationType.Loop;
            }
            EditorUtility.SetDirty(playable);
        }
    }

    void OnDrawHeader(Rect rect)
    {
        GUI.Label(rect, "States");
    }

    void OnDrawElement(Rect rect, int index, bool isactive, bool isfocused)
    {
        SerializedProperty state = serializedObject.FindProperty("states").GetArrayElementAtIndex(index);
        float y = rect.yMin + 2;

        DrawPropertyField("name");
        DrawPropertyField("animation");
        DrawPropertyField("loopAnimation");
        DrawPropertyField("onAnimationEnd");

        void DrawPropertyField(string name)
        {
            SerializedProperty property = state.FindPropertyRelative(name);
            float h = EditorGUI.GetPropertyHeight(property);
            float w = rect.width;
            if (Application.isPlaying && name == "name")
            {
                w -= 80;
                if (GUI.Button(new Rect(rect.xMax - 80, y, 80, 16), "Play"))
                    playable.Play(playable.states[index]);
            }
            EditorGUI.PropertyField(new Rect(rect.x, y, w, h), property);
            y += h + 2;
        }
    }

    float GetElementHeight(int index)
    {
        SerializedProperty state = serializedObject.FindProperty("states").GetArrayElementAtIndex(index);
        return 2 + 18 * 3 + EditorGUI.GetPropertyHeight(state.FindPropertyRelative("onAnimationEnd")) + 10;
    }

    void OnAddElement(ReorderableList list)
    {
        playable.states.Add(new UIPlayable.State());
    }
}
