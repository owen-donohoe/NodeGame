using System;
using System.Collections.Generic;
using System.Globalization;

namespace NodeWar.Lobby
{
    public enum ClaimActionKind { Ok, Claim, Price }

    public readonly struct ClaimAction
    {
        public ClaimActionKind Kind { get; }
        public string CurrencyKind { get; }
        public long Amount { get; }
        private ClaimAction(ClaimActionKind kind, string currencyKind = null, long amount = 0)
        {
            Kind = kind; CurrencyKind = currencyKind; Amount = amount;
        }
        public static ClaimAction Ok => new ClaimAction(ClaimActionKind.Ok);
        public static ClaimAction Claim => new ClaimAction(ClaimActionKind.Claim);
        public static ClaimAction Price(string currencyKind, long amount)
        {
            if (string.IsNullOrWhiteSpace(currencyKind)) throw new ArgumentException("Price needs a currency", nameof(currencyKind));
            if (amount < 0) throw new ArgumentOutOfRangeException(nameof(amount));
            return new ClaimAction(ClaimActionKind.Price, currencyKind, amount);
        }
        public string PrimaryLabel() => Kind == ClaimActionKind.Ok ? "OK" : Kind == ClaimActionKind.Claim ? "Claim" :
            Amount.ToString(CultureInfo.InvariantCulture) + " " + CurrencyKind;
    }

    public sealed class ClaimRequest
    {
        public string Title { get; }
        public string Description { get; }
        public IReadOnlyList<string> Disclosure { get; }
        public ClaimAction Action { get; }

        public ClaimRequest(string title, string description, ClaimAction action, string[] disclosure = null)
        {
            Title = title ?? "";
            Description = description ?? "";
            Action = action;
            Disclosure = disclosure == null ? null : Array.AsReadOnly((string[])disclosure.Clone());
        }
        public string PrimaryLabel() => Action.PrimaryLabel();
    }
}
