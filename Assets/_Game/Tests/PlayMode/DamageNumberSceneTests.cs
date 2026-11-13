using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using ElementalTD.Core;
using ElementalTD.Presentation;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using UnityEngine.UIElements;
using Object = UnityEngine.Object;

namespace ElementalTD.PlayModeTests
{
    /// <summary>
    /// The damage numbers on the scene (plan 0009, 4d-AC6 to AC8, AC13, AC15, AC16, AC18). Ticks are driven by the test, one per frame
    /// where a criterion needs frames; the frame a test reads is the one that followed its last tick. Records are given to the view's
    /// public Show and, where a criterion is a formula, also to a model written from the plan (FloatingSceneBase.ReferenceStack).
    /// </summary>
    public partial class DamageNumberSceneTests : FloatingSceneBase
    {
        private const float P = 0.08f;

        /// <summary>The visual direction's top-line step (section 2): pinned here, not read from the scene, so a view that ignores its field fails. A tuning changes this and the scene-values test.</summary>
        private const float DirectionLineStep = 34f;

        private static float Ease(float t) => 1f - (1f - t) * (1f - t);

        /// <summary>Slot 0's bottom centre over an enemy as it is drawn: 0.6 cells above its centre, towards the top of the screen.</summary>
        private Vector2 Slot0Over(Enemy enemy) => PanelOf(DrawnWorld(enemy) + new Vector3(0f, 0f, 0.6f * _map.CellSize));

        /// <summary>
        /// One tick and one frame per step. <paramref name="due"/> gives the records to show after that tick (given to the view and the
        /// model); the frame is compared with the model; <paramref name="check"/> sees the matched numbers.
        /// </summary>
        private IEnumerator Drive(ReferenceStack model, int ticks, Func<int, List<Hit>> due, Action<int, List<(Drawn drawn, Expected expected)>> check = null)
        {
            var lastLift = new Dictionary<int, float>();
            var lastSlot = new Dictionary<int, float>();
            for (int k = 0; k < ticks; k++)
            {
                Battle.Tick();
                List<Hit> hits = due?.Invoke(k);
                if (hits != null)
                    foreach (Hit hit in hits)
                    {
                        NumberView.Show(hit);
                        model.Show(hit);
                    }
                yield return null;
                var pairs = MatchDrawn(model.Frame(BoardDrawn()), DrawnNumbers(), $"tick {k}");
                foreach (var pair in pairs)
                {
                    // Nothing moves down against its enemy, and no slot falls.
                    if (lastLift.TryGetValue(pair.expected.Order, out float lift)) Assert.GreaterOrEqual(pair.expected.Lift, lift - 1e-3f, $"tick {k}: {pair.expected} moved down");
                    if (lastSlot.TryGetValue(pair.expected.Order, out float slot)) Assert.GreaterOrEqual(pair.expected.EasedSlot, slot - 1e-3f, $"tick {k}: {pair.expected} slot fell");
                    lastLift[pair.expected.Order] = pair.expected.Lift;
                    lastSlot[pair.expected.Order] = pair.expected.EasedSlot;
                }
                check?.Invoke(k, pairs);
            }
        }

        private static void AssertNoOverlap(List<(Drawn drawn, Expected expected)> pairs, string when, bool exemptTopLine)
        {
            for (int i = 0; i < pairs.Count; i++)
                for (int j = i + 1; j < pairs.Count; j++)
                {
                    if (exemptTopLine && (pairs[i].expected.OnLine || pairs[j].expected.OnLine)) continue;
                    Assert.IsFalse(Overlaps(pairs[i].drawn.Rect, pairs[j].drawn.Rect), $"{when}: {pairs[i].expected} and {pairs[j].expected} overlap");
                }
        }

        // ---- The scene holds the direction's values

        [UnityTest]
        public IEnumerator S4d_AC6_To_AC8_And_AC16_The_Scene_Holds_The_Values_Of_The_Visual_Direction()
        {
            yield return null;
            Assert.AreEqual(0.35f, LifeOf(HitKind.Weak));
            Assert.AreEqual(0.45f, LifeOf(HitKind.Normal));
            Assert.AreEqual(0.5f, LifeOf(HitKind.Strong));
            Assert.AreEqual(17f, HeightOf(HitKind.Weak));
            Assert.AreEqual(20f, HeightOf(HitKind.Normal));
            Assert.AreEqual(23f, HeightOf(HitKind.Strong));
            Assert.AreEqual(0.6f, SceneValue("heightCells"), "28 above the enemy: 0.6 cells");
            Assert.AreEqual(0.08f, PushSeconds);
            Assert.AreEqual(3, (int)SceneValue("budgetSlots"));
            Assert.AreEqual(0.7f, SceneValue("topSlotOpacity"));
            Assert.AreEqual(0.5f, SceneValue("opaqueShare"));
            Assert.AreEqual(47f, SceneValue("topLimitPixels"));
            Assert.AreEqual(DirectionLineStep, SceneValue("lineStepPixels"));
            Assert.AreEqual(40, (int)SceneValue("maxNumbers"));
            Assert.AreEqual(30, (int)SceneValue("weakCutoff"));
            Assert.AreEqual(0.35f, SceneValue("wordDepthCells"));
            Assert.AreEqual(1f, SceneValue("immuneHoldSeconds"));
            Assert.AreEqual(0.2f, SceneValue("immuneFadeSeconds"));
            AssertPoolIntact();
        }

        [UnityTest]
        public IEnumerator S4d_AC6_Each_Kind_Has_Its_Class_Size_Weight_Colour_Outline_And_A_Label_As_Tall_As_Its_Height_Field()
        {
            yield return null;
            NumberView.Show(HitAtCell(3, 3, 7f, HitKind.Weak, OffBoard + 1));
            NumberView.Show(HitAtCell(6, 3, 8f, HitKind.Normal, OffBoard + 2));
            NumberView.Show(HitAtCell(9, 3, 16f, HitKind.Strong, OffBoard + 3));
            yield return null;
            Battle.Tick(); // grown by the next frame: scale 1 needs 5 ticks
            for (int i = 0; i < 5; i++)
            {
                Battle.Tick();
                yield return null;
            }

            var look = new Dictionary<string, (string cls, float size, FontStyle style, string colour, float outline, HitKind kind)>
            {
                ["7"] = ("damage-number--weak", 14f, FontStyle.Normal, "B3ACA0", 1f, HitKind.Weak),
                ["8"] = ("damage-number--normal", 17f, FontStyle.Bold, "FFFFFF", 2f, HitKind.Normal),
                ["16"] = ("damage-number--strong", 20f, FontStyle.Bold, "FFC23D", 2f, HitKind.Strong),
            };
            List<Drawn> drawn = DrawnNumbers();
            Assert.AreEqual(3, drawn.Count);
            foreach (Drawn d in drawn)
            {
                var (cls, size, style, colour, outline, kind) = look[d.Text];
                IResolvedStyle r = d.Label.resolvedStyle;
                Assert.IsTrue(d.Label.ClassListContains(cls), d.Text);
                Assert.AreEqual(size, r.fontSize, d.Text + " size");
                Assert.AreEqual(style, r.unityFontStyleAndWeight, d.Text + " weight");
                AssertColor(Hex(colour), r.color, d.Text + " colour");
                Assert.AreEqual(outline, r.unityTextOutlineWidth, d.Text + " outline width");
                AssertColor(Color.black, r.unityTextOutlineColor, d.Text + " outline colour");
                Assert.AreEqual(1f, d.Scale, 1e-6f);
                Assert.LessOrEqual(d.Label.layout.height, HeightOf(kind) + 0.5f, d.Text + ": the label is at most half a pixel taller than its height field");
                Assert.AreEqual(d.Bottom.x, d.Rect.center.x, PositionTolerance, d.Text + ": hung by its centre");
                Assert.AreEqual(d.Bottom.y, d.Rect.yMax, PositionTolerance, d.Text + ": hung by its bottom");
            }
            AssertPoolIntact();
        }

