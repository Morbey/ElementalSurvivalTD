using System;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using System.Text;
using ElementalTD.Core;
using ElementalTD.Data;
using ElementalTD.Presentation;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.UIElements;
using Object = UnityEngine.Object;

namespace ElementalTD.PlayModeTests
{
    /// <summary>
    /// Helpers for the text over the board (plan 0009): reading what the views drew, and a model of the stack written from the
    /// plan's formulas (4d-AC6 to AC8, AC15, AC16) that the tests compare the drawn frame with. The scene runs at time scale 0 with
    /// ticks driven by the test, so the tick fraction is 0 unless a test sets it.
    /// </summary>
    public abstract class FloatingSceneBase : SceneTestBase
    {
        protected const float Dt = 1f / 60f;
        protected const float PositionTolerance = 0.05f;
        /// <summary>The plan's tolerance for two numbers overlapping: more than this on both axes.</summary>
        protected const float OverlapTolerance = 0.5f;
        protected const int OffBoard = 1_000_000;

        private DamageNumberView _numbers;
        private Transform _enemies;

        protected DamageNumberView NumberView
        {
            get
            {
                if (_numbers == null) _numbers = Object.FindAnyObjectByType<DamageNumberView>();
                Assert.IsNotNull(_numbers);
                return _numbers;
            }
        }

        protected VisualElement NumberLayer => Root.Q<VisualElement>("damage-numbers");
        protected VisualElement WordLayer => Root.Q<VisualElement>("immune-words");

        protected float SceneValue(string field) =>
            Convert.ToSingle(typeof(DamageNumberView).GetField(field, BindingFlags.NonPublic | BindingFlags.Instance).GetValue(NumberView));

        protected float HeightOf(HitKind kind) => SceneValue(kind == HitKind.Weak ? "weakHeight" : kind == HitKind.Normal ? "normalHeight" : "strongHeight");
        protected float LifeOf(HitKind kind) => SceneValue(kind == HitKind.Weak ? "weakLifeSeconds" : kind == HitKind.Normal ? "normalLifeSeconds" : "strongLifeSeconds");

        protected float PushSeconds => SceneValue("pushSeconds");

        protected Vector2 PanelOf(Vector3 world) => RuntimePanelUtils.CameraTransformWorldToPanel(Root.panel, world, _camera);

        /// <summary>The panel point of a board position in cells, raised by the given cells towards the top of the screen.</summary>
        protected Vector2 PanelOfCell(float x, float y, float raiseCells = 0f) => PanelOf(_map.CellToWorld(x, y + raiseCells));

        /// <summary>Where the n-th enemy is drawn: the n-th pooled enemy view.</summary>
        protected Vector3 DrawnWorld(Enemy enemy)
        {
            if (_enemies == null) _enemies = FindByName("Enemies");
            for (int i = 0; i < Board.EnemyCount; i++)
                if (ReferenceEquals(Board.EnemyAt(i), enemy)) return _enemies.GetChild(i).position;
            Assert.Fail("the enemy is not on the board");
            return default;
        }

        protected bool OnBoard(Enemy enemy)
        {
            for (int i = 0; i < Board.EnemyCount; i++)
                if (ReferenceEquals(Board.EnemyAt(i), enemy)) return true;
            return false;
        }

        // ---- What is drawn

        protected struct Drawn
        {
            public int Index;
            public Label Label;
            public Vector2 Bottom;   // the anchor point: the label's bottom centre, or its top centre for a word
            public float Scale, Opacity;
            public Rect Rect;        // worldBound of the label, as scaled
            public string Text;
        }

        protected List<Drawn> ReadLayer(VisualElement layer)
        {
            var list = new List<Drawn>();
            for (int i = 0; i < layer.childCount; i++)
            {
                VisualElement anchor = layer[i];
                if (anchor.resolvedStyle.visibility != Visibility.Visible) continue;
                var label = (Label)anchor[0];
                Vector3 translate = anchor.resolvedStyle.translate;
                list.Add(new Drawn
                {
                    Index = i,
                    Label = label,
                    Bottom = new Vector2(translate.x, translate.y),
                    Scale = anchor.resolvedStyle.scale.value.x,
                    Opacity = anchor.resolvedStyle.opacity,
                    Rect = label.worldBound,
                    Text = label.text,
                });
            }
            return list;
        }

