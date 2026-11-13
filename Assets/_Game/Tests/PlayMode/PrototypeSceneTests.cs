using System.Collections;
using System.Collections.Generic;
using ElementalTD.Core;
using ElementalTD.Presentation;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.UIElements;
using Is = UnityEngine.TestTools.Constraints.Is;
using UnityEngine.TestTools.Constraints;
using UnityEngine.TestTools;

namespace ElementalTD.PlayModeTests
{
    public class PrototypeSceneTests : SceneTestBase
    {
        private const string SpawnOrExitText = "Cannot build on the spawn or the exit.";
        private const string WallText = "Cannot build on a wall.";
        private const string EnemyText = "An enemy is in the way.";
        private const string PathText = "That would block the path.";

        [Test]
        public void AC1_Scene_Camera_Is_Orthographic_And_Straight_Down()
        {
            // Where it looks and how big the board is on screen are the 4b-AC16 test.
            Assert.IsTrue(_camera.orthographic);
            Assert.Greater(Vector3.Dot(_camera.transform.forward, Vector3.down), 0.999f);
        }

        private static string MaterialPath(Material m)
        {
#if UNITY_EDITOR
            return UnityEditor.AssetDatabase.GetAssetPath(m);
#else
            return string.Empty;
#endif
        }

        private static Material CellMaterial(int x, int y)
        {
            GameObject cell = GameObject.Find($"Cell {x},{y}");
            Assert.IsNotNull(cell, $"Cell {x},{y} not found");
            return cell.GetComponentInChildren<Renderer>().sharedMaterial;
        }

        [UnityTest]
        public IEnumerator S2a_AC4_Every_Renderer_Uses_A_Presentation_Material_With_A_URP_Shader()
        {
            // With a tower placed and the hover shown, so the tower and hover materials are checked too.
            ClickCell(3, 2);
            _input.Hover(ScreenOf(new GridPos(3, 2)));
            yield return null;
            Assert.IsNotNull(GameObject.Find("Tower 3,2"));

            Renderer[] renderers = Object.FindObjectsByType<Renderer>(FindObjectsInactive.Include);
            Assert.Greater(renderers.Length, 144);
            foreach (Renderer r in renderers)
            {
                Assert.IsNotNull(r.sharedMaterial, r.name);
                StringAssert.StartsWith("Assets/_Game/Presentation/Materials/", MaterialPath(r.sharedMaterial), r.name);
                StringAssert.StartsWith("Universal Render Pipeline/", r.sharedMaterial.shader.name, r.name);
            }
            Assert.AreEqual(0, Object.FindObjectsByType<Collider>(FindObjectsInactive.Include).Length);
        }

        [Test]
        public void S2a_AC4_Spawn_Exit_Wall_Free_Cells_And_Enemy_Use_Five_Different_Materials()
        {
            var materials = new HashSet<Material>
            {
                CellMaterial(_map.Spawn.X, _map.Spawn.Y),
                CellMaterial(_map.Exit.X, _map.Exit.Y),
                CellMaterial(5, 0),
                CellMaterial(1, 0),
                Agents()[0].GetComponentInChildren<Renderer>().sharedMaterial
            };
            Assert.AreEqual(5, materials.Count);
        }

        [Test]
        public void S2a_AC4_Scene_Has_No_Colliders()
        {
            Assert.AreEqual(0, Object.FindObjectsByType<Collider>(FindObjectsInactive.Include).Length);
        }