        // ---- 4d-AC6 a number per hit

        [UnityTest]
        public IEnumerator S4d_AC6_A_Real_Hit_Shows_One_Number_That_Grows_As_The_Square_Of_Its_Age_And_Hangs_Over_The_Enemy()
        {
            Element wave = Battle.Waves.WaveAt(0).GroupAt(0).Enemy.Element;
            BuyFree(2, 3, OtherElement(wave));
            Enemy enemy = SpawnWave1Enemy(health: 1_000_000f);
            yield return null;
            Assert.AreEqual(0, VisibleNumbers());

            int guard = 0;
            while (Battle.HitCount == 0 && guard++ < 200)
            {
                Battle.Tick();
                yield return null;
                if (Battle.HitCount == 0) Assert.AreEqual(0, VisibleNumbers(), "no number before a landing");
            }
            Assert.AreEqual(1, Battle.HitCount, "a shot landed");
            Hit hit = Battle.HitAt(0);
            List<Drawn> drawn = DrawnNumbers();
            Assert.AreEqual(1, drawn.Count, "exactly one number on the frame after the landing");
            Assert.AreEqual(Mathf.Round(hit.Amount).ToString(CultureInfo.InvariantCulture), drawn[0].Text, "the record's amount");
            Assert.AreEqual("20", drawn[0].Text, "x2 of the tower's 10");
            Assert.IsTrue(drawn[0].Label.ClassListContains("damage-number--strong"));
            Assert.AreEqual(20f, drawn[0].Label.resolvedStyle.fontSize);
            AssertColor(Hex("FFC23D"), drawn[0].Label.resolvedStyle.color, "strong colour");
            Assert.AreEqual(0f, drawn[0].Scale, 1e-6f, "scale 0 on the frame of its tick");
            Assert.AreEqual(1f, drawn[0].Opacity, 1e-6f);

            int landed = Battle.TickCount;
            for (int age = 0; age <= 8; age++)
            {
                if (age > 0)
                {
                    Battle.Tick();
                    yield return null;
                }
                Assert.AreEqual(landed + age, Battle.TickCount);
                drawn = DrawnNumbers();
                Assert.AreEqual(1, drawn.Count, $"age {age} ticks");
                float grown = Mathf.Min(1f, age * Dt / P);
                Assert.AreEqual(grown * grown, drawn[0].Scale, 0.002f, $"age {age}: scale is (age / p) squared, 1 from p on");
                Vector2 expected = OnBoard(enemy) ? Slot0Over(enemy) : PanelOfCell(hit.X, hit.Y, 0.6f);
                Assert.AreEqual(expected.x, drawn[0].Bottom.x, PositionTolerance, $"age {age} x");
                Assert.AreEqual(expected.y, drawn[0].Bottom.y, PositionTolerance, $"age {age}: the direction's height above the enemy view");
                if (age >= 5)
                {
                    Assert.AreEqual(drawn[0].Bottom.x, drawn[0].Rect.center.x, PositionTolerance);
                    Assert.AreEqual(drawn[0].Bottom.y, drawn[0].Rect.yMax, PositionTolerance, "grown about its bottom centre");
                }
            }
            AssertPoolIntact();
        }

        /// <summary>The numbers of a snapshot, in a form that compares: sorted by text and place.</summary>
        private List<(string text, Vector2 bottom, float scale, float opacity)> Snapshot()
        {
            var list = new List<(string, Vector2, float, float)>();
            foreach (Drawn d in DrawnNumbers()) list.Add((d.Text, d.Bottom, d.Scale, d.Opacity));
            list.Sort((a, b) => a.Item2.x != b.Item2.x ? a.Item2.x.CompareTo(b.Item2.x) : a.Item2.y.CompareTo(b.Item2.y));
            return list;
        }

        private List<(string text, Vector2 bottom, float scale, float opacity)> _snapshot;

        /// <summary>Five or two towers of the other element on a still enemy; ticks until the first shot is launched, then this many more.</summary>
        private IEnumerator RealHitsTo(int towers, int ticksAfterLaunch, bool frameEveryTick, ReferenceStack[] modelOut)
        {
            Element wave = Battle.Waves.WaveAt(0).GroupAt(0).Enemy.Element;
            Element other = OtherElement(wave);
            var cells = new[] { (1, 3), (1, 5), (0, 3), (0, 5), (2, 3) };
            for (int i = 0; i < towers; i++) BuyFree(cells[i].Item1, cells[i].Item2, other);
            Board.SpawnEnemy(new EnemyStats(0f, 1_000_000f, 0, wave));
            yield return null;
            ReferenceStack model = NewStack();
            modelOut[0] = model;

            Battle.Tick();
            if (frameEveryTick)
            {
                yield return null;
                MatchDrawn(model.Frame(BoardDrawn()), DrawnNumbers(), "launch tick");
            }
            Assert.Greater(Battle.ProjectileCount, 0, "the towers fired on the first tick");
            for (int k = 0; k < ticksAfterLaunch; k++)
            {
                Battle.Tick();
                if (!frameEveryTick) continue;
                yield return null;
                MatchDrawn(model.Frame(BoardDrawn()), DrawnNumbers(), $"tick {k + 2}");
            }
            if (!frameEveryTick) yield return null;
            var expected = model.Frame(BoardDrawn());
            MatchDrawn(expected, DrawnNumbers(), "the last frame");
            _snapshot = Snapshot();
        }

        [UnityTest]
        public IEnumerator S4d_AC6_Many_Ticks_In_One_Frame_Show_The_Same_Numbers_As_One_Tick_Per_Frame(
            [Values(2, 5)] int towers, [Values(12, 20, 28)] int ticksAfterLaunch)
        {
            var model = new ReferenceStack[1];
            yield return RealHitsTo(towers, ticksAfterLaunch, false, model);
            var late = _snapshot;
            Assert.Greater(Battle.HitCount, 0);
            int records = Battle.HitCount;

            var old = _bootstrap;
            _bootstrap.Restart();
            int guard = 0;
            while (old != null && guard++ < 120) yield return null;
            Assert.IsTrue(old == null, "the scene never reloaded");
            yield return null;
            AcquireScene(disableInput: true);
            yield return Frames(3);

            yield return RealHitsTo(towers, ticksAfterLaunch, true, model);
            var every = _snapshot;
            Assert.AreEqual(records, Battle.HitCount, "the same battle");
            Assert.AreEqual(every.Count, late.Count, "the same number of numbers");
            for (int i = 0; i < late.Count; i++)
            {
                Assert.AreEqual(every[i].text, late[i].text, $"number {i}");
                Assert.AreEqual(every[i].bottom.x, late[i].bottom.x, 0.01f, $"number {i} translate x");
                Assert.AreEqual(every[i].bottom.y, late[i].bottom.y, 0.01f, $"number {i} translate y");
                Assert.AreEqual(every[i].scale, late[i].scale, 0.001f, $"number {i} scale");
                Assert.AreEqual(every[i].opacity, late[i].opacity, 0.001f, $"number {i} opacity");
            }
            AssertPoolIntact();
        }