        protected List<Drawn> DrawnNumbers() => ReadLayer(NumberLayer);
        protected List<Drawn> DrawnWords() => ReadLayer(WordLayer);

        protected static bool Overlaps(Rect a, Rect b, float tolerance = OverlapTolerance) =>
            Mathf.Min(a.xMax, b.xMax) - Mathf.Max(a.xMin, b.xMin) > tolerance
            && Mathf.Min(a.yMax, b.yMax) - Mathf.Max(a.yMin, b.yMin) > tolerance;

        /// <summary>A board y (in cells) whose cell centre lies at the panel y, at the column x: the direction's rows on a board that this window letterboxes.</summary>
        protected float CellYAtPanelY(float x, float panelY)
        {
            Vector2 p0 = PanelOfCell(x, 0f), p1 = PanelOfCell(x, 1f);
            return (panelY - p0.y) / (p1.y - p0.y);
        }

        protected float CellPanelHeight => Mathf.Abs(PanelOfCell(0f, 1f).y - PanelOfCell(0f, 0f).y);

        /// <summary>Why the last MatchDrawn did not match.</summary>
        protected string LastMismatch;

        protected Hit HitOn(Enemy enemy, float amount, HitKind kind) => new Hit(enemy.X, enemy.Y, amount, kind, Battle.TickCount, enemy.Entry);

        protected Hit HitAtCell(float x, float y, float amount, HitKind kind, int entry) => new Hit(x, y, amount, kind, Battle.TickCount, entry);

        /// <summary>An enemy of wave 1 (stats of the first group) on the board, entering now.</summary>
        protected Enemy SpawnWave1Enemy(float? health = null, float? speed = null)
        {
            EnemyStats s = Battle.Waves.WaveAt(0).GroupAt(0).Enemy;
            return Board.SpawnEnemy(new EnemyStats(speed ?? s.Speed, health ?? s.MaxHealth, 0, s.Element));
        }

        /// <summary>Stats of the buyable tower of an element, at no cost, so a test can build more than the gold allows.</summary>
        protected TowerStats FreeTowerOf(Element element)
        {
            for (int i = 0; i < _input.TowerCount; i++)
            {
                TowerStats s = _input.TowerAt(i).ToStats();
                if (s.Element == element) return new TowerStats(s.Range, s.Damage, s.ShotsPerSecond, s.ProjectileSpeed, 0, s.Element);
            }
            Assert.Fail($"no buyable tower of {element}");
            return default;
        }

        protected Element OtherElement(Element element)
        {
            for (int i = 0; i < _input.TowerCount; i++)
                if (_input.TowerAt(i).ToStats().Element != element) return _input.TowerAt(i).ToStats().Element;
            Assert.Fail("no other element");
            return default;
        }

        protected void BuyFree(int x, int y, Element element) =>
            Assert.AreEqual(PlacementResult.Placed, Battle.TryBuyTower(new GridPos(x, y), FreeTowerOf(element)), $"tower at ({x},{y})");

        protected void SetTickFraction(float fraction) =>
            typeof(ElementalTD.Gameplay.PrototypeBootstrap).GetField("_accumulator", BindingFlags.NonPublic | BindingFlags.Instance)
                .SetValue(_bootstrap, fraction * Battle.TickSeconds);

        protected void AssertPoolIntact()
        {
            Assert.AreEqual((int)SceneValue("maxNumbers"), NumberLayer.childCount, "the number of labels under damage-numbers never changes");
            for (int i = 0; i < NumberLayer.childCount; i++) Assert.AreEqual(1, NumberLayer[i].childCount);
        }

        // ---- The model

        /// <summary>The scene's numbers, read from the view.</summary>
        protected ReferenceStack NewStack() => new ReferenceStack(this);

        protected sealed class Expected
        {
            public int Entry, Order, Slot;
            public HitKind Kind;
            public string Text;
            public float Height;
            public Vector2 Bottom;
            public float Scale, Opacity, EasedSlot, Lift;
            public bool OnLine, Leaving;
            public override string ToString() =>
                $"#{Order} entry {Entry} {Kind} \"{Text}\" slot {Slot} lift {Lift:F2} at ({Bottom.x:F2},{Bottom.y:F2}) scale {Scale:F3} opacity {Opacity:F3}{(OnLine ? " on the line" : "")}{(Leaving ? " leaving" : "")}";
        }

