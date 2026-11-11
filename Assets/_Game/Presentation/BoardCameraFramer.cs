using ElementalTD.Data;
using ElementalTD.Gameplay;
using UnityEngine;
using UnityEngine.UIElements;

namespace ElementalTD.Presentation
{
    /// <summary>
    /// Keeps the whole grid in view from straight above, fitted into the screen rectangle of the HUD's board area,
    /// re-framing when the view's aspect or that rectangle changes (ADR 0002). Until the HUD has been laid out,
    /// it frames the whole screen.
    /// </summary>
    public sealed class BoardCameraFramer : MonoBehaviour
    {
        private const string BoardAreaName = "board-area";

        [SerializeField] private PrototypeBootstrap bootstrap;
        [SerializeField] private Camera viewCamera;
        [SerializeField] private UIDocument document;
        [Tooltip("Extra world units around the grid when framing the camera, in cells.")]
        [SerializeField, Min(0f)] private float framePaddingCells = 0.5f;
        [SerializeField, Min(1f)] private float cameraHeight = 20f;

        private static readonly Rect WholeScreen = new Rect(0f, 0f, 1f, 1f);

        private MapDefinition _map;
        private VisualElement _root;
        private VisualElement _boardArea;
        private float _framedAspect;
        private Rect _framedArea;

        private void Start()
        {
            _root = document != null ? document.rootVisualElement : null;
            _boardArea = _root?.Q<VisualElement>(BoardAreaName);
            if (bootstrap == null || bootstrap.Map == null || viewCamera == null || _boardArea == null)
            {
                Debug.LogError($"BoardCameraFramer: assign the bootstrap, its map, the camera and the UI document (with {BoardAreaName}).", this);
                enabled = false;
                return;
            }

            _map = bootstrap.Map;
            Frame(BoardAreaOnScreen());
        }

        private void LateUpdate()
        {
            Rect area = BoardAreaOnScreen();
            if (viewCamera.aspect != _framedAspect || area != _framedArea) Frame(area);
        }

        /// <summary>The board area as a fraction of the screen, from its bottom-left corner; the whole screen until it has a size.</summary>
        private Rect BoardAreaOnScreen()
        {
            Rect screen = _root.worldBound;
            Rect area = _boardArea.worldBound;
            // NaN before the first layout fails both comparisons.
            if (!(screen.width > 0f && screen.height > 0f && area.width > 0f && area.height > 0f)) return WholeScreen;

            float width = area.width / screen.width;
            float height = area.height / screen.height;
            float x = (area.xMin - screen.xMin) / screen.width;
            float yFromTop = (area.yMin - screen.yMin) / screen.height; // the panel's y grows downwards
            return new Rect(x, 1f - yFromTop - height, width, height);
        }

        private void Frame(Rect area)
        {
            float padding = framePaddingCells * _map.CellSize;
            float boardWidth = _map.Width * _map.CellSize;
            float boardDepth = _map.Height * _map.CellSize;
            float neededWidth = boardWidth + 2f * padding;
            float neededDepth = boardDepth + 2f * padding;

            _framedAspect = viewCamera.aspect;
            _framedArea = area;

            float areaAspect = _framedAspect * area.width / area.height;
            float areaWorldHeight = Mathf.Max(neededDepth, neededWidth / areaAspect);
            float orthographicSize = areaWorldHeight / area.height * 0.5f;

            // The board's centre sits at the area's centre: the camera moves by the area's offset from the screen centre.
            float screenWorldHeight = 2f * orthographicSize;
            float screenWorldWidth = screenWorldHeight * _framedAspect;
            float offsetX = (area.center.x - 0.5f) * screenWorldWidth;
            float offsetZ = (area.center.y - 0.5f) * screenWorldHeight;

            viewCamera.orthographic = true;
            viewCamera.orthographicSize = orthographicSize;
            viewCamera.transform.SetPositionAndRotation(
                new Vector3(boardWidth * 0.5f - offsetX, cameraHeight, boardDepth * 0.5f - offsetZ),
                Quaternion.Euler(90f, 0f, 0f));
        }
    }
}