        [UnityTest]
        public IEnumerator S4d_AC6_More_Than_64_Unread_Hits_Show_Only_Those_Within_Their_Life_And_Throw_Nothing()
        {
            Element wave = Battle.Waves.WaveAt(0).GroupAt(0).Enemy.Element;
            Element other = OtherElement(wave);
            foreach (var cell in new[] { (1, 3), (1, 5), (0, 3), (0, 5), (2, 3) }) BuyFree(cell.Item1, cell.Item2, other);
            Board.SpawnEnemy(new EnemyStats(0f, 1_000_000f, 0, wave));
            yield return null;
            ReferenceStack model = NewStack();
            for (int i = 0; i < 900; i++) Battle.Tick();
            Assert.Greater(Battle.HitCount, Battle.HitLogCapacity + 10, "more hits than the log holds, none read");

            yield return null;

            var expected = model.Frame(BoardDrawn());
            Assert.Greater(expected.Count, 0);
            MatchDrawn(expected, DrawnNumbers(), "after 900 ticks with no frame");
            AssertPoolIntact();
        }

        [UnityTest]
        public IEnumerator S4d_AC6_The_Reference_Order_Run_For_3000_Ticks_With_No_Frame_Then_One_Frame_Shows_Only_Records_Within_Their_Life()
        {
            yield return null;
            ReferenceStack model = NewStack();
            for (int ticks = 0; ticks < 3000; ticks++)
            {
                RunReferenceActionsAt(ticks);
                Battle.Tick();
            }
            yield return null;
            MatchDrawn(model.Frame(BoardDrawn()), DrawnNumbers(), "after 3000 ticks");
            AssertPoolIntact();
        }

        [UnityTest]
        public IEnumerator S4d_AC6_The_Tick_Fraction_Is_Part_Of_A_Numbers_Age_And_Of_Where_Its_Enemy_Is_Drawn()
        {
            Enemy enemy = SpawnWave1Enemy(health: 1_000_000f);
            yield return null;
            ReferenceStack model = NewStack();
            for (int i = 0; i < 20; i++) Battle.Tick();
            SetTickFraction(0.5f);
            model.Alpha = 0.5f;
            Hit hit = HitOn(enemy, 20f, HitKind.Strong);
            NumberView.Show(hit);
            model.Show(hit);
            yield return null;

            Assert.AreEqual(0.5f, _bootstrap.TickAlpha, 1e-4f);
            var pairs = MatchDrawn(model.Now(BoardDrawn()), DrawnNumbers(), "half a tick");
            float grown = 0.5f * Dt / P;
            Assert.AreEqual(grown * grown, pairs[0].drawn.Scale, 0.001f, "aged by half a tick");
            Vector2 expected = Slot0Over(enemy);
            Assert.AreEqual(expected.y, pairs[0].drawn.Bottom.y, PositionTolerance, "over the enemy as drawn between its ticks");
            Vector3 world = DrawnWorld(enemy);
            Assert.AreEqual((enemy.PreviousX + enemy.X) * 0.5f + 0.5f, world.x, 1e-3f, "the view is halfway between the ticks");
            Assert.AreNotEqual(enemy.PreviousX, enemy.X);
        }

        // ---- 4d-AC6 to AC8 through Show

        [UnityTest]
        public IEnumerator S4d_AC6_To_AC8_One_Of_Each_Kind_Has_Its_Life_Full_Opacity_To_The_Share_Then_A_Linear_Fade_And_Is_Hidden_When_Its_Life_Ends(
            [Values(HitKind.Weak, HitKind.Normal, HitKind.Strong)] HitKind kind)
        {
            Enemy enemy = SpawnWave1Enemy(health: 1_000_000f);
            yield return null;
            float life = LifeOf(kind);
            int endTick = Mathf.RoundToInt(life / Dt);
            ReferenceStack model = NewStack();
            bool shown = false;
            yield return Drive(model, endTick + 3, k => shown ? null : new List<Hit> { HitOn(enemy, 9f, kind) }, (k, pairs) =>
            {
                shown = true;
                if (k < endTick - 1) Assert.AreEqual(1, pairs.Count, $"alive at {k} ticks of {endTick}");
                if (k > endTick) Assert.AreEqual(0, pairs.Count, $"gone at {k}");
                if (k == endTick) Assert.AreEqual(0, pairs.Count, "hidden on the tick its life ends");
                if (pairs.Count == 0) return;
                float t = k * Dt / life;
                float opacity = t <= 0.5f ? 1f : (1f - t) / 0.5f;
                Assert.AreEqual(opacity, pairs[0].drawn.Opacity, 0.002f, $"{kind} at {k}");
            });
            AssertPoolIntact();
        }

        [UnityTest]
        public IEnumerator S4d_AC6_Amounts_Round_Half_Up_To_At_Least_1_Written_Without_Grouping_Whatever_The_Culture()
        {
            yield return null;
            var previous = CultureInfo.CurrentCulture;
            var previousUi = CultureInfo.CurrentUICulture;
            var comma = (CultureInfo)CultureInfo.InvariantCulture.Clone();
            comma.NumberFormat.NumberDecimalSeparator = ",";
            comma.NumberFormat.NumberGroupSeparator = ".";
            comma.NumberFormat.NumberGroupSizes = new[] { 3 };
            comma.NumberFormat.NegativeSign = "~";
            var amounts = new[] { (2.4f, "2"), (2.5f, "3"), (0.2f, "1"), (16f, "16"), (0.5f, "1"), (1234f, "1234"), (1499.5f, "1500"), (99999f, "99999") };
            try
            {
                CultureInfo.CurrentCulture = comma;
                CultureInfo.CurrentUICulture = comma;
                int entry = OffBoard;
                foreach (var (amount, _) in amounts) NumberView.Show(HitAtCell(1 + entry % 10, 3, amount, HitKind.Strong, entry++));
                NumberView.Show(HitAtCell(2, 5, 16f, HitKind.Strong, entry++)); // 16 again, on another label
                yield return null;
            }
            finally
            {
                CultureInfo.CurrentCulture = previous;
                CultureInfo.CurrentUICulture = previousUi;
            }

            var texts = new List<string>();
            foreach (Drawn d in DrawnNumbers()) texts.Add(d.Text);
            foreach (var (_, text) in amounts) Assert.Contains(text, texts);
            Assert.AreEqual(amounts.Length + 1, texts.Count);

            var sixteens = DrawnNumbers().FindAll(d => d.Text == "16");
            Assert.AreEqual(2, sixteens.Count);
            Assert.AreSame(sixteens[0].Label.text, sixteens[1].Label.text, "an amount is written once: the same string object");
        }

        [UnityTest]
        public IEnumerator S4d_AC7_A_Second_Record_Lifts_The_First_By_Its_Height_Eased_Out_Over_P_And_The_Two_Never_Intersect(
            [Values(0, 1, 2, 3, 5, 10)] int gap)
        {
            Enemy enemy = SpawnWave1Enemy(health: 1_000_000f);
            yield return null;
            ReferenceStack model = NewStack();
            yield return Drive(model, gap + 40, k =>
            {
                var hits = new List<Hit>();
                if (k == 0) hits.Add(HitOn(enemy, 20f, HitKind.Strong));
                if (k == gap) hits.Add(HitOn(enemy, 21f, HitKind.Strong));
                return hits;
            }, (k, pairs) =>
            {
                AssertNoOverlap(pairs, $"gap {gap}, tick {k}", exemptTopLine: true);
                var older = pairs.Find(p => p.expected.Order == 0);
                if (older.expected == null || k < gap) return;
                float lift = 23f * Ease(Mathf.Min(1f, (k - gap) * Dt / P));
                Assert.AreEqual(lift, older.expected.Lift, 0.01f, $"tick {k}: the older number's lift, by the plan's formula");
                if (!older.expected.OnLine)
                    Assert.AreEqual(Slot0Over(enemy).y - lift, older.drawn.Bottom.y, PositionTolerance, $"tick {k}: the older number is lifted");
                var newer = pairs.Find(p => p.expected.Order == 1);
                if (newer.expected != null && !older.expected.OnLine && !newer.expected.OnLine)
                    Assert.AreEqual(Slot0Over(enemy).y, newer.drawn.Bottom.y, PositionTolerance, "the newest stays at slot 0");
            });
            AssertPoolIntact();
        }

