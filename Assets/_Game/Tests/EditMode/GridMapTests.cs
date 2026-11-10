using System;
using NUnit.Framework;

namespace ElementalTD.Core.Tests
{
    public class GridMapTests
    {
        [TestCase(0, 5)]
        [TestCase(5, 0)]
        [TestCase(-1, -1)]
        public void AC3_GridMap_Rejects_Size_Below_One(int width, int height)
        {
            Assert.Throws<ArgumentException>(() => new GridMap(width, height, new GridPos[0]));
        }

        [Test]
        public void AC3_GridMap_Rejects_Blocked_Cell_OutOfBounds()
        {
            Assert.Throws<ArgumentException>(() => new GridMap(4, 4, new[] { new GridPos(4, 0) }));
            Assert.Throws<ArgumentException>(() => new GridMap(4, 4, new[] { new GridPos(0, -1) }));
        }

        [Test]
        public void AC3_GridMap_InBounds_IsBlocked_And_Null_Blocked_List()
        {
            var map = new GridMap(4, 3, new[] { new GridPos(1, 1) });
            Assert.IsTrue(map.InBounds(new GridPos(3, 2)));
            Assert.IsFalse(map.InBounds(new GridPos(4, 2)));
            Assert.IsFalse(map.InBounds(new GridPos(-1, 0)));
            Assert.IsTrue(map.IsBlocked(new GridPos(1, 1)));
            Assert.IsFalse(map.IsBlocked(new GridPos(0, 0)));
            Assert.Throws<ArgumentOutOfRangeException>(() => map.IsBlocked(new GridPos(9, 9)));
            Assert.DoesNotThrow(() => new GridMap(2, 2, null));
        }
    }
}