        [UnityTest]
        public IEnumerator S2b_AC1_AC5_Enemy_Routes_Around_A_Tower_Placed_On_The_S_Route()
        {
            ClickCell(5, 7); // the gap of wall A: the route must go through (5,8)
            Assert.IsTrue(Board.HasTower(new GridPos(5, 7)));
            Assert.AreEqual(9442, Board.Field.Distance(_map.Spawn), "route recalculated in the same frame");

            // The tower now kills a wave-1 enemy (45 health) before it gets far, so the walker is a test enemy that cannot die.
            Enemy enemy = Board.SpawnEnemy(new EnemyStats(2f, 1_000_000f));
            yield return null;
            var visited = new HashSet<GridPos>();
            for (int tick = 0; tick < 1500 && !visited.Contains(new GridPos(9, 1)) && enemy.IsOnBoard; tick++)
            {
                Battle.Tick();
                GridPos cell = enemy.Follower.From;
                Assert.IsFalse(Board.Map.IsBlocked(cell), $"enemy inside blocked cell {cell}");
                visited.Add(cell);
                if (tick % 20 == 0) yield return null;
            }

            Assert.IsTrue(visited.Contains(new GridPos(9, 1)), "enemy never got past the tower");
            Assert.IsFalse(visited.Contains(new GridPos(5, 7)));
            Assert.IsTrue(visited.Contains(new GridPos(5, 8)), "enemy did not detour");
        }

        [UnityTest]
        public IEnumerator S2b_AC1_AC2_Click_Places_Tower_On_Cell_Centre_And_Blocks_It_Second_Click_Selects_It_And_Sell_Removes_It()
        {
            var cell = new GridPos(2, 2);
            int gold = Battle.Gold;
            int cost = _input.Tower.ToStats().Cost;
            ClickCell(2, 2);
            Assert.AreEqual(gold - cost, Battle.Gold, "the purchase is paid");
            GameObject tower = GameObject.Find("Tower 2,2");
            Assert.IsNotNull(tower);
            Assert.Less(Vector3.Distance(_map.CellToWorld(cell), tower.transform.position), 0.01f);
            Assert.IsTrue(_bootstrap.Board.HasTower(cell));
            Assert.IsTrue(_bootstrap.Board.Map.IsBlocked(cell));

            ClickCell(2, 2);
            Assert.AreEqual(gold - cost, Battle.Gold, "4c-AC2: the second click selects: it neither sells nor buys");
            Assert.IsTrue(_bootstrap.Board.HasTower(cell));
            Assert.AreSame(Board.TowerOn(cell), _input.SelectedTower);
            Assert.IsTrue(_input.SellSelected());
            yield return null;
            Assert.IsNull(GameObject.Find("Tower 2,2"));
            Assert.AreEqual(gold, Battle.Gold, "3b-AC5: a tower that never fired refunds everything");
            Assert.IsFalse(_bootstrap.Board.HasTower(cell));
            Assert.IsFalse(_bootstrap.Board.Map.IsBlocked(cell));

            // A fixed wall cannot be removed by clicking.
            ClickCell(5, 0);
            Assert.IsTrue(_bootstrap.Board.Map.IsBlocked(new GridPos(5, 0)));
        }

        [UnityTest]
        public IEnumerator S2b_AC4_Message_Text_Per_Refusal(
            [ValueSource(nameof(RefusalCases))] string reason)
        {
            yield return null;
            Assert.IsFalse(RefusalShown(), "the refusal must start hidden");

            string expected;
            switch (reason)
            {
                case "spawnOrExit": ClickCell(_map.Spawn.X, _map.Spawn.Y); expected = SpawnOrExitText; break;
                case "wall": ClickCell(5, 0); expected = WallText; break;
                case "enemy":
                    yield return TickUntilFirstEnemy();
                    GridPos heading = Board.EnemyAt(0).Follower.To; // the cell it is walking to
                    ClickCell(heading.X, heading.Y);
                    expected = EnemyText;
                    break;
                default:
                    ClickCell(5, 7);
                    yield return null;
                    Assert.IsFalse(RefusalShown(), "a placement shows no refusal");
                    ClickCell(5, 8);
                    expected = PathText;
                    break;
            }
            yield return null;
            Assert.IsTrue(RefusalShown());
            Assert.AreEqual(expected, RefusalText);
            Assert.IsFalse(MessageShown(), "4d-AC10: nothing on the message line");
            Assert.IsEmpty(_label.text);
        }

        private static readonly string[] RefusalCases = { "spawnOrExit", "wall", "enemy", "path" };

