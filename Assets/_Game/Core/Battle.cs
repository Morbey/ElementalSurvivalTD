using System;
using System.Collections.Generic;

namespace ElementalTD.Core
{
    /// <summary>
    /// Advances the board in fixed ticks: re-entry, enemies, towers, projectiles, in that order.
    /// The prototype's one enemy loops: after leaving the board (killed or at the exit) it re-enters at the spawn.
    /// </summary>
    public sealed class Battle
    {
        public const float TickSeconds = 1f / 60f;

        private readonly Enemy _enemy;
        private readonly float _reentryDelaySeconds;
        private float _reentryTimer;
        private readonly List<Projectile> _projectiles = new List<Projectile>();
        private readonly Stack<Projectile> _freeProjectiles = new Stack<Projectile>();

        public Board Board { get; }
        public int ProjectileCount => _projectiles.Count;
        public Projectile ProjectileAt(int index) => _projectiles[index];

        public Battle(Board board, in EnemyStats enemy, float reentryDelaySeconds)
        {
            Board = board ?? throw new ArgumentNullException(nameof(board));
            if (reentryDelaySeconds < 0f)
                throw new ArgumentOutOfRangeException(nameof(reentryDelaySeconds), reentryDelaySeconds, "The re-entry delay cannot be negative.");

            _reentryDelaySeconds = reentryDelaySeconds;
            _enemy = new Enemy(board.Field, board.Spawn, enemy);
            board.AddEnemy(_enemy);
        }

        public void Tick()
        {
            StepReentry();
            StepEnemies();
            StepTowers();
            StepProjectiles();
        }

        private void StepReentry()
        {
            if (_enemy.IsOnBoard) return;
            _reentryTimer -= TickSeconds;
            if (_reentryTimer <= 0f) Board.AddEnemy(_enemy);
        }

        private void StepEnemies()
        {
            int i = 0;
            while (i < Board.EnemyCount)
            {
                Enemy enemy = Board.EnemyAt(i);
                if (enemy.Step(TickSeconds)) Leave(enemy); // removed from the list, so i now holds the next one
                else i++;
            }
        }

        private void StepTowers()
        {
            for (int i = 0; i < Board.TowerCount; i++)
            {
                Tower tower = Board.TowerAt(i);
                tower.Cooldown -= TickSeconds;
                if (tower.Cooldown > 0f) continue;

                Enemy target = FindTarget(tower);
                if (target == null)
                {
                    tower.Cooldown = 0f;
                    continue;
                }

                Projectile projectile = _freeProjectiles.Count > 0 ? _freeProjectiles.Pop() : new Projectile();
                projectile.Launch(tower.Cell, target, tower.Stats);
                _projectiles.Add(projectile);
                tower.Cooldown += 1f / tower.Stats.ShotsPerSecond;
            }
        }

        private void StepProjectiles()
        {
            int i = 0;
            while (i < _projectiles.Count)
            {
                Projectile projectile = _projectiles[i];
                if (projectile.TargetGone)
                {
                    Release(i);
                    continue;
                }

                if (!projectile.Advance(TickSeconds))
                {
                    i++;
                    continue;
                }

                Enemy target = projectile.Target;
                target.TakeDamage(projectile.Damage);
                Release(i);
                if (target.Health <= 0f) Leave(target);
            }
        }

        /// <summary>The enemy in range nearest the exit; on a tie, the first to enter.</summary>
        private Enemy FindTarget(Tower tower)
        {
            float range = tower.Stats.Range;
            float rangeSquared = range * range;
            Enemy best = null;
            float bestDistance = float.MaxValue;

            for (int i = 0; i < Board.EnemyCount; i++)
            {
                Enemy enemy = Board.EnemyAt(i);
                float dx = enemy.X - tower.Cell.X;
                float dy = enemy.Y - tower.Cell.Y;
                if (dx * dx + dy * dy > rangeSquared) continue;

                float distance = enemy.DistanceToExit;
                if (distance < bestDistance)
                {
                    best = enemy;
                    bestDistance = distance;
                }
            }
            return best;
        }

        private void Leave(Enemy enemy)
        {
            Board.RemoveEnemy(enemy);
            if (enemy == _enemy) _reentryTimer = _reentryDelaySeconds;
        }

        private void Release(int index)
        {
            Projectile projectile = _projectiles[index];
            _projectiles.RemoveAt(index);
            projectile.Release();
            _freeProjectiles.Push(projectile);
        }
    }
}
