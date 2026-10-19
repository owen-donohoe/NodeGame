using UnityEngine.UIElements;

namespace NodeWar.Lobby
{
    /// <summary>Compatibility facade for page-owned content in the shared MkSheet.</summary>
    public class LobbySheet
    {
        private readonly MkSheet sheet;
        private VisualElement content;
        public bool IsOpen => sheet.IsOpen;
        public event System.Action<VisualElement> Closed;
        public event System.Action<VisualElement> Opened;

        public LobbySheet(VisualElement root)
        {
            var layer = root.Q<VisualElement>("sheet-layer");
            layer.Clear();
            sheet = new MkSheet { name = "sheet" };
            sheet.Scrim.name = "sheet-scrim";
            sheet.Handle.name = "sheet-handle";
            sheet.Body.name = "sheet-body";
            layer.Add(sheet);
            sheet.Closed += () => Closed?.Invoke(content);
        }
        public static VisualElement Lift(VisualElement pageRoot, string contentName)
        {
            var result = pageRoot?.Q<VisualElement>(contentName);
            if (result == null) return null;
            result.RemoveFromHierarchy();
            for (int i = 0; i < pageRoot.styleSheets.count; i++) result.styleSheets.Add(pageRoot.styleSheets[i]);
            return result;
        }
        public bool IsShowing(VisualElement candidate) => IsOpen && candidate != null && content == candidate;
        public void Open(VisualElement newContent, bool large = false)
        {
            if (newContent == null) return;
            if (IsOpen && content != newContent) Close();
            if (newContent.parent != sheet.Body.contentContainer)
            {
                sheet.Body.Clear(); sheet.Body.Add(newContent);
            }
            content = newContent;
            sheet.Open(large);
            Opened?.Invoke(content);
        }
        public void Close() => sheet.Close();
    }
}