        [UnityTest]
        public IEnumerator S2b_AC4_And_S4d_AC10_Refusal_Hides_After_Its_Life_Unscaled_And_A_New_Refusal_Replaces_It_And_Restarts_The_Timer()
        {
            // The refusal's life is the direction's 1.5 s (1.3), not the 2 s of 2b-AC4.
            ClickCell(_map.Spawn.X, _map.Spawn.Y);
            yield return null;
            Assert.IsTrue(RefusalShown());
            Assert.AreEqual(SpawnOrExitText, RefusalText);

            yield return new WaitForSecondsRealtime(1.0f);
            ClickCell(5, 0);
            yield return null;
            Assert.AreEqual(WallText, RefusalText, "a new refusal replaces the text");

            yield return new WaitForSecondsRealtime(0.8f); // 1.8 s after the first, 0.8 s after the second
            Assert.IsTrue(RefusalShown(), "timer must restart on the new refusal");

            yield return new WaitForSecondsRealtime(1.0f); // 1.8 s after the second
            Assert.IsFalse(RefusalShown(), "the refusal must hide after 1.5 s");
        }

        [UnityTest]
        public IEnumerator S2b_AC4_Click_Outside_The_Grid_Does_Nothing()
        {
            int distanceBefore = _bootstrap.Board.Field.Distance(_map.Spawn);
            _input.Click(OffGrid);
            yield return null;
            Assert.IsFalse(MessageShown());
            Assert.IsFalse(RefusalShown());
            Assert.AreEqual(distanceBefore, _bootstrap.Board.Field.Distance(_map.Spawn));
            Assert.AreEqual(0, FindByName("Towers").childCount);
        }

        [UnityTest]
        public IEnumerator S2b_AC8_Camera_Reframes_For_Aspect_1_And_2_5_And_Grid_Stays_In_View(
            [Values(1f, 2.5f)] float aspect)
        {
            _camera.aspect = aspect;
            yield return null;
            yield return null;

            float halfHeight = _camera.orthographicSize;
            float halfWidth = halfHeight * _camera.aspect;
            Vector3 c = _camera.transform.position;
            float width = _map.Width * _map.CellSize, depth = _map.Height * _map.CellSize;
            Assert.LessOrEqual(c.x - halfWidth, 0f);
            Assert.GreaterOrEqual(c.x + halfWidth, width);
            Assert.LessOrEqual(c.z - halfHeight, 0f);
            Assert.GreaterOrEqual(c.z + halfHeight, depth);
        }

        [UnityTest]
        public IEnumerator S2b_AC10_Hover_Allocates_Nothing_After_Warmup()
        {
            Vector2 onGrid = ScreenOf(new GridPos(2, 2));
            _input.Hover(onGrid);
            _input.Hover(OffGrid);
            yield return null;
            Assert.That(() => { _input.Hover(onGrid); }, Is.Not.AllocatingGCMemory());
            Assert.That(() => { _input.Hover(OffGrid); }, Is.Not.AllocatingGCMemory());
        }

        [UnityTest]
        public IEnumerator S2c_AC5_A_Projectile_View_Shows_Between_Tower_And_Enemy_And_The_Bar_Shrinks_To_Health_Over_Max()
        {
            var towerCell = new GridPos(1, 3);
            ClickCell(1, 3);
            yield return TickUntilFirstEnemy();
            Enemy enemy = Board.EnemyAt(0);
            Assert.AreEqual(1f, FillOf(EnemyViewAt(0)).localScale.x, 1e-4f, "full on entry");

            yield return TickUntil(() => Battle.ProjectileCount > 0, 100);

            Transform shown = null;
            foreach (Transform child in FindByName("Projectiles"))
                if (child.gameObject.activeSelf) shown = child;
            Assert.IsNotNull(shown, "an active projectile view");
            Vector3 tower = _map.CellToWorld(towerCell);
            Vector3 target = EnemyViewAt(0).position;
            float detour = Vector3.Distance(tower, shown.position) + Vector3.Distance(shown.position, target) - Vector3.Distance(tower, target);
            Assert.Less(detour, 0.3f * _map.CellSize, "on the way from the tower to the enemy");

            yield return TickUntil(() => enemy.Health < enemy.MaxHealth, 300);
            Assert.AreEqual(enemy.MaxHealth - 10f * 2f, enemy.Health, "one hit of 10 from the Light tower on a Shadow enemy: x2");
            Assert.AreEqual(enemy.Health / enemy.MaxHealth, FillOf(EnemyViewAt(0)).localScale.x, 1e-4f);
        }

