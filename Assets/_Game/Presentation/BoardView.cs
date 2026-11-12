using ElementalTD.Core;
using ElementalTD.Data;
using ElementalTD.Gameplay;
using UnityEngine;

namespace ElementalTD.Presentation
{
    /// <summary>
    /// Shows the board: the cell tiles, one visual per tower in its element's material, the hover, the frame around the
    /// tower selected on the board and the range disc. The disc stands for the selected tower while there is one; otherwise
    /// for the tower on the hovered cell, or else for the tower to buy.
    /// </summary>
    public sealed class BoardView : MonoBehaviour
    {
        [SerializeField] private PrototypeBootstrap bootstrap;
        [SerializeField] private TowerPlacementInput placementInput;
        [SerializeField] private ElementPalette palette;

        [Header("Cells")]
        [Tooltip("Authored for a cell size of 1; its root is scaled by the map's cell size. Its tile starts with the free material.")]
        [SerializeField] private GameObject cellPrefab;
        [SerializeField] private Material blockedMaterial;
        [SerializeField] private Material spawnMaterial;
        [SerializeField] private Material exitMaterial;

        [Header("Towers and hover")]
        [Tooltip("Authored for a cell size of 1; its root is scaled by the map's cell size.")]
        [SerializeField] private GameObject towerPrefab;
        [Tooltip("Authored for a cell size of 1; its root is scaled by the map's cell size.")]
        [SerializeField] private GameObject hoverPrefab;
        [Tooltip("A disc of radius 1 at a cell size of 1; scaled on x and z by the tower's range. Its material is the one for no element.")]
        [SerializeField] private GameObject rangeDiscPrefab;
        [Tooltip("Authored for a cell size of 1, centred on the cell; its root is scaled by the map's cell size.")]
        [SerializeField] private GameObject selectionFramePrefab;

        private Board _board;
        private MapDefinition _map;
        private GridMap _grid;
        private Transform _towerRoot;
        private GameObject[] _towers;
        private GameObject _hover;
        private Transform _hoverTransform;
        private GameObject _rangeDisc;
        private Transform _rangeDiscTransform;
        private Renderer _rangeDiscRenderer;
        private Material[] _rangeDiscMaterials; // by element id
        private Material _shownDiscMaterial;
        private float _shownDiscRange = -1f; // forces the first write
        private GameObject _selectionFrame;
        private Transform _selectionFrameTransform;
        private Tower _framedTower;
        // Off the grid, so the first shown cell is always written.
        private GridPos _shownHoverCell = new GridPos(-1, -1);
        private GridPos _shownDiscCell = new GridPos(-1, -1);

        private void Start()
        {
            if (bootstrap == null || bootstrap.Board == null || placementInput == null || palette == null || cellPrefab == null
                || blockedMaterial == null || spawnMaterial == null || exitMaterial == null
                || towerPrefab == null || hoverPrefab == null || rangeDiscPrefab == null || selectionFramePrefab == null
                || placementInput.TowerCount == 0)
            {
                Debug.LogError("BoardView: assign every reference, and towers on the placement input; the board must exist.", this);
                enabled = false;
                return;
            }

            _board = bootstrap.Board;
            _map = bootstrap.Map;
            _grid = _board.Map;
            _towers = new GameObject[_grid.CellCount];
            _towerRoot = new GameObject("Towers").transform;

            BuildTiles();

            _hover = Instantiate(hoverPrefab);
            _hover.name = "Hover";
            _hoverTransform = _hover.transform;
            _hoverTransform.localScale = Vector3.one * _map.CellSize;
            _hover.SetActive(false);

            _rangeDisc = Instantiate(rangeDiscPrefab);
            _rangeDisc.name = "RangeDisc";
            _rangeDiscTransform = _rangeDisc.transform;
            _rangeDiscRenderer = _rangeDisc.GetComponentInChildren<Renderer>();
            _shownDiscMaterial = _rangeDiscRenderer.sharedMaterial;
            _rangeDiscMaterials = palette.MaterialsById(look => look.RangeDisc, _shownDiscMaterial);
            _rangeDisc.SetActive(false);

            _selectionFrame = Instantiate(selectionFramePrefab);
            _selectionFrame.name = "SelectionFrame";
            _selectionFrameTransform = _selectionFrame.transform;
            _selectionFrameTransform.localScale = Vector3.one * _map.CellSize;
            _selectionFrame.SetActive(false);

            _board.TowerPlaced += OnTowerPlaced;
            _board.TowerRemoved += OnTowerRemoved;
        }

