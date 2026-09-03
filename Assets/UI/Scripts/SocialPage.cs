using UnityEngine.UIElements;

namespace NodeWar.Lobby
{
    /// <summary>
    /// Social. A marked placeholder, and deliberately nothing else.
    ///
    /// There is no friends list, club, presence or chat in the project - not a
    /// stub, not a data field. A screen of empty lists would imply a system
    /// that has not been designed, which is exactly what the placeholder rule
    /// exists to prevent.
    ///
    /// No state to read, so no OnShow. The one useful thing it says - that the
    /// join code in the Play sheet is how two people actually play each other
    /// today - is static text in the layout.
    /// </summary>
    public class SocialPage : LobbyPage
    {
        public SocialPage(VisualTreeAsset layout)
            : base(LobbyPageID.Social, Build(layout))
        {
        }

        private static VisualElement Build(VisualTreeAsset layout)
        {
            VisualElement root = new VisualElement();
            root.name = "page-social";

            if (layout != null)
            {
                layout.CloneTree(root);
            }
            else
            {
                VisualElement box = new VisualElement();
                box.AddToClassList("placeholder");
                box.style.flexGrow = 1;

                Label note = new Label("Social layout missing - assign SocialPage.uxml");
                note.AddToClassList("placeholder__label");

                box.Add(note);
                root.Add(box);
            }

            return root;
        }
    }
}