        private IEnumerator TickUntilOnStep(GridPos from, GridPos to)
        {
            yield return TickUntil(() => Board.EnemyCount > 0 && Board.EnemyAt(0).Follower.From == from && Board.EnemyAt(0).Follower.To == to, 1500, 50);
        }

        private static bool OnDiagonal(Enemy enemy) =>
            enemy.Follower.From.X != enemy.Follower.To.X && enemy.Follower.From.Y != enemy.Follower.To.Y;

        [UnityTest]
        public IEnumerator S2c_AC11_On_A_Straight_Stretch_The_Enemy_View_Moves_On_Every_Frame_By_Speed_Times_Frame_Time()
        {
            // The stretch from (9,4) to (9,1) lasts 1.5 s at speed 2.
            yield return TickUntilOnStep(new GridPos(9, 4), new GridPos(9, 3));
            Enemy enemy = Board.EnemyAt(0);
            for (int i = 0; i < 5; i++) Battle.Tick(); // two tick positions to interpolate between
            Transform view = EnemyViewAt(0);
            Time.timeScale = 1f;
            for (int i = 0; i < 3; i++) yield return null; // let the new time scale take effect
            Vector3 last = view.position;
            float lastDt = Time.deltaTime;
            float sumExpected = 0f, sumMoved = 0f;
            for (int frame = 0; frame < 40; frame++)
            {
                yield return null;
                Vector3 now = view.position;
                // The view shown now was drawn by the previous frame's LateUpdate, from that frame's delta time.
                float expected = enemy.Speed * lastDt * _map.CellSize;
                float moved = Vector3.Distance(last, now);
                sumExpected += expected;
                sumMoved += moved;
                Assert.Greater(moved, 0f, $"frame {frame}: the enemy did not move");
                Assert.AreEqual(expected, moved, 0.15f * expected + 0.002f, $"frame {frame}");
                last = now;
                lastDt = Time.deltaTime;
            }
            Assert.AreEqual(sumExpected, sumMoved, 0.02f * sumExpected + 0.0001f, "over 40 frames");
            Assert.AreEqual(9, enemy.Follower.To.X, "still on the straight stretch");
            Assert.AreEqual(9, enemy.Follower.From.X);
            Assert.GreaterOrEqual(enemy.Follower.To.Y, 1);
        }

        [UnityTest]
        public IEnumerator S3c_AC6_On_A_Diagonal_Stretch_The_Enemy_View_Moves_On_Every_Frame_By_Speed_Times_Frame_Time_Equally_On_X_And_Z()
        {
            // From the tick the first enemy is on (1,4) heading for (2,5): the diagonal stretch (1,4) to (4,7) lasts 2.1 s.
            yield return TickUntilOnStep(new GridPos(1, 4), new GridPos(2, 5));
            Enemy enemy = Board.EnemyAt(0);
            for (int i = 0; i < 5; i++) Battle.Tick();
            Transform view = EnemyViewAt(0);
            Time.timeScale = 1f;
            for (int i = 0; i < 3; i++) yield return null;
            Vector3 last = view.position;
            float lastDt = Time.deltaTime;
            float sumExpected = 0f, sumMoved = 0f, sumX = 0f, sumZ = 0f;
            for (int frame = 0; frame < 40; frame++)
            {
                yield return null;
                Vector3 now = view.position;
                float expected = enemy.Speed * lastDt * _map.CellSize;
                float moved = Vector3.Distance(last, now);
                sumExpected += expected;
                sumMoved += moved;
                sumX += now.x - last.x;
                sumZ += now.z - last.z;
                float tolerance = 0.15f * expected + 0.002f;
                Assert.Greater(moved, 0f, $"frame {frame}: the enemy did not move");
                Assert.AreEqual(expected, moved, tolerance, $"frame {frame}: speed along the diagonal");
                Assert.AreEqual(now.x - last.x, now.z - last.z, tolerance, $"frame {frame}: equal movement on x and z");
                Assert.Greater(now.x - last.x, 0f, $"frame {frame}: moving up and to the right");
                last = now;
                lastDt = Time.deltaTime;
            }
            Assert.AreEqual(sumExpected, sumMoved, 0.02f * sumExpected + 0.0001f, "over 40 frames");
            Assert.AreEqual(sumX, sumZ, 0.02f * sumX + 0.0001f, "equal on x and z over 40 frames");
            Assert.IsTrue(OnDiagonal(enemy), "still on the diagonal stretch");
            Assert.LessOrEqual(enemy.Follower.To.X, 4);
        }