        [UnityTest]
        public IEnumerator S4d_AC7_A_Third_Record_Adds_Its_Lift_And_The_Oldest_Is_At_Most_At_The_Top_Slots_Opacity()
        {
            Enemy enemy = SpawnWave1Enemy(health: 1_000_000f);
            yield return null;
            ReferenceStack model = NewStack();
            yield return Drive(model, 25, k => k == 0 || k == 6 || k == 12 ? new List<Hit> { HitOn(enemy, 20f, HitKind.Strong) } : null, (k, pairs) =>
            {
                AssertNoOverlap(pairs, $"tick {k}", true);
                if (k < 12 + 5) return;
                Assert.AreEqual(3, pairs.Count);
                float baseY = Slot0Over(enemy).y;
                foreach (var pair in pairs)
                {
                    if (pair.expected.OnLine) return;
                    Assert.AreEqual(baseY - 23f * (2 - pair.expected.Order), pair.drawn.Bottom.y, PositionTolerance, $"slot of number {pair.expected.Order}: the lifts add up");
                }
                var oldest = pairs.Find(p => p.expected.Order == 0);
                Assert.AreEqual(Mathf.Min(0.7f, oldest.drawn.Opacity), oldest.drawn.Opacity, 1e-4f);
                Assert.LessOrEqual(oldest.drawn.Opacity, 0.7f + 1e-4f, "the top slot's opacity is at most 0.7");
                Assert.AreEqual(2, oldest.expected.Slot);
            });
            AssertPoolIntact();
        }

        [UnityTest]
        public IEnumerator S4d_AC7_A_Weak_Under_A_Strong_Lifts_It_By_The_Weak_Height_And_The_Strong_One_Stays_Up_After_The_Weak_One_Ends()
        {
            Enemy enemy = SpawnWave1Enemy(health: 1_000_000f);
            yield return null;
            ReferenceStack model = NewStack();
            int weakEnds = Mathf.RoundToInt(0.35f / Dt);
            int checkedAfter = 0;
            yield return Drive(model, 29, k =>
            {
                if (k == 0) return new List<Hit> { HitOn(enemy, 20f, HitKind.Strong) };
                if (k == 2) return new List<Hit> { HitOn(enemy, 3f, HitKind.Weak) };
                return null;
            }, (k, pairs) =>
            {
                var strong = pairs.Find(p => p.expected.Kind == HitKind.Strong);
                Assert.IsNotNull(strong.expected);
                if (k < 2 + 5 || strong.expected.OnLine) return;
                Assert.AreEqual(Slot0Over(enemy).y - 17f, strong.drawn.Bottom.y, PositionTolerance, $"tick {k}: lifted by the weak height, 17");
                if (k > 2 + weakEnds) checkedAfter++;
                if (k > 2 + weakEnds) Assert.AreEqual(1, pairs.Count, "the weak number is gone");
            });
            Assert.Greater(checkedAfter, 0, "the strong number was seen standing after the weak one ended");
        }

        [UnityTest]
        public IEnumerator S4d_AC8_Four_Records_Six_Ticks_Apart_Fill_Three_Slots_And_The_Fourth_Makes_The_First_Leave_Fading_Over_P()
        {
            Enemy enemy = SpawnWave1Enemy(health: 1_000_000f);
            yield return null;
            ReferenceStack model = NewStack();
            var seenLeaving = new List<float>();
            yield return Drive(model, 30, k => k % 6 == 0 && k <= 18 ? new List<Hit> { HitOn(enemy, 20f, HitKind.Strong) } : null, (k, pairs) =>
            {
                var first = pairs.Find(p => p.expected.Order == 0);
                if (k >= 12 && k < 18)
                {
                    Assert.AreEqual(0.7f, first.drawn.Opacity, 0.002f, $"tick {k}: the number in the last slot is at the top slot's opacity");
                    Assert.AreEqual(2, first.expected.Slot);
                }
                if (k >= 18 && first.expected != null)
                {
                    int after = k - 18;
                    Assert.IsTrue(first.expected.Leaving);
                    float t = k * Dt / 0.5f;
                    float raw = Mathf.Min(0.7f, t <= 0.5f ? 1f : (1f - t) / 0.5f);
                    Assert.AreEqual(raw * Mathf.Max(0f, 1f - after * Dt / P), first.drawn.Opacity, 0.002f, $"tick {k}: fading to 0 over p from the fourth hit's tick");
                    Assert.AreEqual(46f + 23f * Ease(Mathf.Min(1f, after * Dt / P)), first.expected.Lift, 0.01f, $"tick {k}: lifted one more height by the fourth hit");
                    seenLeaving.Add(first.drawn.Opacity);
                }
                if (k >= 18 + 5) Assert.IsNull(first.expected, $"tick {k}: the first number is hidden, its label free");
                if (k < 18) Assert.AreEqual(k / 6 + 1, pairs.Count, $"tick {k}: a number per record, three slots");
                else if (k < 18 + 5) Assert.AreEqual(4, pairs.Count, $"tick {k}: the leaving number is still drawn");
                else Assert.AreEqual(3, pairs.Count, $"tick {k}");
            });
            Assert.Greater(seenLeaving.Count, 3, "the first number was seen leaving");
            AssertPoolIntact();
        }

        [UnityTest]
        public IEnumerator S4d_AC8_Five_Records_On_One_Tick_Leave_Three_Numbers_And_Two_Leaving()
        {
            Enemy enemy = SpawnWave1Enemy(health: 1_000_000f);
            yield return null;
            ReferenceStack model = NewStack();
            yield return Drive(model, 8, k =>
            {
                if (k != 0) return null;
                var hits = new List<Hit>();
                for (int i = 0; i < 5; i++) hits.Add(HitOn(enemy, 10f + i, HitKind.Strong));
                return hits;
            }, (k, pairs) =>
            {
                if (k == 0)
                {
                    Assert.AreEqual(5, pairs.Count, "all five show on their tick");
                    Assert.AreEqual(2, pairs.FindAll(p => p.expected.Leaving).Count, "two are already leaving");
                    Assert.AreEqual(3, pairs.FindAll(p => !p.expected.Leaving).Count);
                }
                if (k >= 5) Assert.AreEqual(3, pairs.Count, $"tick {k}: three numbers once the two have faded");
            });
            AssertPoolIntact();
        }

        [UnityTest]
        public IEnumerator S4d_AC8_A_Record_Naming_Another_Enemy_Lifts_None_Of_The_First_Enemys_Numbers()
        {
            Enemy first = SpawnWave1Enemy(health: 1_000_000f);
            Battle.Tick();
            Battle.Tick();
            Enemy second = SpawnWave1Enemy(health: 1_000_000f);
            yield return null;
            ReferenceStack model = NewStack();
            yield return Drive(model, 20, k =>
            {
                if (k == 0) return new List<Hit> { HitOn(first, 20f, HitKind.Strong) };
                if (k == 3) return new List<Hit> { HitOn(second, 21f, HitKind.Strong), HitOn(second, 22f, HitKind.Strong) };
                return null;
            }, (k, pairs) =>
            {
                var onFirst = pairs.Find(p => p.expected.Entry == first.Entry);
                Assert.AreEqual(0, onFirst.expected.Slot, "the first enemy's number has no newer number of its own");
                if (!onFirst.expected.OnLine && k >= 1) Assert.AreEqual(Slot0Over(first).y, onFirst.drawn.Bottom.y, PositionTolerance, $"tick {k}: not lifted");
            });
        }

