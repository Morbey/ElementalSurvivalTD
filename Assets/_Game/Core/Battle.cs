using System;
using System.Collections.Generic;

namespace ElementalTD.Core
{
    /// <summary>
    /// Plays one run in fixed ticks: waves, enemies, towers, projectiles, then the outcome, in that order.
    /// Enemies enter by the wave schedule, cost a life at the exit and pay their gold reward when killed.
    /// Towers are bought and sold here: the battle checks the gold, the board keeps the placement rule.
    /// </summary>
    public sealed class Battle
    {
        public const float TickSeconds = 1f / 60f;

        private readonly List<Projectile> _projectiles = new List<Projectile>();
        private readonly Stack<Projectile> _freeProjectiles = new Stack<Projectile>();
        private readonly int _sellRefundPercent;

        public Board Board { get; }
        public WaveSchedule Waves { get; }
        public int Lives { get; private set; }
        /// <summary>Whole gold, never below 0.</summary>
        public int Gold { get; private set; }
        public RunOutcome Outcome { get; private set; }
        public int ProjectileCount => _projectiles.Count;
        public Projectile ProjectileAt(int index) => _projectiles[index];

        public Battle(Board board, Wave[] waves, int lives, int startingGold = 0, int sellRefundPercent = 100)
        {
            Board = board ?? throw new ArgumentNullException(nameof(board));
            if (lives < 1) throw new ArgumentOutOfRangeException(nameof(lives), lives, "A run needs at least one life.");
            if (startingGold < 0) throw new ArgumentOutOfRangeException(nameof(startingGold), startingGold, "Starting gold cannot be negative.");
            if (sellRefundPercent < 0 || sellRefundPercent > 100)
                throw new ArgumentOutOfRangeException(nameof(sellRefundPercent), sellRefundPercent, "The sell refund percent must be 0 to 100.");

            Waves = new WaveSchedule(waves);
            Lives = lives;
            Gold = startingGold;
            _sellRefundPercent = sellRefundPercent;
            Outcome = RunOutcome.Running;
        }

        /// <summary>The gold is checked before the board rule; only a placed tower costs gold.</summary>
        public PlacementResult TryBuyTower(GridPos cell, in TowerStats stats)
        {
            if (Gold < stats.Cost) return PlacementResult.NotEnoughGold;

            PlacementResult result = Board.TryPlaceTower(cell, stats);
            if (result == PlacementResult.Placed) Gold -= stats.Cost;
            return result;
        }

        /// <summary>
        /// Refunds the full cost while the tower has never launched a shot, then the refund percent of it, rounded down.
        /// Returns false when the cell has no tower.
        /// </summary>
        public bool TrySellTower(GridPos cell)
        {
            Tower tower = Board.TowerOn(cell);
            if (tower == null) return false;

            Board.TryRemoveTower(cell);
            int cost = tower.Stats.Cost;
            // Integer arithmetic, so the refund is the same under Mono and IL2CPP.
            Gold += tower.HasFired ? cost * _sellRefundPercent / 100 : cost;
            return true;
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
                tower.HasFired = true;
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
                if (target.Health <= 0f)
                {
                    // Removal makes later shots at it TargetGone, so a kill pays once.
                    Gold += target.GoldReward;
                    Board.RemoveEnemy(target);
                }
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
