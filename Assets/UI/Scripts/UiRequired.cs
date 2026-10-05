using UnityEngine;
using UnityEngine.UIElements;

namespace NodeWar.UI
{
    /// <summary>Diagnose broken UXML contracts without stopping the rest of binding.</summary>
    public static class UiRequired
    {
        public static T Q<T>(VisualElement root, string name, string owner) where T : VisualElement
        {
            VisualElement actual = root == null ? null : root.Q<VisualElement>(name);
            T result = actual as T;
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            if (result == null)
                Debug.LogError("[UiRequired] " + owner + " requires '" + name + "': expected " +
                    typeof(T).FullName + ", actual " + (actual == null ? "missing" : actual.GetType().FullName) + ".");
#endif
            return result;
        }
    }
}
