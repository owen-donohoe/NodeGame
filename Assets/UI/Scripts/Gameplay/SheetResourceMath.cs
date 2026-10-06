namespace NodeWar.UI
{
    /// <summary>Pure layout and value-feedback decisions for the sheet's resource stack.</summary>
    public static class SheetResourceMath
    {
        public static bool Expanded(ResourceKind resource, ResourceKind involved, bool visible)
        {
            return visible && resource != ResourceKind.None && (resource & involved) != 0;
        }

        public static bool Bounce(int previous, int current, bool hasValue, bool reducedMotion, bool viewerChanged)
        {
            return hasValue && previous != current && !reducedMotion && !viewerChanged;
        }
    }
}
