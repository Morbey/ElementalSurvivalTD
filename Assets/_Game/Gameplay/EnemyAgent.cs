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
        private PathFollower _follower;
        private MapDefinition _map;
        private Transform _transform;
        private float _speed;

        public void Init(PathFollower follower, float speedCellsPerSecond, MapDefinition map)
        {
            _follower = follower;
            _speed = speedCellsPerSecond;
            _map = map;
            _transform = transform;
            SyncPosition();
        }

        private void Update()
        {
            if (_follower == null) return;

            bool arrived = _follower.Step(Time.deltaTime, _speed);
            SyncPosition();
            if (arrived)
            {
                _follower = null;
                Destroy(gameObject);
            }
        }

        private void SyncPosition()
        {
            Vector3 from = _map.CellToWorld(_follower.From);
            Vector3 to = _map.CellToWorld(_follower.To);
            _transform.position = Vector3.Lerp(from, to, _follower.Progress);
        }
    }
}
