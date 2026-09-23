namespace NodeWar.UI
{
    /// <summary>
    /// Which of the three resources something is about: the node sheet's
    /// content, for the resource chip row NodeSheet draws above itself, and
    /// the HUD rings' production, for which resource a working villager will
    /// hand its owner.
    ///
    /// A flags enum because Market and Equip involve more than one at once.
    /// ResourceProduction asks about exactly one bit at a time, which is a
    /// narrower use of the same vocabulary rather than a second one.
    ///
    /// Its own file, out of NodeSheetContent.cs where it started, for the
    /// reason ResourceRingMath has its own: everything that reads it on the
    /// HUD side is UnityEngine-free and tested in dotnet/NodeWar.View.Tests,
    /// and NodeSheetContent is neither.
    /// </summary>
    [System.Flags]
    public enum ResourceKind
    {
        None = 0,
        Food = 1,
        Materials = 2,
        Metal = 4
    }
}
