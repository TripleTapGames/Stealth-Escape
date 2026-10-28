using System;
using System.Collections.Generic;

namespace ShadowSteps.Core
{
    /// <summary>The seven player actions. Every valid action spends exactly one turn.</summary>
    public enum GameAction
    {
        North = 0,
        East = 1,
        South = 2,
        West = 3,
        Wait = 4,
        Smoke = 5,
        Emp = 6
    }

    public static class GameActionUtil
    {
        /// <summary>Order used by the reference breadth-first solver (ACTIONS list in build.py). Keep this order for solver parity.</summary>
        public static readonly IReadOnlyList<GameAction> ReferenceOrder = new[]
        {
            GameAction.North, GameAction.East, GameAction.South, GameAction.West,
            GameAction.Wait, GameAction.Smoke, GameAction.Emp
        };

        public static bool IsMove(GameAction a) => (int)a >= 0 && (int)a <= 3;

        public static bool IsGadget(GameAction a) => a == GameAction.Smoke || a == GameAction.Emp;

        public static Direction ToDirection(GameAction a)
        {
            if (!IsMove(a)) throw new ArgumentException("Action is not a move: " + a);
            return (Direction)(int)a;
        }

        public static GameAction FromDirection(Direction d) => (GameAction)(int)d;

        /// <summary>Token used by reference/validation.json: N, E, S, W, WAIT, SMOKE, EMP.</summary>
        public static string ToToken(GameAction a)
        {
            switch (a)
            {
                case GameAction.North: return "N";
                case GameAction.East: return "E";
                case GameAction.South: return "S";
                case GameAction.West: return "W";
                case GameAction.Wait: return "WAIT";
                case GameAction.Smoke: return "SMOKE";
                case GameAction.Emp: return "EMP";
                default: throw new ArgumentOutOfRangeException(nameof(a));
            }
        }

        public static bool TryParse(string token, out GameAction action)
        {
            switch ((token ?? string.Empty).Trim().ToUpperInvariant())
            {
                case "N": action = GameAction.North; return true;
                case "E": action = GameAction.East; return true;
                case "S": action = GameAction.South; return true;
                case "W": action = GameAction.West; return true;
                case "WAIT": action = GameAction.Wait; return true;
                case "SMOKE": action = GameAction.Smoke; return true;
                case "EMP": action = GameAction.Emp; return true;
                default: action = GameAction.Wait; return false;
            }
        }

        public static GameAction Parse(string token)
        {
            if (!TryParse(token, out var a)) throw new FormatException("Unknown action token '" + token + "'");
            return a;
        }

        public static List<GameAction> ParseSequence(IEnumerable<string> tokens)
        {
            var list = new List<GameAction>();
            foreach (var t in tokens) list.Add(Parse(t));
            return list;
        }

        public static string ToTokenString(IEnumerable<GameAction> actions) =>
            string.Join(" ", System.Linq.Enumerable.Select(actions, ToToken));
    }
}
