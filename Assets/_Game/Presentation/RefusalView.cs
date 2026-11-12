using ElementalTD.Core;
using ElementalTD.Data;
using ElementalTD.Gameplay;
using UnityEngine;
using UnityEngine.UIElements;

namespace ElementalTD.Presentation
{
    /// <summary>
    /// Shows why a purchase was refused, over the refused cell: one label that rises and fades in unscaled time, because
    /// it answers a click and must last as long at any game speed. Kept inside the board area so it never covers a bar.
    /// A newer refusal restarts it at its own cell.
    /// </summary>
    // After BoardCameraFramer, so a frame that reframes the camera places the label with the new one.
    [DefaultExecutionOrder(100)]
    public sealed class RefusalView : MonoBehaviour
    {
        private const string AnchorName = "refusal-anchor";
        private const string LabelName = "refusal";
        private const string BoardAreaName = "board-area";

        [SerializeField] private UIDocument document;
        [SerializeField] private TowerPlacementInput placementInput;
        [SerializeField] private PrototypeBootstrap bootstrap;
        [SerializeField] private Camera viewCamera;

        [Header("Texts")]
        [SerializeField] private string spawnOrExitText;
        [SerializeField] private string cellBlockedText;
        [SerializeField] private string enemyInTheWayText;
        [SerializeField] private string wouldBlockPathText;
        [SerializeField] private string notEnoughGoldText;

        [Header("Motion")]
        [Tooltip("Cells above the cell's centre, towards the top of the screen, where the label's bottom centre starts.")]
        [SerializeField] private float heightCells;
        [Tooltip("Panel pixels the label rises, linearly, over its life.")]
        [SerializeField] private float risePixels;
        [Tooltip("Seconds of unscaled time the label stays on screen.")]
        [SerializeField, Min(0f)] private float lifeSeconds;
        [Tooltip("The last seconds of its life, over which the label fades linearly to 0.")]
        [SerializeField, Min(0f)] private float fadeSeconds;
        [Tooltip("Panel pixels the label's whole rectangle is kept inside the board area.")]
        [SerializeField, Min(0f)] private float marginPixels;

        private Battle _battle;
        private MapDefinition _map;
        private VisualElement _root;
        private VisualElement _boardArea;
        private FloatingLabel _label;
        private bool _active;
        private GridPos _cell;
        private float _shownAt;
        private Vector2 _size;

        private void Start()
        {
            _root = document != null ? document.rootVisualElement : null;
            VisualElement anchor = _root?.Q<VisualElement>(AnchorName);
            Label label = anchor?.Q<Label>(LabelName);
            _boardArea = _root?.Q<VisualElement>(BoardAreaName);
            if (label == null || _boardArea == null || placementInput == null || bootstrap == null || bootstrap.Battle == null || viewCamera == null)
            {
                Debug.LogError($"RefusalView: assign the UI document (with {AnchorName} holding the label {LabelName}, and {BoardAreaName}), the placement input, the bootstrap and the camera; the battle must exist.", this);
                enabled = false;
                return;
            }

            _battle = bootstrap.Battle;
            _map = bootstrap.Map;
            _label = new FloatingLabel(anchor, label);
            placementInput.PlacementRefused += OnPlacementRefused;
        }

        private void OnDestroy()
        {
            if (placementInput != null) placementInput.PlacementRefused -= OnPlacementRefused;
        }

        private void OnPlacementRefused(PlacementResult reason, GridPos cell)
        {
            string text = TextFor(reason);
            _label.SetText(text);
            // Measured once per refusal, so the clamp of every frame needs no layout read.
            _size = _label.Label.MeasureTextSize(text, float.NaN, VisualElement.MeasureMode.Undefined, float.NaN, VisualElement.MeasureMode.Undefined);
            _cell = cell;
            _shownAt = Time.unscaledTime;
            _active = true;
        }

        private void LateUpdate()
        {
            if (!_active) return;

            float age = Time.unscaledTime - _shownAt;
            IPanel panel = _root.panel;
            if (_battle.Outcome != RunOutcome.Running || !(age < lifeSeconds) || panel == null)
            {
                _active = false;
                _label.Hide();
                return;
            }

            var toPanel = new BoardToPanel(viewCamera, panel, _map);
            Vector2 position = toPanel.Point(_cell.X, _cell.Y + heightCells);
            position.y -= risePixels * age / lifeSeconds;

            Rect area = _boardArea.worldBound;
            if (area.width > 0f && area.height > 0f)
            {
                // Max of the Min: in an area narrower or shorter than the label, its left and top edges win.
                float halfWidth = _size.x * 0.5f;
                position.x = Mathf.Max(area.xMin + marginPixels + halfWidth, Mathf.Min(position.x, area.xMax - marginPixels - halfWidth));
                position.y = Mathf.Max(area.yMin + marginPixels + _size.y, Mathf.Min(position.y, area.yMax - marginPixels));
            }

            _label.MoveTo(position);
            _label.SetOpacity(Mathf.Clamp01((lifeSeconds - age) / fadeSeconds));
            _label.Show();
        }

        private string TextFor(PlacementResult reason)
        {
            switch (reason)
            {
                case PlacementResult.SpawnOrExit: return spawnOrExitText;
                case PlacementResult.CellBlocked: return cellBlockedText;
                case PlacementResult.EnemyInTheWay: return enemyInTheWayText;
                case PlacementResult.NotEnoughGold: return notEnoughGoldText;
                default: return wouldBlockPathText;
            }
        }
    }
}