        [UnityTest]
        public IEnumerator S4d_AC8_At_The_Cap_Of_40_A_New_Hit_Takes_The_Oldest_Numbers_Label()
        {
            yield return null;
            for (int k = 1; k <= 45; k++) NumberView.Show(HitAtCell(1 + k % 14, 3 + k % 3, k, HitKind.Strong, OffBoard + k));
            yield return null;

            var texts = new List<string>();
            foreach (Drawn d in DrawnNumbers()) texts.Add(d.Text);
            Assert.AreEqual(40, texts.Count, "40 alive");
            for (int k = 1; k <= 5; k++) CollectionAssert.DoesNotContain(texts, k.ToString(), $"the oldest ({k}) was recycled");
            for (int k = 6; k <= 45; k++) CollectionAssert.Contains(texts, k.ToString());
            Assert.AreEqual(40, new HashSet<VisualElement>(DrawnNumbers().ConvertAll(d => d.Label.parent)).Count, "forty labels, no label shown twice");
            AssertPoolIntact();

            // Everything ends; the alive count has not drifted: 29 numbers and then a weak one still show.
            for (int i = 0; i < 31; i++) { Battle.Tick(); yield return null; }
            Assert.AreEqual(0, VisibleNumbers());
            for (int k = 1; k <= 29; k++) NumberView.Show(HitAtCell(2 + k % 12, 3, k, HitKind.Strong, OffBoard + 100 + k));
            NumberView.Show(HitAtCell(3, 5, 7f, HitKind.Weak, OffBoard + 500));
            yield return null;
            Assert.AreEqual(30, VisibleNumbers(), "a weak hit shows with 29 alive after the cap was reached and everything ended");
        }

        [UnityTest]
        public IEnumerator S4d_AC8_A_Weak_Hit_Shows_Below_30_Alive_And_Not_From_30_And_Lifts_None_Then_Shows_Again_Below_30()
        {
            yield return null;
            ReferenceStack model = NewStack();
            const int enemyEntry = OffBoard;
            void ShowBoth(Hit hit)
            {
                NumberView.Show(hit);
                model.Show(hit);
            }

            // 28 strong numbers at tick 0, one more at tick 5.
            for (int i = 0; i < 28; i++) ShowBoth(HitAtCell(2 + i % 12, 3, 30 + i, HitKind.Strong, enemyEntry + 1 + i));
            ShowBoth(HitAtCell(2, 6, 1f, HitKind.Weak, enemyEntry + 1)); // 29 alive -> shows (30 alive)
            yield return null;
            Assert.AreEqual(29, VisibleNumbers(), "with 28 alive a weak one shows");
            ShowBoth(HitAtCell(2, 6, 2f, HitKind.Weak, enemyEntry + 1)); // 29 alive: shows
            yield return null;
            Assert.AreEqual(30, VisibleNumbers(), "with 29 alive a weak one shows");
            var before = Snapshot();
            ShowBoth(HitAtCell(2, 6, 3f, HitKind.Weak, enemyEntry + 1)); // 30 alive: none
            for (int i = 0; i < 5; i++)
            {
                Battle.Tick();
                yield return null;
            }
            Assert.AreEqual(30, VisibleNumbers(), "with 30 alive a weak one shows none");
            MatchDrawn(model.Frame(BoardDrawn()), DrawnNumbers(), "a skipped weak hit lifts nothing");

            ShowBoth(HitAtCell(2, 6, 4f, HitKind.Normal, enemyEntry + 1));
            yield return null;
            Assert.AreEqual(31, VisibleNumbers(), "a normal one shows with 30 alive");
            MatchDrawn(model.Frame(BoardDrawn()), DrawnNumbers(), "and lifts");

            for (int i = 0; i < 27; i++)
            {
                Battle.Tick();
                yield return null;
                model.Frame(BoardDrawn());
            }
            // The 28 strong ones and the two weak ones are gone; the normal one lives to 27 ticks after its own.
            ShowBoth(HitAtCell(4, 6, 5f, HitKind.Weak, enemyEntry + 100));
            yield return null;
            var drawn = DrawnNumbers();
            Assert.Less(drawn.Count, 30);
            Assert.IsTrue(drawn.Exists(d => d.Text == "5"), "the weak hit shows again once fewer than 30 are alive");
            MatchDrawn(model.Frame(BoardDrawn()), drawn, "after the weak one shows again");
            Assert.AreEqual(30, before.Count);
            AssertPoolIntact();
        }

        // ---- 4d-AC15 following the enemy

        [UnityTest]
        public IEnumerator S4d_AC15_Numbers_Hang_From_A_Walking_Enemy_Through_A_Turn_And_A_Diagonal_Step_Whatever_The_Tick_Fraction([Values(0f, 0.5f)] float fraction)
        {
            Enemy enemy = SpawnWave1Enemy(health: 1_000_000f);
            yield return null;
            ReferenceStack model = NewStack();
            SetTickFraction(fraction);
            model.Alpha = fraction;
            bool diagonal = false, turned = false;
            float lastDx = 0;
            int checkedFrames = 0;
            yield return Drive(model, 900, k => k % 12 == 0 && OnBoard(enemy) ? new List<Hit> { HitOn(enemy, 20f, HitKind.Strong) } : null, (k, pairs) =>
            {
                if (!OnBoard(enemy)) return;
                diagonal |= enemy.Follower.From.X != enemy.Follower.To.X && enemy.Follower.From.Y != enemy.Follower.To.Y;
                float dx = enemy.X - enemy.PreviousX;
                if (k > 2 && Mathf.Abs(dx - lastDx) > 0.001f) turned = true;
                lastDx = dx;
                foreach (var pair in pairs)
                    if (pair.expected.Slot == 0 && !pair.expected.OnLine && pair.expected.Entry == enemy.Entry)
                    {
                        Vector2 expected = Slot0Over(enemy);
                        Assert.AreEqual(expected.x, pair.drawn.Bottom.x, PositionTolerance, $"tick {k}");
                        Assert.AreEqual(expected.y, pair.drawn.Bottom.y, PositionTolerance, $"tick {k}: the direction's height above the enemy view");
                        checkedFrames++;
                    }
            });
            Assert.IsTrue(diagonal, "the walk had a diagonal step");
            Assert.IsTrue(turned, "and a turn");
            Assert.Greater(checkedFrames, 100);
        }

