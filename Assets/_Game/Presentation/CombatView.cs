using System.Collections.Generic;
using ElementalTD.Core;
using ElementalTD.Data;
using ElementalTD.Gameplay;
using UnityEngine;

namespace ElementalTD.Presentation
{
    /// <summary>
    /// Draws the battle every frame, between its last two ticks: the enemy view on enemy 0, and the n-th pooled
    /// projectile view on the n-th projectile. One enemy view is the prototype's limit; slice 3 pools them per enemy.
    /// </summary>
    public sealed class CombatView : MonoBehaviour
    {
        [SerializeField] private PrototypeBootstrap bootstrap;
        [Tooltip("Authored for a cell size of 1; its root is scaled by the map's cell size.")]
        [SerializeField] private EnemyView enemyPrefab;
        [Tooltip("Authored for a cell size of 1; its root is scaled by the map's cell size.")]
        [SerializeField] private GameObject projectilePrefab;
        [Tooltip("Projectile views created at start-up; the pool grows only when more are in flight.")]
        [SerializeField, Min(0)] private int prewarmProjectiles;

        private Battle _battle;
        private MapDefinition _map;
        private Vector3 _viewScale;
        private EnemyView _enemyView;
        private GameObject _enemyObject;
        private bool _enemyShown;
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

            _enemyView = Instantiate(enemyPrefab);
            _enemyView.name = "Enemy";
            _enemyView.transform.localScale = _viewScale;
            _enemyObject = _enemyView.gameObject;
            _enemyObject.SetActive(false);

            _projectileRoot = new GameObject("Projectiles").transform;
            for (int i = 0; i < prewarmProjectiles; i++) AddProjectileView();
        }

        private void LateUpdate()
        {
            float alpha = bootstrap.TickAlpha;
            Board board = _battle.Board;

            bool showEnemy = board.EnemyCount > 0;
            if (showEnemy)
            {
                Enemy enemy = board.EnemyAt(0);
                _enemyView.Show(Interpolate(enemy.PreviousX, enemy.PreviousY, enemy.X, enemy.Y, alpha),
                    enemy.Health / enemy.MaxHealth);
            }
            if (showEnemy != _enemyShown)
            {
                _enemyObject.SetActive(showEnemy);
                _enemyShown = showEnemy;
            }

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
