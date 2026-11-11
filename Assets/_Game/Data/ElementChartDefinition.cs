using System;
using ElementalTD.Core;
using UnityEngine;

namespace ElementalTD.Data
{
    /// <summary>
    /// The elements and, per ordered pair of attacker and defender, a damage multiplier; a pair not listed deals x1 (ADR 0005).
    /// Immunity to the own element is a Core rule, so no relation may pair an element with itself.
    /// </summary>
    [CreateAssetMenu(fileName = "ElementChart", menuName = "ElementalTD/Element Chart")]
    public sealed class ElementChartDefinition : ScriptableObject
    {
        [Serializable]
        private sealed class Relation
        {
            [SerializeField] private ElementDefinition attacker;
            [SerializeField] private ElementDefinition defender;
            [Tooltip("Must be above 0.")]
            [SerializeField, Min(0f)] private float damageMultiplier = 1f;

            public ElementDefinition Attacker => attacker;
            public ElementDefinition Defender => defender;
            public float DamageMultiplier => damageMultiplier;
        }

        [SerializeField] private ElementDefinition[] elements = new ElementDefinition[0];
        [SerializeField] private Relation[] relations = new Relation[0];

        /// <summary>
        /// Throws for a missing reference, two elements with one id and a relation whose element is not listed;
        /// the Core chart refuses a pair of one element and a multiplier at or below 0.
        /// </summary>
        public ElementChart ToChart()
        {
            int elementCount = elements == null ? 0 : elements.Length;
            int highestId = 0;
            for (int i = 0; i < elementCount; i++)
            {
                if (elements[i] == null) throw new InvalidOperationException($"{name}: element {i + 1} is missing.");
                int id = elements[i].ToElement().Id;
                for (int j = 0; j < i; j++)
                    if (elements[j].ToElement().Id == id)
                        throw new InvalidOperationException($"{name}: {elements[j].name} and {elements[i].name} share the id {id}.");
                highestId = Math.Max(highestId, id);
            }

            var chart = new ElementChart(highestId);
            int relationCount = relations == null ? 0 : relations.Length;
            for (int i = 0; i < relationCount; i++)
            {
                Relation relation = relations[i];
                if (relation == null || relation.Attacker == null || relation.Defender == null)
                    throw new InvalidOperationException($"{name}: relation {i + 1} has a missing element.");
                if (!IsListed(relation.Attacker) || !IsListed(relation.Defender))
                    throw new InvalidOperationException($"{name}: relation {i + 1} uses an element the chart does not list.");
                chart.Set(relation.Attacker.ToElement(), relation.Defender.ToElement(), relation.DamageMultiplier);
            }
            return chart;
        }

        private bool IsListed(ElementDefinition element)
        {
            if (elements == null) return false;
            for (int i = 0; i < elements.Length; i++)
                if (elements[i] == element) return true;
            return false;
        }
    }
}