        private void OnDestroy()
        {
            if (_board == null) return;
            _board.TowerPlaced -= OnTowerPlaced;
            _board.TowerRemoved -= OnTowerRemoved;
        }

        private void LateUpdate()
        {
            Tower selected = placementInput.SelectedTower;
            bool hasHover = placementInput.HasHoverCell;

            if (hasHover) MoveTo(_hoverTransform, placementInput.HoverCell, ref _shownHoverCell);
            SetActive(_hover, hasHover);

            if (selected != null && !ReferenceEquals(selected, _framedTower))
            {
                _framedTower = selected;
                _selectionFrameTransform.position = _map.CellToWorld(selected.Cell);
            }
            SetActive(_selectionFrame, selected != null);

            if (selected != null) ShowDisc(selected.Cell, selected.Stats.Range, selected.Stats.Element);
            else if (hasHover) ShowDiscForHover(placementInput.HoverCell);
            SetActive(_rangeDisc, selected != null || hasHover);
        }

        private void ShowDiscForHover(GridPos cell)
        {
            Tower tower = _board.TowerOn(cell);
            if (tower != null)
            {
                ShowDisc(cell, tower.Stats.Range, tower.Stats.Element);
                return;
            }

            TowerDefinition toBuy = placementInput.Tower; // never null while a cell is hovered
            ShowDisc(cell, toBuy.RangeCells, toBuy.Element != null ? toBuy.Element.ToElement() : default);
        }

        private void ShowDisc(GridPos cell, float range, Element element)
        {
            MoveTo(_rangeDiscTransform, cell, ref _shownDiscCell);

            if (range != _shownDiscRange)
            {
                _shownDiscRange = range;
                float radius = range * _map.CellSize;
                _rangeDiscTransform.localScale = new Vector3(radius, _map.CellSize, radius);
            }

            Material material = ElementPalette.ById(_rangeDiscMaterials, element.Id);
            if (!ReferenceEquals(material, _shownDiscMaterial))
            {
                _shownDiscMaterial = material;
                _rangeDiscRenderer.sharedMaterial = material;
            }
        }

        private void MoveTo(Transform target, GridPos cell, ref GridPos shownCell)
        {
            if (cell == shownCell) return;
            shownCell = cell;
            target.position = _map.CellToWorld(cell);
        }

        private static void SetActive(GameObject target, bool active)
        {
            if (target.activeSelf != active) target.SetActive(active);
        }

        private void BuildTiles()
        {
            var root = new GameObject("Grid").transform;
            var cellScale = Vector3.one * _map.CellSize;
            GridPos spawn = _board.Spawn;
            GridPos exit = _board.Exit;

            for (int y = 0; y < _grid.Height; y++)
            {
                for (int x = 0; x < _grid.Width; x++)
                {
                    var cell = new GridPos(x, y);
                    GameObject tile = Instantiate(cellPrefab, _map.CellToWorld(cell), Quaternion.identity, root);
                    tile.name = $"Cell {x},{y}";
                    tile.transform.localScale = cellScale;

                    var renderer = tile.GetComponentInChildren<Renderer>();
                    if (cell == spawn) renderer.sharedMaterial = spawnMaterial;
                    else if (cell == exit) renderer.sharedMaterial = exitMaterial;
                    else if (_grid.IsBlocked(cell)) renderer.sharedMaterial = blockedMaterial;
                }
            }
        }

        private void OnTowerPlaced(GridPos cell)
        {
            GameObject tower = Instantiate(towerPrefab, _map.CellToWorld(cell), Quaternion.identity, _towerRoot);
            tower.name = $"Tower {cell.X},{cell.Y}";
            tower.transform.localScale = Vector3.one * _map.CellSize;
            if (palette.TryGetLook(_board.TowerOn(cell).Stats.Element, out ElementPalette.Look look) && look.Tower != null)
                tower.GetComponentInChildren<Renderer>().sharedMaterial = look.Tower;
            _towers[_grid.Index(cell)] = tower;
        }

        private void OnTowerRemoved(GridPos cell)
        {
            int index = _grid.Index(cell);
            Destroy(_towers[index]);
            _towers[index] = null;
        }
    }
}