        protected sealed class ReferenceStack
        {
            private sealed class Num
            {
                public Hit Rec;
                public int Order;
                public float Height, Life;
                public Vector3 World;
                public readonly List<Link> Newer = new List<Link>();
                public int LeaveTick = -1;
            }

            private struct Link
            {
                public int Tick;
                public float Height;
                public Num Who;
                public bool Recycled;
            }

            private readonly FloatingSceneBase _t;
            private readonly List<Num> _alive = new List<Num>();
            private readonly float _p, _topOpacity, _share, _limit, _step, _heightCells;
            private readonly int _budget, _cap, _cutoff;
            private int _read, _next;

            /// <summary>The tick fraction of the moment; the tests set it to what they set on the bootstrap.</summary>
            public float Alpha;

            public int Alive => _alive.Count;

            public ReferenceStack(FloatingSceneBase t)
            {
                _t = t;
                _p = t.SceneValue("pushSeconds");
                _topOpacity = t.SceneValue("topSlotOpacity");
                _share = t.SceneValue("opaqueShare");
                _limit = t.SceneValue("topLimitPixels");
                _step = t.SceneValue("lineStepPixels");
                _heightCells = t.SceneValue("heightCells");
                _budget = (int)t.SceneValue("budgetSlots");
                _cap = (int)t.SceneValue("maxNumbers");
                _cutoff = (int)t.SceneValue("weakCutoff");
                _read = t.Battle.HitCount;
            }

            private float Age(int tick) => (_t.Battle.TickCount - tick + Alpha) * Dt;

            /// <summary>What the view's Show does for a record: the test calls the view's Show with the same record.</summary>
            public void Show(Hit hit)
            {
                float life = _t.LifeOf(hit.Kind);
                if (!(Age(hit.Tick) < life)) return;
                if (hit.Kind == HitKind.Weak && _alive.Count >= _cutoff) return;

                float height = _t.HeightOf(hit.Kind);
                var number = new Num { Rec = hit, Order = _next++, Height = height, Life = life, World = _t._map.CellToWorld(hit.X, hit.Y) };
                foreach (Num older in _alive)
                {
                    if (older.Rec.TargetEntry != hit.TargetEntry) continue;
                    older.World = number.World;
                    older.Newer.Add(new Link { Tick = hit.Tick, Height = height, Who = number });
                    if (older.Newer.Count >= _budget && older.LeaveTick < 0) older.LeaveTick = hit.Tick;
                }
                if (_alive.Count >= _cap)
                {
                    Num oldest = _alive[0];
                    foreach (Num n in _alive) if (n.Order < oldest.Order) oldest = n;
                    _alive.Remove(oldest);
                    foreach (Num n in _alive)
                        for (int i = 0; i < n.Newer.Count; i++)
                            if (ReferenceEquals(n.Newer[i].Who, oldest)) n.Newer[i] = new Link { Tick = n.Newer[i].Tick, Height = n.Newer[i].Height, Who = oldest, Recycled = true };
                }
                _alive.Add(number);
            }

            private void Expire() =>
                _alive.RemoveAll(n => !(Age(n.Rec.Tick) < n.Life) || (n.LeaveTick >= 0 && Age(n.LeaveTick) >= _p));

            /// <summary>What a frame draws now: reads the log like the view, then gives every number's expected state.</summary>
            public List<Expected> Frame(Func<int, Vector3?> enemyDrawnWorld)
            {
                Expire();
                Battle battle = _t.Battle;
                for (int i = Mathf.Max(_read, battle.HitCount - Battle.HitLogCapacity); i < battle.HitCount; i++) Show(battle.HitAt(i));
                _read = battle.HitCount;
                Expire();
                return Now(enemyDrawnWorld);
            }