        [UnityTest]
        public IEnumerator S4d_AC15_The_Numbers_Of_A_Killed_Enemy_Stay_Over_The_Killing_Record_With_Their_Lifts_Also_After_The_Camera_Changes_And_End_On_Their_Own_Ticks()
        {
            Element wave = Battle.Waves.WaveAt(0).GroupAt(0).Enemy.Element;
            Element other = OtherElement(wave);
            foreach (var cell in new[] { (1, 3), (1, 5), (2, 3) }) BuyFree(cell.Item1, cell.Item2, other);
            Enemy enemy = SpawnWave1Enemy(health: 50f); // three hits of 20 kill it
            yield return null;
            ReferenceStack model = NewStack();

            int guard = 0;
            while (OnBoard(enemy) && guard++ < 100)
            {
                Battle.Tick();
                yield return null;
                MatchDrawn(model.Frame(BoardDrawn()), DrawnNumbers(), $"tick {Battle.TickCount}");
            }
            Assert.IsFalse(OnBoard(enemy), "killed");
            Assert.AreEqual(3, Battle.HitCount);
            Hit kill = Battle.HitAt(2);
            int killTick = Battle.TickCount;
            Vector2 stand = PanelOfCell(kill.X, kill.Y, 0.6f);
            List<Drawn> drawn = DrawnNumbers();
            Assert.AreEqual(3, drawn.Count, "all three numbers are on screen at the kill");
            foreach (Drawn column in drawn) Assert.AreEqual(stand.x, column.Bottom.x, PositionTolerance, "one column over the killing record's position");

            Drawn newest = drawn.Find(d => Mathf.Abs(d.Bottom.x - stand.x) < PositionTolerance && Mathf.Abs(d.Bottom.y - stand.y) < PositionTolerance);
            Assert.IsNotNull(newest.Label, "the killing hit's number stands at slot 0 over the killing record's position");
            var settled = new List<Vector2>();
            for (int k = 0; k < 6; k++)
            {
                Battle.Tick();
                yield return null;
                MatchDrawn(model.Frame(BoardDrawn()), DrawnNumbers(), "after the kill");
            }
            foreach (Drawn d in DrawnNumbers()) settled.Add(d.Bottom);
            for (int k = 0; k < 4; k++)
            {
                Battle.Tick();
                yield return null;
                var now = DrawnNumbers();
                Assert.AreEqual(settled.Count, now.Count);
                foreach (Drawn d in now) Assert.IsTrue(settled.Exists(s => Vector2.Distance(s, d.Bottom) < PositionTolerance), "they do not move");
            }

            // The camera changes: the numbers are at their place on the first frame, over the same board point.
            _camera.aspect = 2.5f;
            yield return null;
            MatchDrawn(model.Frame(BoardDrawn()), DrawnNumbers(), "the first frame after the aspect changed");
            Vector2 newStand = PanelOfCell(kill.X, kill.Y, 0.6f);
            Assert.Greater(Vector2.Distance(stand, newStand), 5f, "the aspect moved the board point");
            Assert.IsTrue(DrawnNumbers().Exists(d => Vector2.Distance(d.Bottom, newStand) < PositionTolerance), "the newest stands over the point under the new camera");

            int guard2 = 0;
            while (DrawnNumbers().Count > 0 && guard2++ < 60)
            {
                Battle.Tick();
                yield return null;
                MatchDrawn(model.Frame(BoardDrawn()), DrawnNumbers(), "ending");
            }
            Assert.AreEqual(0, DrawnNumbers().Count, "they ended on their own ticks");
            Assert.LessOrEqual(Battle.TickCount, killTick + 40);
            _camera.ResetAspect();
        }

        [UnityTest]
        public IEnumerator S4d_AC15_A_Number_Of_An_Enemy_That_Exits_Stays_Where_The_Enemy_View_Last_Stood()
        {
            Enemy enemy = SpawnWave1Enemy(health: 1_000_000f, speed: 20f);
            yield return null;
            ReferenceStack model = NewStack();
            int guard = 0;
            while (enemy.DistanceToExit > 3f && guard++ < 400)
            {
                Battle.Tick();
                yield return null;
            }
            Hit hit = HitOn(enemy, 20f, HitKind.Strong);
            NumberView.Show(hit);
            model.Show(hit);
            Vector3 lastStood = default;
            guard = 0;
            while (OnBoard(enemy) && guard++ < 50)
            {
                yield return null;
                MatchDrawn(model.Frame(BoardDrawn()), DrawnNumbers(), "while it walks");
                if (OnBoard(enemy)) lastStood = DrawnWorld(enemy);
                Battle.Tick();
            }
            Assert.IsFalse(OnBoard(enemy), "it exited");
            yield return null;
            Vector2 expected = PanelOf(lastStood + new Vector3(0f, 0f, 0.6f * _map.CellSize));
            List<Drawn> drawn = DrawnNumbers();
            Assert.AreEqual(1, drawn.Count);
            Assert.AreEqual(expected.x, drawn[0].Bottom.x, PositionTolerance, "x where the enemy view last stood");
            Assert.AreEqual(expected.y, drawn[0].Bottom.y, PositionTolerance, "y where the enemy view last stood");
            Vector2 stays = drawn[0].Bottom;
            Battle.Tick();
            yield return null;
            Assert.AreEqual(stays.x, DrawnNumbers()[0].Bottom.x, PositionTolerance);
            Assert.AreEqual(stays.y, DrawnNumbers()[0].Bottom.y, PositionTolerance);
        }

        [UnityTest]
        public IEnumerator S4d_AC15_The_Next_Enemy_On_A_Reused_Object_Carries_None_Of_The_Numbers_Of_The_One_Before()
        {
            Enemy first = SpawnWave1Enemy(health: 1_000_000f);
            for (int i = 0; i < 10; i++) Battle.Tick();
            yield return null;
            ReferenceStack model = NewStack();
            Hit hit = HitOn(first, 20f, HitKind.Strong);
            NumberView.Show(hit);
            model.Show(hit);
            yield return null;
            MatchDrawn(model.Frame(BoardDrawn()), DrawnNumbers(), "while it is on the board");
            Board.RemoveEnemy(first);
            yield return null;
            MatchDrawn(model.Frame(BoardDrawn()), DrawnNumbers(), "after it left");
            Vector2 stays = DrawnNumbers()[0].Bottom;

            Enemy second = SpawnWave1Enemy(health: 1_000_000f);
            Assert.AreSame(first, second, "the pool hands the same object out");
            Assert.AreNotEqual(first.Entry - 1, second.Entry);
            Battle.Tick();
            yield return null;
            MatchDrawn(model.Frame(BoardDrawn()), DrawnNumbers(), "after the next enemy entered");
            Assert.AreEqual(stays.x, DrawnNumbers()[0].Bottom.x, PositionTolerance, "the old number has not moved");
            Assert.AreEqual(stays.y, DrawnNumbers()[0].Bottom.y, PositionTolerance);

            Hit second1 = HitOn(second, 22f, HitKind.Strong);
            NumberView.Show(second1);
            model.Show(second1);
            yield return null;
            var pairs = MatchDrawn(model.Frame(BoardDrawn()), DrawnNumbers(), "a record naming the new enemy");
            Assert.AreEqual(2, pairs.Count);
            var oldNumber = pairs.Find(p => p.drawn.Text == "20");
            var newNumber = pairs.Find(p => p.drawn.Text == "22");
            Assert.AreEqual(stays.y, oldNumber.drawn.Bottom.y, PositionTolerance, "lifted by nothing");
            Assert.AreEqual(Slot0Over(second).x, newNumber.drawn.Bottom.x, PositionTolerance);
            Assert.AreEqual(Slot0Over(second).y, newNumber.drawn.Bottom.y, PositionTolerance, "at slot 0 over the new enemy");
        }

        // ---- 4d-AC16 the top line

