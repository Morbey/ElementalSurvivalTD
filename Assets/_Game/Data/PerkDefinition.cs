using System;
using ElementalTD.Core;
using UnityEngine;

namespace ElementalTD.Data
{
    /// <summary>A perk: one modifier and the words of its card (ADR 0007). Its place in the run's pool is its identity.</summary>
    [CreateAssetMenu(fileName = "Perk_", menuName = "ElementalTD/Perk Definition")]
    public sealed class PerkDefinition : ScriptableObject
    {
        [SerializeField] private Stat stat;
        [Tooltip("While unassigned, no element: the perk changes every tower. Only a tower stat takes an element.")]
        [SerializeField] private ElementDefinition element;
        [SerializeField] private Operation operation;
        [Tooltip("An addition above 0; a multiplication above 1. Gold per kill takes whole additions only.")]
        [SerializeField] private float value;
        [Tooltip("The card's title; {0} is the element's name.")]
        [SerializeField] private string title = "";
        [Tooltip("The attribute line of the card.")]
        [SerializeField] private string attributeText = "";

        public string Title => title;
        public string AttributeText => attributeText;
        public ElementDefinition Element => element;

        /// <summary>Throws naming the asset for a modifier that lowers a stat, or one Core refuses.</summary>
        public Modifier ToModifier()
        {
            // No perk lowers a stat: the clamp stage holds no bound yet (ADR 0007, decision 2).
            if (operation == Operation.Add && value <= 0f)
                throw new InvalidOperationException($"{name}: an addition must be above 0, not {value}.");
            if (operation == Operation.Multiply && value <= 1f)
                throw new InvalidOperationException($"{name}: a multiplication must be above 1, not {value}.");

            try
            {
                return new Modifier(stat, operation, value, element != null ? element.ToElement() : default);
            }
            catch (ArgumentException exception)
            {
                throw new InvalidOperationException($"{name}: {exception.Message}", exception);
            }
        }
    }
}