        [UnityTest]
        public IEnumerator S3c_AC6_A_Projectile_View_And_The_Health_Bar_Stay_With_An_Enemy_On_A_Diagonal_Step()
        {
            var towerCell = new GridPos(2, 3);
            ClickCell(2, 3);
            Assert.IsTrue(Board.HasTower(towerCell));
            yield return TickUntilOnStep(new GridPos(1, 4), new GridPos(2, 5));
            Enemy enemy = Board.EnemyAt(0);
            yield return TickUntil(() => Battle.ProjectileCount > 0, 100, 1);
            Assert.IsTrue(OnDiagonal(enemy), "the shot is aimed at an enemy on a diagonal step");

            Transform shown = null;
            foreach (Transform child in FindByName("Projectiles"))
                if (child.gameObject.activeSelf) shown = child;
            Assert.IsNotNull(shown, "an active projectile view");
            Transform view = EnemyViewAt(0);
            Vector3 tower = _map.CellToWorld(towerCell);
            Vector3 target = view.position;
            float detour = Vector3.Distance(tower, shown.position) + Vector3.Distance(shown.position, target) - Vector3.Distance(tower, target);
            Assert.Less(detour, 0.3f * _map.CellSize, "on the way from the tower to the enemy");

            float before = enemy.Health;
            yield return TickUntil(() => enemy.Health < before, 100, 1);
            Assert.AreEqual(enemy.Health / enemy.MaxHealth, FillOf(EnemyViewAt(0)).localScale.x, 1e-4f);
            Vector3 bar = FillOf(EnemyViewAt(0)).position;
            Vector3 body = EnemyViewAt(0).position;
            Assert.Less(Vector3.Distance(new Vector3(bar.x, 0f, bar.z), new Vector3(body.x, 0f, body.z)), 1f * _map.CellSize, "the bar stays with the enemy");
        }

        [UnityTest]
        public IEnumerator S3c_AC5_A_Click_On_A_Side_Cell_Of_An_Enemys_Diagonal_Step_Shows_The_Message_And_Places_No_Tower()
        {
            yield return TickUntilOnStep(new GridPos(1, 4), new GridPos(2, 5));
            int gold = Battle.Gold;
            Assert.IsFalse(RefusalShown());

            foreach (var side in new[] { new GridPos(2, 4), new GridPos(1, 5) })
            {
                ClickCell(side.X, side.Y);
                yield return null;
                Assert.IsTrue(RefusalShown(), $"{side}");
                Assert.AreEqual(EnemyText, RefusalText, $"{side}");
                Assert.IsFalse(Board.HasTower(side), $"{side}");
                Assert.IsNull(GameObject.Find($"Tower {side.X},{side.Y}"));
                Assert.AreEqual(0, Board.TowerCount);
                Assert.AreEqual(gold, Battle.Gold);
            }
            Assert.AreEqual(0, FindByName("Towers").childCount);
        }
    }
}
