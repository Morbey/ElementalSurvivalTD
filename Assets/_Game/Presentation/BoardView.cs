using ElementalTD.Core;
using ElementalTD.Data;
using ElementalTD.Gameplay;
using UnityEngine;

namespace ElementalTD.Presentation
{
    /// <summary>Shows the board: the cell tiles, one visual per tower and the hover highlight.</summary>
    public sealed class BoardView : MonoBehaviour
    {
        [SerializeField] private PrototypeBootstrap bootstrap;
        [SerializeField] private TowerPlacementInput placementInput;

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

        private MapDefinition _map;
        private GridMap _grid;
        private Transform _towerRoot;
        private GameObject[] _towers;
        private GameObject _hover;
        private Transform _hoverTransform;

        private void Start()
        {
            if (bootstrap == null || bootstrap.Board == null || placementInput == null || cellPrefab == null
                || blockedMaterial == null || spawnMaterial == null || exitMaterial == null
                || towerPrefab == null || hoverPrefab == null)
            {
                Debug.LogError("BoardView: assign every reference; the board must exist.", this);
                enabled = false;
                return;
            }

            _map = bootstrap.Map;
            _grid = bootstrap.Board.Map;
            _towers = new GameObject[_grid.CellCount];
            _towerRoot = new GameObject("Towers").transform;

            BuildTiles();

            _hover = Instantiate(hoverPrefab);
            _hover.name = "Hover";
            _hoverTransform = _hover.transform;
            _hoverTransform.localScale = Vector3.one * _map.CellSize;
            _hover.SetActive(false);

            placementInput.TowerPlaced += OnTowerPlaced;
            placementInput.TowerRemoved += OnTowerRemoved;
        }

        private void OnDestroy()
        {
            if (placementInput == null) return;
            placementInput.TowerPlaced -= OnTowerPlaced;
            placementInput.TowerRemoved -= OnTowerRemoved;
        }

        private void LateUpdate()
        {
            bool show = placementInput.HasHoverCell;
            if (show) _hoverTransform.position = _map.CellToWorld(placementInput.HoverCell);
            if (_hover.activeSelf != show) _hover.SetActive(show);
        }

        private void BuildTiles()
        {
            var root = new GameObject("Grid").transform;
            var cellScale = Vector3.one * _map.CellSize;
            GridPos spawn = bootstrap.Board.Spawn;
            GridPos exit = bootstrap.Board.Exit;

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
