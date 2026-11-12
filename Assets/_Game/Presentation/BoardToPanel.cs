using ElementalTD.Data;
using UnityEngine;
using UnityEngine.UIElements;

namespace ElementalTD.Presentation
{
    /// <summary>
    /// Where a board position lies on the HUD panel. Built once per frame from two camera reads; a label then costs
    /// arithmetic. The board camera looks straight down with no turn (BoardCameraFramer), so cell x maps to panel x
    /// and cell y to panel y, each by a scale and an offset (ADR 0006).
    /// </summary>
    internal readonly struct BoardToPanel
    {
        private readonly Vector2 _origin;
        private readonly Vector2 _perCell;

        public BoardToPanel(Camera camera, IPanel panel, MapDefinition map)
        {
            _origin = RuntimePanelUtils.CameraTransformWorldToPanel(panel, map.CellToWorld(0f, 0f), camera);
            _perCell = RuntimePanelUtils.CameraTransformWorldToPanel(panel, map.CellToWorld(1f, 1f), camera) - _origin;
        }

        /// <summary>Panel coordinates of a position in cells; a larger cell y lies higher on the screen.</summary>
        public Vector2 Point(float cellX, float cellY) =>
            new Vector2(_origin.x + cellX * _perCell.x, _origin.y + cellY * _perCell.y);
    }
}
