using System.Collections.Generic;
using ElementalTD.Core;

namespace ElementalTD.Core.Tests
{
    /// <summary>The prototype layout, built in code (same cells as Map_Prototype.asset).</summary>
    internal static class TestMaps
    {
        public static readonly GridPos Spawn = new GridPos(0, 4);
        public static readonly GridPos Exit = new GridPos(15, 4);

        public static List<GridPos> PrototypeBlocked()
        {
            var blocked = new List<GridPos>();
            for (int y = 0; y <= 6; y++) blocked.Add(new GridPos(5, y));
            for (int y = 2; y <= 8; y++) blocked.Add(new GridPos(10, y));
            return blocked;
        }

        public static FlowField PrototypeField(out GridMap map)
        {
            map = new GridMap(16, 9, PrototypeBlocked());
            var field = new FlowField(map);
            field.Compute(Exit);
            return field;
        }

        public static FlowField OpenField(int width, int height, GridPos exit, out GridMap map)
        {
            map = new GridMap(width, height, new GridPos[0]);
            var field = new FlowField(map);
            field.Compute(exit);
            return field;
        }
    }
}
