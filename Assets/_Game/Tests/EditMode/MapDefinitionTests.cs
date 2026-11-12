using ElementalTD.Data;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace ElementalTD.Core.Tests
{
    public class MapDefinitionTests
    {
        private const string MapPath = "Assets/_Game/Data/Assets/Map_Prototype.asset";
        private const string EnemyPath = "Assets/_Game/Data/Assets/Enemy_Shadow45.asset";

        private static MapDefinition Make(string json)
        {
            var map = ScriptableObject.CreateInstance<MapDefinition>();
            JsonUtility.FromJsonOverwrite(json, map);
            return map;
        }

        [Test]
        public void AC5_MapDefinition_Fields_Drive_GridMap_Spawn_Exit_And_World_Position()
        {
            MapDefinition def = Make("{\"width\":7,\"height\":4,\"cellSize\":2,\"spawn\":{\"x\":1,\"y\":2},\"exit\":{\"x\":6,\"y\":3},\"blockedCells\":[{\"x\":3,\"y\":0},{\"x\":3,\"y\":1}]}");
            try
            {
                GridMap grid = def.ToGridMap();
                Assert.AreEqual(7, grid.Width);
                Assert.AreEqual(4, grid.Height);
                Assert.IsTrue(grid.IsBlocked(new GridPos(3, 0)));
                Assert.IsTrue(grid.IsBlocked(new GridPos(3, 1)));
                Assert.IsFalse(grid.IsBlocked(new GridPos(3, 2)));
                Assert.AreEqual(new GridPos(1, 2), def.Spawn);
                Assert.AreEqual(new GridPos(6, 3), def.Exit);
                Assert.AreEqual(new Vector3(3f, 0f, 5f), def.CellToWorld(new GridPos(1, 2)));
            }
            finally { Object.DestroyImmediate(def); }
        }

        [Test]
        public void AC5_Changing_A_Blocked_Cell_Changes_The_Route_Without_Code_Change()
        {
            string open = "{\"width\":5,\"height\":3,\"cellSize\":1,\"spawn\":{\"x\":0,\"y\":1},\"exit\":{\"x\":4,\"y\":1},\"blockedCells\":[]}";
            string walled = "{\"width\":5,\"height\":3,\"cellSize\":1,\"spawn\":{\"x\":0,\"y\":1},\"exit\":{\"x\":4,\"y\":1},\"blockedCells\":[{\"x\":2,\"y\":0},{\"x\":2,\"y\":1}]}";
            MapDefinition a = Make(open), b = Make(walled);
            try
            {
                var fa = new FlowField(a.ToGridMap()); fa.Compute(a.Exit);
                var fb = new FlowField(b.ToGridMap()); fb.Compute(b.Exit);
                // Open: 4 straight steps. Walled: (1,2) diagonal, (2,2), (3,2), then the diagonal to (4,1) = 2 * 577 + 2 * 408.
                Assert.AreEqual(1632, fa.Distance(a.Spawn));
                Assert.AreEqual(1970, fb.Distance(b.Spawn));
            }
            finally { Object.DestroyImmediate(a); Object.DestroyImmediate(b); }
        }

        [Test]
        public void AC5_Blocked_Cell_Out_Of_Bounds_Throws_On_ToGridMap()
        {
            MapDefinition def = Make("{\"width\":2,\"height\":2,\"blockedCells\":[{\"x\":5,\"y\":5}]}");
            try { Assert.Throws<System.ArgumentException>(() => def.ToGridMap()); }
            finally { Object.DestroyImmediate(def); }
        }

        [Test]
        public void AC5_Map_Prototype_Asset_Holds_The_Expected_Values()
        {
            var def = AssetDatabase.LoadAssetAtPath<MapDefinition>(MapPath);
            Assert.IsNotNull(def);
            Assert.AreEqual(16, def.Width);
            Assert.AreEqual(9, def.Height);
            Assert.AreEqual(1f, def.CellSize);
            Assert.AreEqual(new GridPos(0, 4), def.Spawn);
            Assert.AreEqual(new GridPos(15, 4), def.Exit);
            GridMap grid = def.ToGridMap();
            int blocked = 0;
            for (int y = 0; y < grid.Height; y++)
                for (int x = 0; x < grid.Width; x++)
                {
                    bool expected = (x == 5 && y <= 6) || (x == 10 && y >= 2);
                    Assert.AreEqual(expected, grid.IsBlocked(new GridPos(x, y)), $"cell {x},{y}");
                    if (grid.IsBlocked(new GridPos(x, y))) blocked++;
                }
            Assert.AreEqual(14, blocked);
        }

        [Test]
        public void AC5_Enemy_Speed_Comes_From_The_Asset()
        {
            var asset = AssetDatabase.LoadAssetAtPath<EnemyDefinition>(EnemyPath);
            Assert.IsNotNull(asset);
            Assert.AreEqual(2f, asset.SpeedCellsPerSecond);

            var def = ScriptableObject.CreateInstance<EnemyDefinition>();
            try
            {
                JsonUtility.FromJsonOverwrite("{\"speedCellsPerSecond\":3.5}", def);
                Assert.AreEqual(3.5f, def.SpeedCellsPerSecond);
            }
            finally { Object.DestroyImmediate(def); }
        }

        [Test]
        public void S2b_AC1_WorldToCell_Round_Trips_Every_Cell_And_Maps_Points_Just_Inside_Each_Edge()
        {
            MapDefinition def = Make("{\"width\":5,\"height\":4,\"cellSize\":2}");
            try
            {
                for (int y = 0; y < 4; y++)
                    for (int x = 0; x < 5; x++)
                    {
                        var cell = new GridPos(x, y);
                        Vector3 c = def.CellToWorld(cell);
                        Assert.AreEqual(cell, def.WorldToCell(c));
                        const float e = 0.999f; // just inside the half cell (1.0)
                        Assert.AreEqual(cell, def.WorldToCell(c + new Vector3(e, 0, e)));
                        Assert.AreEqual(cell, def.WorldToCell(c - new Vector3(e, 0, e)));
                    }
                Assert.AreEqual(new GridPos(-1, -1), def.WorldToCell(new Vector3(-0.1f, 0, -0.1f)));
                Assert.AreEqual(new GridPos(5, 4), def.WorldToCell(new Vector3(10.1f, 0, 8.1f)));
            }
            finally { Object.DestroyImmediate(def); }
        }

        [Test]
        public void S2c_AC8_ToStats_Returns_The_Definition_Values()
        {
            var tower = ScriptableObject.CreateInstance<TowerDefinition>();
            var enemy = ScriptableObject.CreateInstance<EnemyDefinition>();
            try
            {
                JsonUtility.FromJsonOverwrite("{\"rangeCells\":2.5,\"damage\":10,\"shotsPerSecond\":1.5,\"projectileSpeedCellsPerSecond\":10}", tower);
                JsonUtility.FromJsonOverwrite("{\"speedCellsPerSecond\":2,\"maxHealth\":100}", enemy);
                TowerStats t = tower.ToStats();
                EnemyStats e = enemy.ToStats();
                Assert.AreEqual(new[] { 2.5f, 10f, 1.5f, 10f }, new[] { t.Range, t.Damage, t.ShotsPerSecond, t.ProjectileSpeed });
                Assert.AreEqual(new[] { 2f, 100f }, new[] { e.Speed, e.MaxHealth });
            }
            finally
            {
                Object.DestroyImmediate(tower);
                Object.DestroyImmediate(enemy);
            }
        }

        [Test]
        public void S2c_AC8_CellToWorld_Float_Overload_Agrees_With_The_GridPos_Overload()
        {
            MapDefinition def = Make("{\"width\":7,\"height\":4,\"cellSize\":2,\"spawn\":{\"x\":1,\"y\":2},\"exit\":{\"x\":6,\"y\":3},\"blockedCells\":[]}");
            try
            {
                Assert.AreEqual(def.CellToWorld(new GridPos(3, 2)), def.CellToWorld(3f, 2f));
                Assert.AreEqual(new Vector3(7f, 0f, 5f), def.CellToWorld(3f, 2f));
                Assert.AreEqual(new Vector3(8f, 0f, 4f), def.CellToWorld(3.5f, 1.5f), "between cell centres");
            }
            finally { Object.DestroyImmediate(def); }
        }
    }
}
