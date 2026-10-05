#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using NodeWar.Lobby;
using UnityEditor;
using UnityEngine;

namespace NodeWar.UI.Editor
{
    [CustomPropertyDrawer(typeof(UIArtTheme.IconEntry))]
    public sealed class UIArtThemeIconEntryDrawer : PropertyDrawer
    {
        private const float HelpHeight = 46f;
        private static float Step => EditorGUIUtility.singleLineHeight + EditorGUIUtility.standardVerticalSpacing;

        public override float GetPropertyHeight(SerializedProperty property, GUIContent label)
        {
            return Step * 5f + (Invalid(property) ? HelpHeight + EditorGUIUtility.standardVerticalSpacing : 0f);
        }

        private static bool Invalid(SerializedProperty property)
        {
            return !LobbyIconUsage.IsValidThemePair(
                (LobbyIconKind)property.FindPropertyRelative("kind").intValue,
                (LobbyIconContext)property.FindPropertyRelative("context").intValue);
        }

        public override void OnGUI(Rect position, SerializedProperty property, GUIContent label)
        {
            EditorGUI.BeginProperty(position, label, property);
            int oldIndent = EditorGUI.indentLevel;
            try
            {
                var row = new Rect(position.x, position.y, position.width, EditorGUIUtility.singleLineHeight);
                EditorGUI.LabelField(row, label, EditorStyles.boldLabel);
                EditorGUI.indentLevel = oldIndent + 1;
                row.y += Step;
                var kindProperty = property.FindPropertyRelative("kind");
                var contextProperty = property.FindPropertyRelative("context");
                var kind = (LobbyIconKind)kindProperty.intValue;
                var context = (LobbyIconContext)contextProperty.intValue;

                var kinds = new List<LobbyIconUsage.Icon>();
                foreach (var icon in LobbyIconUsage.All)
                    if (!icon.Retired || icon.Kind == kind) kinds.Add(icon);
                // Preserve unknown saved values instead of silently selecting or overwriting another kind.
                if (LobbyIconUsage.For(kind) == null)
                    kinds.Add(new LobbyIconUsage.Icon(kind, "Unrecognized", "Saved value " + kindProperty.intValue));
                kinds.Sort((a, b) =>
                {
                    int order = StringComparer.Ordinal.Compare(a.Path, b.Path);
                    return order != 0 ? order : ((int)a.Kind).CompareTo((int)b.Kind);
                });
                var names = new GUIContent[kinds.Count];
                int selectedKind = -1;
                for (int i = 0; i < kinds.Count; i++)
                {
                    names[i] = new GUIContent(kinds[i].Path);
                    if (kinds[i].Kind == kind) selectedKind = i;
                }
                EditorGUI.BeginChangeCheck();
                int nextKind = EditorGUI.Popup(row, new GUIContent("Icon"), selectedKind, names);
                if (EditorGUI.EndChangeCheck() && nextKind >= 0)
                {
                    kind = kinds[nextKind].Kind;
                    kindProperty.intValue = (int)kind;
                    if (!LobbyIconUsage.IsValidThemePair(kind, context))
                    {
                        context = LobbyIconContext.Anywhere;
                        contextProperty.intValue = (int)context;
                    }
                }

                row.y += Step;
                var choices = LobbyIconUsage.WhereChoices(kind);
                var whereNames = new GUIContent[choices.Length];
                int selectedWhere = -1;
                for (int i = 0; i < choices.Length; i++)
                {
                    whereNames[i] = new GUIContent(choices[i].Label);
                    if (choices[i].Context == context) selectedWhere = i;
                }
                string hint = "Choose where this icon appears, or Anywhere for every use.";
                // Existing single-location overrides still match. Display their equivalent Anywhere
                // choice without rewriting saved data just because the Inspector was opened.
                if (selectedWhere < 0 && choices.Length == 1 && LobbyIconUsage.IsUsed(kind, context))
                {
                    selectedWhere = 0;
                    hint = "Saved location: " + LobbyIconUsage.For(kind).Locations[0].Label +
                        ". This icon has one location; Anywhere covers the same use. The saved value is preserved.";
                }
                EditorGUI.BeginChangeCheck();
                int nextWhere = EditorGUI.Popup(row, new GUIContent("Where", hint), selectedWhere, whereNames);
                if (EditorGUI.EndChangeCheck() && nextWhere >= 0)
                    contextProperty.intValue = (int)choices[nextWhere].Context;

                row.y += Step;
                EditorGUI.PropertyField(row, property.FindPropertyRelative("sprite"), new GUIContent("Sprite"));
                row.y += Step;
                EditorGUI.PropertyField(row, property.FindPropertyRelative("keepOriginalColours"), new GUIContent("Keep original colours"));
                row.y += Step;
                if (Invalid(property))
                {
                    row.height = HelpHeight;
                    EditorGUI.HelpBox(row, "This saved icon/Where pair has no active use and will never match. " +
                        "Choose an active icon and a listed location (or Anywhere).", MessageType.Error);
                }
            }
            finally
            {
                EditorGUI.indentLevel = oldIndent;
                EditorGUI.EndProperty();
            }
        }
    }
}
#endif
