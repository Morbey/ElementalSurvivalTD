using ElementalTD.Core;
using ElementalTD.Data;
using UnityEngine;

namespace ElementalTD.Gameplay
{
    /// <summary>
    /// Drives a Core PathFollower and turns its grid state into a world position. Holds no rules.
    /// </summary>
    public sealed class EnemyAgent : MonoBehaviour
    {
        private Board _board;
        private PathFollower _follower;
        private MapDefinition _map;
        private Transform _transform;
        private float _speed;

        public void Init(Board board, float speedCellsPerSecond, MapDefinition map)
        {
            _board = board;
            _follower = board.AddFollower();
            _speed = speedCellsPerSecond;
            _map = map;
            _transform = transform;
            SyncPosition();
        }

        private void Update()
        {
            if (_follower == null) return;

            // Prototype stand-in for waves (goes away in slice 3): the one enemy loops forever.
            if (_follower.Step(Time.deltaTime, _speed)) _follower.Restart(_board.Spawn);
            SyncPosition();
        }

        private void SyncPosition()
        {
            Vector3 from = _map.CellToWorld(_follower.From);
            Vector3 to = _map.CellToWorld(_follower.To);
            _transform.position = Vector3.Lerp(from, to, _follower.Progress);
        }
    }
}
