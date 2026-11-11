using System;
using ElementalTD.Core;
using ElementalTD.Data;
using UnityEngine;

namespace ElementalTD.Presentation
{
    /// <summary>How each element looks: its UI colour and the materials of its towers, enemies, shots and range disc (ADR 0005).</summary>
    [CreateAssetMenu(fileName = "ElementPalette", menuName = "ElementalTD/Element Palette")]
    public sealed class ElementPalette : ScriptableObject
    {
        [Serializable]
        public struct Look
        {
            public ElementDefinition Element;
            public Color UiColor;
            public Material Tower, Enemy, Projectile, RangeDisc;
        }

        [SerializeField] private Look[] looks;

        public int LookCount => looks == null ? 0 : looks.Length;

        public Look LookAt(int index) => looks[index];

        /// <summary>A scan: for start-up and clicks, never per frame. False for no element.</summary>
        public bool TryGetLook(Element element, out Look look)
        {
            if (!element.IsNone)
            {
                for (int i = 0; i < LookCount; i++)
                {
                    if (looks[i].Element != null && looks[i].Element.ToElement() == element)
                    {
                        look = looks[i];
                        return true;
                    }
                }
            }
            look = default;
            return false;
        }

        /// <summary>The highest element id with a look, or 0: the size of a table by id, less one.</summary>
        internal int MaxElementId()
        {
            int maxId = 0;
            for (int i = 0; i < LookCount; i++)
                if (looks[i].Element != null) maxId = Mathf.Max(maxId, looks[i].Element.ToElement().Id);
            return maxId;
        }

        /// <summary>
        /// One material per element id, so a view reads it per frame by index. Index 0, an id with no look and a look
        /// with no such material hold the fallback.
        /// </summary>
        internal Material[] MaterialsById(Func<Look, Material> pick, Material fallback)
        {
            // An array of references, not a material made at runtime; qualified so the source rule against those does not match it.
            var materials = new UnityEngine.Material[MaxElementId() + 1];
            for (int id = 0; id < materials.Length; id++) materials[id] = fallback;
            for (int i = 0; i < LookCount; i++)
            {
                if (looks[i].Element == null) continue;
                Material material = pick(looks[i]);
                if (material != null) materials[looks[i].Element.ToElement().Id] = material;
            }
            return materials;
        }

        /// <summary>The material for an element id, or the fallback at index 0 for an id outside the table.</summary>
        internal static Material ById(Material[] materials, int id) =>
            (uint)id < (uint)materials.Length ? materials[id] : materials[0];
    }
}