        [UnityTest]
        public IEnumerator S4d_AC16_On_The_Top_Row_Three_Numbers_Stand_With_Their_Tops_On_The_Limit_0_1_And_2_Steps_To_The_Right()
        {
            yield return null;
            ReferenceStack model = NewStack();
            float topRowY = CellYAtPanelY(7, 52.96f + 0.6f * CellPanelHeight); // the direction's top row at 960 x 540: slot 0 hangs with its bottom at 52.96, whatever this window makes of the cell
            Vector2 cell = PanelOfCell(7, topRowY, 0.6f);
            yield return Drive(model, 40, k => k == 0 || k == 6 || k == 12 ? new List<Hit> { HitAtCell(7, topRowY, 20 + k, HitKind.Strong, OffBoard + 8) } : null, (k, pairs) =>
            {
                foreach (var pair in pairs)
                    Assert.GreaterOrEqual(pair.drawn.Bottom.y - 23f, 47f - 0.01f, $"tick {k}: no top above the limit during the pushes");
                if (k >= 12 + 5 && k < 25)
                {
                    Assert.AreEqual(3, pairs.Count);
                    foreach (var pair in pairs)
                    {
                        int steps = 2 - pair.expected.Order; // the newest has order 2
                        Assert.AreEqual(47f + 23f, pair.drawn.Bottom.y, PositionTolerance, "top on the limit");
                        Assert.AreEqual(cell.x + DirectionLineStep * steps, pair.drawn.Bottom.x, PositionTolerance, $"number {pair.expected.Order}: {steps} steps to the right");
                    }
                }
            });
        }

        [UnityTest]
        public IEnumerator S4d_AC16_On_The_Second_Row_Only_Slot_2_Is_On_The_Line_Two_Steps_To_The_Right_And_The_Fourth_Record_Makes_The_First_Leave_Along_The_Line()
        {
            yield return null;
            ReferenceStack model = NewStack();
            float secondRowY = CellYAtPanelY(7, 97.7f + 0.6f * CellPanelHeight);
            Vector2 cell = PanelOfCell(7, secondRowY, 0.6f);
            Assert.GreaterOrEqual(cell.y - 23f - 23f, 47f, "slot 1 of a second-row cell has its top below the line");
            Assert.Less(cell.y - 46f - 23f, 47f, "and slot 2 would have its top above it");
            var leavingXs = new List<float>();
            yield return Drive(model, 30, k => k == 0 || k == 6 || k == 12 || k == 18 ? new List<Hit> { HitAtCell(7, secondRowY, 20 + k, HitKind.Strong, OffBoard + 7) } : null, (k, pairs) =>
            {
                foreach (var pair in pairs) Assert.GreaterOrEqual(pair.drawn.Bottom.y - 23f, 47f - 0.01f, $"tick {k}");
                if (k >= 12 + 5 && k < 18)
                {
                    Assert.AreEqual(3, pairs.Count);
                    foreach (var pair in pairs)
                    {
                        int slot = 2 - pair.expected.Order;
                        if (slot < 2)
                        {
                            Assert.IsFalse(pair.expected.OnLine, $"slot {slot} is in the column");
                            Assert.AreEqual(cell.x, pair.drawn.Bottom.x, PositionTolerance);
                            Assert.AreEqual(cell.y - 23f * slot, pair.drawn.Bottom.y, PositionTolerance);
                        }
                        else
                        {
                            Assert.IsTrue(pair.expected.OnLine, "slot 2 is on the line");
                            Assert.AreEqual(47f + 23f, pair.drawn.Bottom.y, PositionTolerance);
                            Assert.AreEqual(cell.x + 2f * DirectionLineStep, pair.drawn.Bottom.x, PositionTolerance, "two steps to the right");
                        }
                    }
                }
                var first = pairs.Find(p => p.expected.Order == 0);
                if (k >= 18 && first.expected != null)
                {
                    Assert.AreEqual(47f + 23f, first.drawn.Bottom.y, PositionTolerance, "it leaves along the line");
                    leavingXs.Add(first.drawn.Bottom.x);
                }
            });
            Assert.Greater(leavingXs.Count, 2);
            Assert.Greater(leavingXs[leavingXs.Count - 1], leavingXs[0], "moving right along the line while it leaves");
            Assert.AreEqual(cell.x + 3f * DirectionLineStep, leavingXs[leavingXs.Count - 1], DirectionLineStep * 0.3f, "towards the third step");
        }

        // ---- 4d-AC18 measured readability

        [UnityTest]
        public IEnumerator S4d_AC18_a_Numbers_Of_One_Walking_Enemy_Off_The_Top_Line_Never_Overlap_Even_With_Six_Towers_Landing([Values(0f, 1.7778f)] float aspect)
        {
            if (aspect > 0f) _camera.aspect = aspect;
            Enemy enemy = SpawnWave1Enemy(health: 1_000_000f);
            yield return null;
            ReferenceStack model = NewStack();
            var offsets = new[] { 0, 0, 1, 3, 7, 12 };
            int framesWithNumbers = 0, framesOnLine = 0, shown = 0;
            int ticks = 1100;
            yield return Drive(model, ticks, k =>
            {
                if (!OnBoard(enemy)) return null;
                var hits = new List<Hit>();
                foreach (int offset in offsets) if (k % 40 == offset) hits.Add(HitOn(enemy, 20f, HitKind.Strong));
                shown += hits.Count;
                return hits;
            }, (k, pairs) =>
            {
                if (pairs.Count > 0) framesWithNumbers++;
                if (pairs.Exists(p => p.expected.OnLine)) framesOnLine++;
                AssertNoOverlap(pairs, $"tick {k}", exemptTopLine: true);
            });
            Debug.Log($"S4d_AC18 (a) aspect {aspect}: {shown} records, {framesWithNumbers} frames with numbers, {framesOnLine} with a number on the top line, no overlap off the line");
            Assert.Greater(shown, 100);
            if (aspect > 0f) Assert.Greater(framesOnLine, 0, "at the direction's 16:9 the walk passes the second row, where the last slot reaches the line");
            _camera.ResetAspect();
        }

        [UnityTest]
        public IEnumerator S4d_AC18_b_Five_Towers_On_A_Top_Row_Enemy_Overlap_On_At_Most_20_Percent_Of_The_Frames_With_A_Number()
        {
            yield return null;
            ReferenceStack model = NewStack();
            int frames = 0, overlapping = 0;
            float topRowY = CellYAtPanelY(7, 52.96f + 0.6f * CellPanelHeight);
            var offsets = new[] { 0, 8, 16, 24, 32 };
            yield return Drive(model, 480, k =>
            {
                foreach (int offset in offsets) if (k % 40 == offset) return new List<Hit> { HitAtCell(7, topRowY, 20f, HitKind.Strong, OffBoard + 8) };
                return null;
            }, (k, pairs) =>
            {
                if (pairs.Count == 0) return;
                frames++;
                bool overlap = false;
                for (int i = 0; i < pairs.Count && !overlap; i++)
                    for (int j = i + 1; j < pairs.Count; j++)
                        if (Overlaps(pairs[i].drawn.Rect, pairs[j].drawn.Rect)) { overlap = true; break; }
                if (overlap) overlapping++;
            });
            float share = (float)overlapping / frames;
            Debug.Log($"S4d_AC18 (b): {overlapping} of {frames} frames with a number overlap = {share:P1} (target at most 20 %)");
            TestContext.Out.WriteLine($"S4d_AC18 (b): {overlapping} of {frames} frames = {share:P2}");
            Assert.Greater(frames, 200);
            Assert.LessOrEqual(share, 0.20f, $"{overlapping} of {frames} frames");
        }

