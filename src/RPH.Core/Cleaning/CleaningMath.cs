using System;

namespace RPH.Core.Cleaning
{
    /// <summary>Rules of the floor-based cleaning service. Inputs come from the game's rooms and the XML tweakables.</summary>
    public static class CleaningMath
    {
        /// <summary>
        /// Whether a janitor cleans a room. His own floor, whatever the department. A floor where no janitor of his shift is
        /// based (nobody would ever clean it) is left to the janitors of the room's own department, as in the game.
        /// </summary>
        public static bool IsInZone(int roomFloor, int homeFloor, bool roomFloorHasJanitor, bool sameDepartment, bool coverFloorsWithoutJanitor)
        {
            if (roomFloor == homeFloor)
            {
                return true;
            }
            return coverFloorsWithoutJanitor && !roomFloorHasJanitor && sameDepartment;
        }

        /// <summary>
        /// Distance in tiles (diagonals count as one) from a tile to a rectangle of tiles; 0 inside or on its border.
        /// </summary>
        public static int DistanceToRange(int x, int y, int minX, int minY, int maxX, int maxY)
        {
            int dx = Math.Max(Math.Max(minX - x, 0), x - maxX);
            int dy = Math.Max(Math.Max(minY - y, 0), y - maxY);
            return Math.Max(dx, dy);
        }

        /// <summary>The janitor stops outside a busy room, close to it: at most <paramref name="frontDistance"/> tiles, not inside.</summary>
        public static bool IsInFrontOfRoom(int distanceToRoom, bool insideRoom, int frontDistance)
        {
            return !insideRoom && distanceToRoom <= frontDistance;
        }

        /// <summary>The janitor has waited long enough in front of a busy room (game seconds against a limit in game minutes).</summary>
        public static bool WaitExpired(float waitedGameSeconds, float limitGameMinutes, float gameSecondsPerGameHour)
        {
            return waitedGameSeconds >= limitGameMinutes / 60f * gameSecondsPerGameHour;
        }
    }
}
