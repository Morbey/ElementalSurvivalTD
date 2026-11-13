using System;
using System.Collections.Generic;

namespace ElementalTD.Core
{
    /// <summary>
    /// Plays one run in fixed ticks: waves, enemies, towers, projectiles, wave bonuses, then the outcome, in that order.
    /// Enemies enter by the wave schedule, cost a life at the exit and pay their gold and then their XP reward when killed;
    /// a shot that lands deals its damage times the chart's multiplier, 0 against the own element, and is recorded in
    /// the hit log, or as a mark on an enemy it cannot hurt (ADR 0006);
    /// a wave pays its survival bonus once its last enemy has entered and none of its enemies is on the board.
    /// Towers are bought and sold here: the battle checks the gold, the board keeps the placement rule.
    /// </summary>
    public sealed class Battle
    {
        public const float TickSeconds = 1f / 60f;
        public const int HitLogCapacity = 64;

        private readonly List<Projectile> _projectiles = new List<Projectile>();
        private readonly Hit[] _hits = new Hit[HitLogCapacity];
        private readonly Stack<Projectile> _freeProjectiles = new Stack<Projectile>();
        private readonly int _sellRefundPercent;
        private readonly bool[] _paidWaves;
        private readonly ElementChart _elements;

        public Board Board { get; }
        public WaveSchedule Waves { get; }
        /// <summary>The run's XP and level; never null (ADR 0004).</summary>
        public Progression Progression { get; }
        public int Lives { get; private set; }
        /// <summary>Whole gold, never below 0.</summary>
        public int Gold { get; private set; }
        public RunOutcome Outcome { get; private set; }
        /// <summary>Waves that have paid their survival bonus.</summary>
        public int SurvivedWaves { get; private set; }
        /// <summary>The number, from 1, of the wave that paid last; 0 before the first.</summary>
        public int LastSurvivedWave { get; private set; }
        public int ProjectileCount => _projectiles.Count;
        public Projectile ProjectileAt(int index) => _projectiles[index];
        /// <summary>Ticks run: 0 before the first; a finished battle counts no more.</summary>
        public int TickCount { get; private set; }
        /// <summary>Hits recorded since the run began; only the last HitLogCapacity are kept (ADR 0006).</summary>
        public int HitCount { get; private set; }

        /// <summary>The record of hit number index, counted from 0; answers only for the last HitLogCapacity.</summary>
        public Hit HitAt(int index)
        {
            if (index < 0 || index >= HitCount || index < HitCount - HitLogCapacity)
                throw new ArgumentOutOfRangeException(nameof(index), index, "The log holds only the last hits.");
            return _hits[index % HitLogCapacity];
        }

        /// <summary>
        /// Without a chart every pair of elements deals x1; immunity to the own element still holds.
        /// Without a progression the battle makes one with no table: XP counts and the level stays 1.
        /// </summary>
        public Battle(Board board, Wave[] waves, int lives, int startingGold = 0, int sellRefundPercent = 100, ElementChart elements = null,
            Progression progression = null)
        {
            Board = board ?? throw new ArgumentNullException(nameof(board));
            if (lives < 1) throw new ArgumentOutOfRangeException(nameof(lives), lives, "A run needs at least one life.");
            if (startingGold < 0) throw new ArgumentOutOfRangeException(nameof(startingGold), startingGold, "Starting gold cannot be negative.");
            if (sellRefundPercent < 0 || sellRefundPercent > 100)
                throw new ArgumentOutOfRangeException(nameof(sellRefundPercent), sellRefundPercent, "The sell refund percent must be 0 to 100.");

            Waves = new WaveSchedule(waves);
            _paidWaves = new bool[Waves.WaveCount];
            Lives = lives;
            Gold = startingGold;
            _sellRefundPercent = sellRefundPercent;
            _elements = elements ?? new ElementChart(0);
            Progression = progression ?? new Progression(null);
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
        /// What TrySellTower would pay for the tower on the cell now: the full cost while the tower has never launched a shot,
        /// then the refund percent of it, rounded down. False, with 0, when the cell has no tower. Changes nothing.
        /// </summary>
        public bool TryGetSellValue(GridPos cell, out int gold)
        {
            Tower tower = Board.TowerOn(cell);
            if (tower == null)
            {
                gold = 0;
                return false;
            }

            int cost = tower.Stats.Cost;
            // Integer arithmetic, so the refund is the same under Mono and IL2CPP.
            gold = tower.HasFired ? cost * _sellRefundPercent / 100 : cost;
            return true;
        }

        /// <summary>Removes the tower and pays what TryGetSellValue answers. Returns false when the cell has no tower.</summary>
        public bool TrySellTower(GridPos cell)
        {
            if (!TryGetSellValue(cell, out int gold)) return false;

            Board.TryRemoveTower(cell);
            Gold += gold;
            return true;
        }

        /// <summary>Does nothing once the run is won or lost.</summary>
        public void Tick()
        {
            if (Outcome != RunOutcome.Running) return;

            // First, so everything stamped during this tick carries the count it has once the tick has run.
            TickCount++;
            StepWaves();
            StepEnemies();
            StepTowers();
            StepProjectiles();
            StepWaveBonuses();
            JudgeOutcome();
        }

        private void StepWaves()
        {
            if (Waves.Step(TickSeconds, out EnemyStats enemy)) Board.SpawnEnemy(enemy, Waves.StartedWaves - 1);
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

                // The only place an element counts: read on landing, so a state on the target can change it later (ADR 0005).
                Enemy target = projectile.Target;
                float multiplier = _elements.Multiplier(projectile.Element, target.Element);
                float damage = projectile.Damage * multiplier;
                // Recorded before the target can be removed, so a killing hit is logged too.
                HitKind kind = ElementChart.KindOf(multiplier);
                if (kind == HitKind.Immune) target.MarkImmuneHit(TickCount);
                else
                {
                    _hits[HitCount % HitLogCapacity] = new Hit(target.X, target.Y, damage, kind, TickCount, target.Entry);
                    HitCount++;
                }
                target.TakeDamage(damage);
                Release(i);
                if (target.Health <= 0f)
                {
                    // Removal makes later shots at it TargetGone, so a kill pays once.
                    Gold += target.GoldReward;
                    Progression.AddXp(target.XpReward);
                    Board.RemoveEnemy(target);
                }
            }
        }

        /// <summary>
        /// Judged one by one, so waves may be survived out of order or on the same tick, each once.
        /// The tick that takes the last life pays nothing; the last wave pays on the tick that wins.
        /// </summary>
        private void StepWaveBonuses()
        {
            if (Lives == 0) return;

            int entered = Waves.EnteredWaves;
            for (int w = 0; w < entered; w++)
            {
                if (_paidWaves[w] || HasEnemyOfWave(w)) continue;

                _paidWaves[w] = true;
                Gold += Waves.WaveAt(w).SurvivalBonus;
                SurvivedWaves++;
                LastSurvivedWave = w + 1;
            }
        }

        private bool HasEnemyOfWave(int waveIndex)
        {
            for (int i = 0; i < Board.EnemyCount; i++)
                if (Board.EnemyAt(i).WaveIndex == waveIndex) return true;
            return false;
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
