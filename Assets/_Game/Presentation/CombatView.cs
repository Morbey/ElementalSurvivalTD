using System.Collections.Generic;
using ElementalTD.Core;
using ElementalTD.Data;
using ElementalTD.Gameplay;
using UnityEngine;

namespace ElementalTD.Presentation
{
    /// <summary>
    /// Draws the battle every frame, between its last two ticks: the n-th pooled enemy view on the n-th enemy on the
    /// board, and the n-th pooled projectile view on the n-th projectile. Views stay anonymous (ADR 0004).
    /// </summary>
    public sealed class CombatView : MonoBehaviour
    {
        [SerializeField] private PrototypeBootstrap bootstrap;
        [Tooltip("Authored for a cell size of 1; its root is scaled by the map's cell size.")]
        [SerializeField] private EnemyView enemyPrefab;
        [Tooltip("Enemy views created at start-up; the pool grows only when more enemies are on the board.")]
        [SerializeField, Min(0)] private int prewarmEnemies;
        [Tooltip("Authored for a cell size of 1; its root is scaled by the map's cell size.")]
        [SerializeField] private GameObject projectilePrefab;
        [Tooltip("Projectile views created at start-up; the pool grows only when more are in flight.")]
        [SerializeField, Min(0)] private int prewarmProjectiles;

        private Battle _battle;
        private MapDefinition _map;
        private Vector3 _viewScale;
        private Transform _enemyRoot;
        private readonly List<EnemyView> _enemyViews = new List<EnemyView>();
        private readonly List<GameObject> _enemyObjects = new List<GameObject>();
        private int _shownEnemies;
        private Transform _projectileRoot;
        private readonly List<GameObject> _projectileObjects = new List<GameObject>();
        private readonly List<Transform> _projectileTransforms = new List<Transform>();
        private int _shownProjectiles;

        private void Start()
        {
            if (bootstrap == null || bootstrap.Battle == null || enemyPrefab == null || projectilePrefab == null)
            {
                Debug.LogError("CombatView: assign the bootstrap and both prefabs; the battle must exist.", this);
                enabled = false;
                return;
            }

            _battle = bootstrap.Battle;
            _map = bootstrap.Map;
            _viewScale = Vector3.one * _map.CellSize;

            _enemyRoot = new GameObject("Enemies").transform;
            for (int i = 0; i < prewarmEnemies; i++) AddEnemyView();

            _projectileRoot = new GameObject("Projectiles").transform;
            for (int i = 0; i < prewarmProjectiles; i++) AddProjectileView();
        }

        private void LateUpdate()
        {
            float alpha = bootstrap.TickAlpha;
            Board board = _battle.Board;

            int enemies = board.EnemyCount;
            while (_enemyViews.Count < enemies) AddEnemyView();
            for (int i = 0; i < enemies; i++)
            {
                Enemy enemy = board.EnemyAt(i);
                _enemyViews[i].Show(Interpolate(enemy.PreviousX, enemy.PreviousY, enemy.X, enemy.Y, alpha),
                    enemy.Health / enemy.MaxHealth);
                if (i >= _shownEnemies) _enemyObjects[i].SetActive(true);
            }
            for (int i = enemies; i < _shownEnemies; i++) _enemyObjects[i].SetActive(false);
            _shownEnemies = enemies;

            int count = _battle.ProjectileCount;
            while (_projectileObjects.Count < count) AddProjectileView();
            for (int i = 0; i < count; i++)
            {
                Projectile projectile = _battle.ProjectileAt(i);
                _projectileTransforms[i].position =
                    Interpolate(projectile.PreviousX, projectile.PreviousY, projectile.X, projectile.Y, alpha);
                if (i >= _shownProjectiles) _projectileObjects[i].SetActive(true);
            }
            for (int i = count; i < _shownProjectiles; i++) _projectileObjects[i].SetActive(false);
            _shownProjectiles = count;
        }

        private Vector3 Interpolate(float previousX, float previousY, float x, float y, float alpha) =>
            _map.CellToWorld(Mathf.Lerp(previousX, x, alpha), Mathf.Lerp(previousY, y, alpha));

        private void AddEnemyView()
        {
            EnemyView view = Instantiate(enemyPrefab, _enemyRoot);
            view.name = "Enemy";
            view.transform.localScale = _viewScale;
            GameObject viewObject = view.gameObject;
            viewObject.SetActive(false);
            _enemyViews.Add(view);
            _enemyObjects.Add(viewObject);
        }

        private void AddProjectileView()
        {
            GameObject view = Instantiate(projectilePrefab, _projectileRoot);
            view.name = "Projectile";
            view.transform.localScale = _viewScale;
            view.SetActive(false);
            _projectileObjects.Add(view);
            _projectileTransforms.Add(view.transform);
        }
    }
}
