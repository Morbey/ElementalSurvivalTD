using System;
using ElementalTD.Core;
using UnityEngine;

namespace ElementalTD.Data
{
    /// <summary>An element: its id is what Core compares, its display name what the player reads (ADR 0005).</summary>
    [CreateAssetMenu(fileName = "Element_", menuName = "ElementalTD/Element Definition")]
    public sealed class ElementDefinition : ScriptableObject
    {
        [Tooltip("Unique among the elements of the chart; at least 1.")]
        [SerializeField, Min(1)] private int id = 1;
        [SerializeField] private string displayName = "";

        public string DisplayName => displayName;

        /// <summary>Throws for an id below 1, so an asset can never stand for no element.</summary>
        public Element ToElement()
        {
            if (id < 1) throw new InvalidOperationException($"{name}: an element id must be at least 1, not {id}.");
            return new Element(id);
        }
    }
}