        [UnityTest]
        public IEnumerator S4d_AC18_c_In_The_Last_Wave_Of_The_Reference_Order_Numbers_Of_Different_Enemies_Overlap_On_At_Most_10_Percent_Of_The_Frames()
        {
            const float aspect = 0f; // the game view: the reference clicks need the whole board on screen
            yield return null;
            ReferenceStack model = NewStack();
            int ticks = 0;
            while (Battle.Waves.StartedWaves < Battle.Waves.WaveCount && ticks < 12000)
            {
                for (int i = 0; i < 30 && Battle.Waves.StartedWaves < Battle.Waves.WaveCount; i++, ticks++)
                {
                    RunReferenceActionsAt(ticks);
                    Battle.Tick();
                }
                yield return null;
                model.Frame(BoardDrawn()); // keeps the model's reading of the log in step
            }
            Assert.AreEqual(Battle.Waves.WaveCount, Battle.Waves.StartedWaves, $"the last wave has started (outcome {Battle.Outcome}, lives {Battle.Lives}, tick {Battle.TickCount}, towers {Board.TowerCount})");

            string firstMismatch = null;
            int frames = 0, different = 0, sameOffLine = 0, sameOnLine = 0, mismatches = 0, maxAlive = 0;
            while (Battle.Outcome == RunOutcome.Running && ticks < 14000)
            {
                RunReferenceActionsAt(ticks);
                Battle.Tick();
                ticks++;
                yield return null;
                if (Battle.Outcome != RunOutcome.Running) break; // under the end screen nothing shows (4d-AC11)
                var expected = model.Frame(BoardDrawn());
                var pairs = MatchDrawn(expected, DrawnNumbers(), "last wave", failOnMismatch: false);
                if (pairs.Count != expected.Count) { if (mismatches++ == 0) firstMismatch = $"tick {ticks}: {LastMismatch}"; }
                if (pairs.Count == 0) continue;
                frames++;
                maxAlive = Mathf.Max(maxAlive, pairs.Count);
                bool d = false, so = false, sl = false;
                for (int i = 0; i < pairs.Count; i++)
                    for (int j = i + 1; j < pairs.Count; j++)
                    {
                        if (!Overlaps(pairs[i].drawn.Rect, pairs[j].drawn.Rect)) continue;
                        if (pairs[i].expected.Entry != pairs[j].expected.Entry) d = true;
                        else if (pairs[i].expected.OnLine || pairs[j].expected.OnLine) sl = true;
                        else so = true;
                    }
                if (d) different++;
                if (so) sameOffLine++;
                if (sl) sameOnLine++;
            }
            Assert.AreEqual(RunOutcome.Won, Battle.Outcome, "the reference order wins");
            string report = $"S4d_AC18 (c) aspect {aspect}: {frames} frames with a number; different enemies overlap on {different} ({(float)different / frames:P2}, target at most 10 %), same enemy off the top line on {sameOffLine} ({(float)sameOffLine / frames:P2}, target 0), same enemy on the top line on {sameOnLine} ({(float)sameOnLine / frames:P2}); up to {maxAlive} numbers at once; {mismatches} frames differed from the model";
            Debug.Log(report);
            TestContext.Out.WriteLine(report);
            Assert.Greater(frames, 300);
            Assert.LessOrEqual((float)different / frames, 0.10f, report);
            Assert.AreEqual(0, sameOffLine, "numbers of one enemy never overlap off the top line: " + report);
            _camera.ResetAspect();
            Assert.AreEqual(0, mismatches, "the drawn frames equal the model's on every frame of the last wave; first: " + firstMismatch);
        }

        // ---- 4d-AC13 cost

        [UnityTest]
        public IEnumerator S4d_AC13_Both_Views_Allocate_Nothing_With_40_Numbers_Alive_And_Words_On_The_Enemies_Over_120_Ticks_Of_Real_Landings()
        {
            Element wave = Battle.Waves.WaveAt(0).GroupAt(0).Enemy.Element;
            Element other = OtherElement(wave);
            BuyFree(2, 3, other);
            BuyFree(2, 5, wave);
            BuyFree(3, 3, wave);
            BuyFree(3, 5, other);
            yield return null;
            int entry = OffBoard;
            void TopUp()
            {
                for (int i = 0; i < 2; i++)
                {
                    int e = entry++;
                    NumberView.Show(HitAtCell(1 + e % 14, 2 + e % 5, 20f, HitKind.Strong, e));
                }
            }

            // Warm-up: the amount 20 is seen, the enemies and projectiles reach their peak, the words exist.
            for (int k = 0; k < 400; k++)
            {
                if (k % 10 == 0) SpawnWave1Enemy(health: 1_000_000f, speed: 0.6f);
                Battle.Tick();
                TopUp();
                if (k % 4 == 0) yield return null;
            }
            yield return null;
            Assert.GreaterOrEqual(DrawnWords().Count, 1, "words stand on marked enemies");

            Action numbersFrame = FrameMethod(NumberView);
            Action refusalFrame = FrameMethod(Object.FindAnyObjectByType<RefusalView>());
            numbersFrame();
            refusalFrame();
            int maxNumbers = 0, maxWords = 0, hitsBefore = Battle.HitCount;
            for (int k = 0; k < 120; k++)
            {
                if (k % 30 == 0) ClickCell(5, 0); // a refusal in flight for the second view
                Battle.Tick();
                TopUp();
                int count = Allocations.Of(numbersFrame);
                Assert.AreEqual(0, count, $"DamageNumberView.LateUpdate allocated on tick {k}");
                count = Allocations.Of(refusalFrame);
                Assert.AreEqual(0, count, $"RefusalView.LateUpdate allocated on tick {k}");
                yield return null;
                maxNumbers = Mathf.Max(maxNumbers, VisibleNumbers());
                maxWords = Mathf.Max(maxWords, DrawnWords().Count);
            }
            Assert.Greater(Battle.HitCount, hitsBefore, "real landings happened during the measurement");
            Assert.GreaterOrEqual(maxNumbers, 36, "about 40 numbers alive");
            Assert.Greater(maxWords, 0);
            AssertPoolIntact();
        }

        [UnityTest]
        public IEnumerator S4d_AC13_On_Frames_With_No_New_Hit_No_Label_Changes_Layout_Gets_A_Geometry_Event_Or_Is_Written_A_New_Text()
        {
            Enemy enemy = SpawnWave1Enemy(health: 1_000_000f, speed: 0.6f);
            Enemy marked = SpawnWave1Enemy(health: 1_000_000f, speed: 0.6f);
            yield return null;
            for (int i = 0; i < 6; i++) NumberView.Show(HitAtCell(2 + i, 3, 12 + i * 3, i % 2 == 0 ? HitKind.Strong : HitKind.Normal, OffBoard + i));
            NumberView.Show(HitOn(enemy, 20f, HitKind.Strong));
            NumberView.Show(HitOn(enemy, 20f, HitKind.Strong));
            yield return Frames(3);

            var labels = new List<VisualElement>();
            Root.Q<VisualElement>("floating-texts").Query<VisualElement>().ForEach(labels.Add);
            Assert.Greater(labels.Count, 80, "anchors and labels of both pools and the refusal");
            int geometryEvents = 0;
            foreach (VisualElement element in labels) element.RegisterCallback<GeometryChangedEvent>(_ => geometryEvents++);
            var layouts = labels.ConvertAll(e => e.layout);
            var texts = new List<string>();
            foreach (VisualElement element in labels) texts.Add((element as Label)?.text);

            for (int k = 0; k < 24; k++)
            {
                Battle.Tick();
                yield return null;
            }
            Assert.Greater(DrawnNumbers().Count, 0, "numbers were moving and fading during the frames");
            Assert.AreEqual(0, geometryEvents, "no label under floating-texts got a GeometryChangedEvent");
            for (int i = 0; i < labels.Count; i++)
            {
                Assert.AreEqual(layouts[i], labels[i].layout, $"layout of element {i} ({labels[i].name})");
                if (labels[i] is Label label) Assert.AreSame(texts[i], label.text, $"text of element {i}: the same string object");
            }
            Assert.IsNotNull(marked);
        }
    }
}
