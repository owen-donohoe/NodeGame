using System;

namespace NodeWar.Lobby
{
    public enum LoadoutSort { OwnedFirst, Name, Role }

    public readonly struct LoadoutTileOrder
    {
        public readonly string ID, Name, Role;
        public readonly bool Owned;
        public readonly int Era;
        public LoadoutTileOrder(string id, string name, string role, bool owned, int era)
        { ID = id; Name = name; Role = role; Owned = owned; Era = era; }
    }

    /// <summary>Filter and total ordering shared by both inventory tabs.</summary>
    public static class LoadoutBrowse
    {
        public static bool Includes(bool showAll, bool owned) => showAll || owned;
        public static int Compare(LoadoutTileOrder a, LoadoutTileOrder b, LoadoutSort sort)
        {
            int order;
            if (sort == LoadoutSort.Name) order = StringComparer.Ordinal.Compare(a.Name, b.Name);
            else if (sort == LoadoutSort.Role)
            {
                order = StringComparer.Ordinal.Compare(a.Role, b.Role);
                if (order == 0) order = a.Era.CompareTo(b.Era);
            }
            else
            {
                order = b.Owned.CompareTo(a.Owned);
                if (order == 0) order = a.Era.CompareTo(b.Era);
                if (order == 0) order = StringComparer.Ordinal.Compare(a.Name, b.Name);
            }
            return order != 0 ? order : StringComparer.Ordinal.Compare(a.ID, b.ID);
        }
    }
}
