using ElementalTD.Data;
using ElementalTD.Gameplay;
using UnityEngine;

namespace ElementalTD.Presentation
{
    /// <summary>Keeps the whole grid in view from straight above, re-framing when the view's aspect changes.</summary>
    public sealed class BoardCameraFramer : MonoBehaviour
    {
        [SerializeField] private PrototypeBootstrap bootstrap;
        [SerializeField] private Camera viewCamera;
        [Tooltip("Extra world units around the grid when framing the camera, in cells.")]
        [SerializeField, Min(0f)] private float framePaddingCells = 0.5f;
        [SerializeField, Min(1f)] private float cameraHeight = 20f;

        private MapDefinition _map;
        private float _framedAspect;

        private void Start()
        {
            if (bootstrap == null || bootstrap.Map == null || viewCamera == null)
            {
                Debug.LogError("BoardCameraFramer: assign the bootstrap, its map and the camera.", this);
                enabled = false;
                return;
            }

            _map = bootstrap.Map;
            Frame();
        }

        private void LateUpdate()
        {
            if (viewCamera.aspect != _framedAspect) Frame();
        }

        private void Frame()
        {
            float worldWidth = _map.Width * _map.CellSize;
            float worldDepth = _map.Height * _map.CellSize;
            float padding = framePaddingCells * _map.CellSize;

            _framedAspect = viewCamera.aspect;
            viewCamera.orthographic = true;
            viewCamera.transform.SetPositionAndRotation(
                new Vector3(worldWidth * 0.5f, cameraHeight, worldDepth * 0.5f),
                Quaternion.Euler(90f, 0f, 0f));
            viewCamera.orthographicSize =
                Mathf.Max(worldDepth * 0.5f, worldWidth * 0.5f / _framedAspect) + padding;
        }
    }
}
