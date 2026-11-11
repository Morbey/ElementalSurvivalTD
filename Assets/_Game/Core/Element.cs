using System;

namespace ElementalTD.Core
{
    /// <summary>An element by its id, written on its asset (ADR 0005). The default value is no element.</summary>
    public readonly struct Element : IEquatable<Element>
    {
        /// <summary>0: no element; an element's id is at least 1.</summary>
        public readonly int Id;

        public Element(int id)
        {
            if (id < 0) throw new ArgumentOutOfRangeException(nameof(id), id, "An element id cannot be negative.");
            Id = id;
        }

        public bool IsNone => Id == 0;

        public bool Equals(Element other) => Id == other.Id;
        public override bool Equals(object obj) => obj is Element other && Equals(other);
        public override int GetHashCode() => Id;
        public static bool operator ==(Element a, Element b) => a.Id == b.Id;
        public static bool operator !=(Element a, Element b) => a.Id != b.Id;
        public override string ToString() => IsNone ? "Element(none)" : $"Element({Id})";
    }
}
