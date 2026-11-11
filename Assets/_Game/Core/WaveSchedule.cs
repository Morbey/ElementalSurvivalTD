using System;

namespace ElementalTD.Core
{
    /// <summary>
    /// Says when the next enemy of a run enters: the waves in order, each after its delay, its enemies one per spawn interval.
    /// One timer counts both; at most one enemy enters per step.
    /// </summary>
    public sealed class WaveSchedule
    {
        private readonly Wave[] _waves;
        private int _wave;
        private int _enteredInWave;
        private bool _inDelay;
        private float _timer;

        public int WaveCount => _waves.Length;
        /// <summary>Waves whose first enemy has entered.</summary>
        public int StartedWaves { get; private set; }
        /// <summary>Above 0 only while a wave's delay runs.</summary>
        public float SecondsToNextWave => _inDelay && _timer > 0f ? _timer : 0f;
        /// <summary>The last enemy of the last wave has entered.</summary>
        public bool IsFinished { get; private set; }

        public WaveSchedule(Wave[] waves)
        {
            if (waves == null) throw new ArgumentNullException(nameof(waves));
            if (waves.Length == 0) throw new ArgumentException("A run needs at least one wave.", nameof(waves));
            for (int i = 0; i < waves.Length; i++)
                if (waves[i].Count < 1) throw new ArgumentException($"Wave {i + 1} has no enemy.", nameof(waves));

            // A copy, so changing the caller's array cannot change a running schedule.
            _waves = (Wave[])waves.Clone();
            _inDelay = true;
            _timer = _waves[0].DelayBeforeSeconds;
        }

        /// <summary>Advances the timer by dt; returns true, with the enemy's stats, when an enemy enters now.</summary>
        public bool Step(float dt, out EnemyStats enemy)
        {
            enemy = default;
            if (IsFinished) return false;

            _timer -= dt;
            if (_timer > 0f) return false;

            // The remainder keeps the cadence exact over a run; capping it at one tick keeps an interval
            // shorter than a tick from eating into the next wave's delay.
            if (_timer < -dt) _timer = -dt;

            Wave wave = _waves[_wave];
            enemy = wave.Enemy;
            if (_enteredInWave == 0) StartedWaves++;
            _enteredInWave++;
            _inDelay = false;

            if (_enteredInWave < wave.Count)
            {
                _timer += wave.SpawnIntervalSeconds;
                return true;
            }

            _wave++;
            _enteredInWave = 0;
            if (_wave < _waves.Length)
            {
                _inDelay = true;
                _timer += _waves[_wave].DelayBeforeSeconds;
            }
            else
            {
                IsFinished = true;
            }
            return true;
        }
    }
}