            /// <summary>The expected state of every number alive, with no log read and no expiry (for tests that call Show themselves).</summary>
            public List<Expected> Now(Func<int, Vector3?> enemyDrawnWorld)
            {
                Expire();
                var result = new List<Expected>();
                foreach (Num n in _alive)
                {
                    Vector3? drawn = enemyDrawnWorld(n.Rec.TargetEntry);
                    if (drawn.HasValue) n.World = drawn.Value;

                    float lift = 0f, slot = n.Newer.Count;
                    foreach (Link link in n.Newer)
                    {
                        float done = link.Recycled ? 1f : Ease(Mathf.Min(1f, Age(link.Tick) / _p));
                        lift += link.Height * done;
                        slot -= 1f - done;
                    }

                    Vector2 bottom = _t.PanelOf(n.World + new Vector3(0f, 0f, _heightCells * _t._map.CellSize));
                    bottom.y -= lift;
                    bool onLine = false;
                    if (bottom.y - n.Height < _limit)
                    {
                        bottom.y = _limit + n.Height;
                        bottom.x += _step * slot;
                        onLine = true;
                    }

                    float age = Age(n.Rec.Tick), t = age / n.Life;
                    float opacity = t <= _share ? 1f : (1f - t) / (1f - _share);
                    if (n.Newer.Count >= _budget - 1) opacity = Mathf.Min(opacity, _topOpacity);
                    if (n.LeaveTick >= 0) opacity *= Mathf.Max(0f, 1f - Age(n.LeaveTick) / _p);
                    float grown = Mathf.Min(1f, age / _p);

                    result.Add(new Expected
                    {
                        Entry = n.Rec.TargetEntry, Order = n.Order, Slot = n.Newer.Count, Kind = n.Rec.Kind, Height = n.Height,
                        Text = Mathf.Max(1, Mathf.FloorToInt(n.Rec.Amount + 0.5f)).ToString(System.Globalization.CultureInfo.InvariantCulture),
                        Bottom = bottom, Scale = grown * grown, Opacity = opacity, EasedSlot = slot, Lift = lift, OnLine = onLine,
                        Leaving = n.LeaveTick >= 0,
                    });
                }
                return result;
            }

            private static float Ease(float t) => 1f - (1f - t) * (1f - t);
        }

        protected Func<int, Vector3?> BoardDrawn() => entry =>
        {
            for (int i = 0; i < Board.EnemyCount; i++)
                if (Board.EnemyAt(i).Entry == entry) return DrawnWorld(Board.EnemyAt(i));
            return null;
        };

        /// <summary>Pairs each expected number with the drawn one at its place; fails with both lists when they differ.</summary>
        protected List<(Drawn drawn, Expected expected)> MatchDrawn(List<Expected> expected, List<Drawn> drawn, string when, bool failOnMismatch = true)
        {
            var pairs = new List<(Drawn, Expected)>();
            var free = new List<Drawn>(drawn);
            var unmatched = new List<Expected>();
            foreach (Expected e in expected)
            {
                int found = -1;
                for (int i = 0; i < free.Count; i++)
                {
                    Drawn d = free[i];
                    if (d.Text == e.Text && Vector2.Distance(d.Bottom, e.Bottom) <= PositionTolerance
                        && Mathf.Abs(d.Scale - e.Scale) <= 0.002f && Mathf.Abs(d.Opacity - e.Opacity) <= 0.002f) { found = i; break; }
                }
                if (found < 0) { unmatched.Add(e); continue; }
                pairs.Add((free[found], e));
                free.RemoveAt(found);
            }

            if (unmatched.Count > 0 || free.Count > 0)
            {
                LastMismatch = $"{when}: {unmatched.Count} expected not drawn (first: {(unmatched.Count > 0 ? unmatched[0].ToString() : "-")}), {free.Count} drawn not expected (first: {(free.Count > 0 ? $"\"{free[0].Text}\" at ({free[0].Bottom.x:F2},{free[0].Bottom.y:F2}) scale {free[0].Scale:F3} opacity {free[0].Opacity:F3}" : "-")})";
            }
            if (failOnMismatch && (unmatched.Count > 0 || free.Count > 0))
            {
                var sb = new StringBuilder($"{when}: the drawn numbers differ from the plan's formulas.\nNot drawn as expected:");
                foreach (Expected e in unmatched) sb.Append("\n  ").Append(e);
                sb.Append("\nDrawn and not expected:");
                foreach (Drawn d in free) sb.Append($"\n  \"{d.Text}\" at ({d.Bottom.x:F2},{d.Bottom.y:F2}) scale {d.Scale:F3} opacity {d.Opacity:F3}");
                Assert.Fail(sb.ToString());
            }
            return pairs;
        }

        /// <summary>One frame later: the drawn numbers against the model's.</summary>
        protected void AssertFrame(ReferenceStack stack, List<Expected> expected, string when) =>
            MatchDrawn(expected, DrawnNumbers(), when);

        /// <summary>Reads a screen-less result of what a Show-driven frame drew: count of visible numbers.</summary>
        protected int VisibleNumbers() => DrawnNumbers().Count;
    }
}
