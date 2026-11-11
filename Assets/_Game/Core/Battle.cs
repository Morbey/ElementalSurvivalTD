using System;
using System.Collections.Generic;

namespace ElementalTD.Core
{
    /// <summary>
    /// Plays one run in fixed ticks: waves, enemies, towers, projectiles, then the outcome, in that order.
    /// Enemies enter by the wave schedule, cost a life at the exit and leave for good when killed.
    /// </summary>
    public sealed class Battle
    {
        public const float TickSeconds = 1f / 60f;

        private readonly List<Projectile> _projectiles = new List<Projectile>();
        private readonly Stack<Projectile> _freeProjectiles = new Stack<Projectile>();

        public Board Board { get; }
        public WaveSchedule Waves { get; }
        public int Lives { get; private set; }
        public RunOutcome Outcome { get; private set; }
        public int ProjectileCount => _projectiles.Count;
        public Projectile ProjectileAt(int index) => _projectiles[index];

        public Battle(Board board, Wave[] waves, int lives)
        {
            Board = board ?? throw new ArgumentNullException(nameof(board));
            if (lives < 1) throw new ArgumentOutOfRangeException(nameof(lives), lives, "A run needs at least one life.");

            Waves = new WaveSchedule(waves);
            Lives = lives;
            Outcome = RunOutcome.Running;
        }

        /// <summary>Does nothing once the run is won or lost.</summary>
        public void Tick()
        {
            if (Outcome != RunOutcome.Running) return;

            StepWaves();
            StepEnemies();
            StepTowers();
            StepProjectiles();
            JudgeOutcome();
        }

        private void StepWaves()
        {
            if (Waves.Step(TickSeconds, out EnemyStats enemy)) Board.SpawnEnemy(enemy);
        }

        private void StepEnemies()
        {
            int i = 0;
            while (i < Board.EnemyCount)
            {
                Enemy enemy = Board.EnemyAt(i);
                if (enemy.Step(TickSeconds))
                {
                    Board.RemoveEnemy(enemy); // removed from the list, so i now holds the next one
                    if (Lives > 0) Lives--;
                }
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
                if (target.Health <= 0f) Board.RemoveEnemy(target);
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

        /// <summary>The loss is judged first, so the last enemy of the run reaching the exit with the last life loses.</summary>
        private void JudgeOutcome()
        {
            if (Lives == 0)
            {
                Outcome = RunOutcome.Lost;
                return;
            }

            if (!Waves.IsFinished || Board.EnemyCount > 0) return;

            Outcome = RunOutcome.Won;
            while (_projectiles.Count > 0) Release(_projectiles.Count - 1);
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
