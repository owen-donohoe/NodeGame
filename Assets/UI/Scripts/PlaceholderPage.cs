using UnityEngine.UIElements;

namespace NodeWar.Lobby
{
    /// <summary>
    /// A page that exists so the shell has something to show, and says so.
    ///
    /// S1 builds the shell before any real page exists. Rather than leave four
    /// tabs leading nowhere, each gets one of these: a labelled empty page that
    /// names the session due to replace it. The .placeholder class makes it
    /// visually obvious that nothing here is real, so a screenshot of the
    /// half-built lobby cannot be mistaken for a working one.
    ///
    /// Every instance of this is deleted as its page lands in S2-S4. If any
    /// survive to S5, that is a page nobody built.
    /// </summary>
    public class PlaceholderPage : LobbyPage
    {
        public PlaceholderPage(LobbyPageID id, string dueIn)
            : base(id, Build(id, dueIn))
        {
        }

        private static VisualElement Build(LobbyPageID id, string dueIn)
        {
            VisualElement root = new VisualElement();
            root.name = "page-" + id.ToString().ToLowerInvariant();

            VisualElement box = new VisualElement();
            box.AddToClassList("placeholder");
            box.style.flexGrow = 1;

            Label title = new Label(id.ToString());
            title.AddToClassList("placeholder__label");
            title.style.fontSize = 22;

            Label note = new Label("Not built yet - " + dueIn);
            note.AddToClassList("placeholder__label");

            box.Add(title);
            box.Add(note);
            root.Add(box);

            return root;
        }
    }
}
