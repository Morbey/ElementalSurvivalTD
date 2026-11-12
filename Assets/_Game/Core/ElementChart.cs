using System;

namespace ElementalTD.Core
{
    /// <summary>
    /// The damage multiplier of an attacking element against a defending one (ADR 0005).
    /// Immunity to the own element is a rule here, not a value: no pair of one element can be stored.
    /// </summary>
    public sealed class ElementChart
    {
        private const float Immune = 0f;
        private const float NotListed = 1f;

        private readonly int _elementCount;
        private readonly float[] _multipliers;

        /// <summary>Holds the ids 1 to elementCount; 0 makes an empty chart.</summary>
        public ElementChart(int elementCount)
        {
            if (elementCount < 0) throw new ArgumentOutOfRangeException(nameof(elementCount), elementCount, "The element count cannot be negative.");
            _elementCount = elementCount;
            _multipliers = new float[elementCount * elementCount];
            for (int i = 0; i < _multipliers.Length; i++) _multipliers[i] = NotListed;
        }

        /// <summary>Rejects no element, an id outside the chart, one same element on both sides and a multiplier at or below 0.</summary>
        public void Set(Element attacker, Element defender, float multiplier)
        {
            CheckInChart(attacker, nameof(attacker));
            CheckInChart(defender, nameof(defender));
            if (attacker == defender)
                throw new ArgumentException($"{attacker} is immune to itself; the pair cannot be set.", nameof(defender));
            if (!(multiplier > 0f))
                throw new ArgumentOutOfRangeException(nameof(multiplier), multiplier, "A multiplier must be above 0.");

            _multipliers[IndexOf(attacker, defender)] = multiplier;
        }

        /// <summary>0 for one same element; otherwise the value set, or 1: never set, no element, or an id outside the chart.</summary>
        public float Multiplier(Element attacker, Element defender)
        {
            if (attacker.IsNone || defender.IsNone) return NotListed;
            if (attacker == defender) return Immune;
            if (attacker.Id > _elementCount || defender.Id > _elementCount) return NotListed;
            return _multipliers[IndexOf(attacker, defender)];
        }

        /// <summary>Immune at 0, weak below 1, normal at 1, strong above 1.</summary>
        public static HitKind KindOf(float multiplier)
        {
            if (multiplier == Immune) return HitKind.Immune;
            if (multiplier < NotListed) return HitKind.Weak;
            return multiplier == NotListed ? HitKind.Normal : HitKind.Strong;
        }

        private void CheckInChart(Element element, string name)
        {
            if (element.IsNone) throw new ArgumentException("No element has no relations.", name);
            if (element.Id > _elementCount)
                throw new ArgumentOutOfRangeException(name, element.Id, $"The chart holds the ids 1 to {_elementCount}.");
        }

        private int IndexOf(Element attacker, Element defender) => (attacker.Id - 1) * _elementCount + defender.Id - 1;
    }
}
