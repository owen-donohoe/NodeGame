using System;

namespace NodeWar.Lobby
{
    public static class LobbyCurrencyNames
    {
        public const string Premium = "Laurels";
        public static string Display(string currencyKind) => string.Equals(currencyKind, "Premium", StringComparison.OrdinalIgnoreCase)
            ? Premium : currencyKind;
    }
}
