using System;
using System.Collections.Generic;
using System.Text;
using ShadowSteps.Core;

namespace ShadowSteps.V3
{
    public enum StageResult { Playing, Won, Caught, Lockdown }

    /// <summary>
    /// Global rules mode, selectable on every level (D-040). Stealth is the original game: any contact with a live guard is
    /// fatal and knockouts are unavailable. Knockout additionally lets the player step onto an adjacent guard from behind
    /// or either side, defeating it for the rest of the run as one counted action. The mode is part of the state so
    /// previews, the solver, retries and any resumed run carry it with them.
    /// </summary>
    public enum StageMode { Stealth = 0, Knockout = 1 }

    public static class StageModeUtil
    {
        /// <summary>Identity of the knockout rule set; recorded with per-mode results so a later rule change can be told apart.</summary>
        public const string RulesVersion = "knockout-nowait-v3"; // D-044: Knockout, WAIT removed (waitLimit 0 everywhere); "knockout-move-v2" records are kept as legacy
        /// <summary>The shipped game runs Knockout only (user decision 2026-11-04, D-043); Stealth stays available to the engine, tools and tests.</summary>
        public const StageMode Default = StageMode.Knockout;
        public static string Token(StageMode m) => m == StageMode.Knockout ? "knockout" : "stealth";
        public static string Label(StageMode m) => m == StageMode.Knockout ? "Knockout" : "Stealth";
        public static bool TryParse(string token, out StageMode mode)
        {
            mode = StageMode.Stealth;
            if (string.IsNullOrEmpty(token)) return false;
            switch (token.Trim().ToLowerInvariant()) { case "stealth": mode = StageMode.Stealth; return true; case "knockout": mode = StageMode.Knockout; return true; }
            return false;
        }
    }

    /// <summary>A stationary smoke cloud: one node, protected counted actions left.</summary>
    public readonly struct SmokeCloud
    {
        public readonly GridCoord Node;
        public readonly int ActionsLeft;
        public SmokeCloud(GridCoord node, int actionsLeft) { Node = node; ActionsLeft = actionsLeft; }
    }

    /// <summary>A disabled camera: index into <see cref="Stage.Cameras"/>, protected counted actions left.</summary>
    public readonly struct EmpEffect
    {
        public readonly int CameraIndex;
        public readonly int ActionsLeft;
        public EmpEffect(int cameraIndex, int actionsLeft) { CameraIndex = cameraIndex; ActionsLeft = actionsLeft; }
    }

    /// <summary>
    /// Complete logical state of one attempt (handover §11 "solver state"). Immutable: <see cref="StageRules.TryApply"/>
    /// returns a new instance. <see cref="T"/> counts Move/Wait actions only and drives every schedule.
    /// </summary>
    public sealed class StageState
    {
        public const int GadgetDuration = 2;

        public GridCoord Player { get; }
        /// <summary>Counted actions taken (Moves + Waits). Guards/cameras are evaluated at this index.</summary>
        public int T { get; }
        public bool HasKey { get; }
        /// <summary>Bit i set = door i has been opened this attempt.</summary>
        public int OpenDoors { get; }
        public IReadOnlyList<SmokeCloud> Clouds { get; }
        public IReadOnlyList<EmpEffect> Emps { get; }
        public int SmokeCharges { get; }
        public int EmpCharges { get; }
        /// <summary>-1 = no alarm; otherwise counted actions remaining before lockdown.</summary>
        public int Alarm { get; }
        /// <summary>An alarm was triggered at some point in this attempt (forfeits Ghost).</summary>
        public bool AlarmEver { get; }
        public StageResult Result { get; }
        public string Cause { get; }
        /// <summary>WAIT actions taken this attempt (compared with <see cref="Stage.WaitLimit"/>).</summary>
        public int WaitsUsed { get; }
        /// <summary>Rules mode of this attempt (fixed at <see cref="Initial"/>; changing mode restarts the level).</summary>
        public StageMode Mode { get; }
        /// <summary>Bit i set = guard i (index into <see cref="Stage.Guards"/>, stable ids preserved) is knocked out for the rest of the run. Up to <see cref="MaxGuards"/> guards.</summary>
        public ulong Defeated { get; }
        public const int MaxGuards = 64;

        public StageState(GridCoord player, int t, bool hasKey, int openDoors, IReadOnlyList<SmokeCloud> clouds, IReadOnlyList<EmpEffect> emps,
            int smokeCharges, int empCharges, int alarm, bool alarmEver, StageResult result, string cause, int waitsUsed = 0, StageMode mode = StageMode.Stealth, ulong defeated = 0UL)
        {
            Player = player; T = t; HasKey = hasKey; OpenDoors = openDoors; WaitsUsed = waitsUsed; Mode = mode; Defeated = defeated;
            Clouds = clouds ?? Array.Empty<SmokeCloud>(); Emps = emps ?? Array.Empty<EmpEffect>();
            SmokeCharges = smokeCharges; EmpCharges = empCharges; Alarm = alarm; AlarmEver = alarmEver; Result = result; Cause = cause ?? string.Empty;
        }

        public static StageState Initial(Stage stage) => Initial(stage, StageMode.Stealth);

        /// <summary>Authored initial state of the level in the given mode: nobody defeated, every schedule at phase 0.</summary>
        public static StageState Initial(Stage stage, StageMode mode) =>
            new StageState(stage.Start, 0, false, 0, null, null, stage.SmokeCharges, stage.EmpCharges, -1, false, StageResult.Playing, null, 0, mode, 0UL);

        public bool IsDefeated(int guardIndex) => guardIndex >= 0 && guardIndex < MaxGuards && (Defeated & (1UL << guardIndex)) != 0;
        public int DefeatedCount { get { int n = 0; ulong d = Defeated; while (d != 0) { n += (int)(d & 1UL); d >>= 1; } return n; } }
        public bool KnockoutsEnabled => Mode == StageMode.Knockout;

        public bool IsTerminal => Result != StageResult.Playing;
        public bool AlarmActive => Alarm >= 0;
        public bool IsDoorOpen(int doorIndex) => (OpenDoors & (1 << doorIndex)) != 0;

        public bool IsSmoked(GridCoord node)
        {
            foreach (var c in Clouds) if (c.Node == node) return true;
            return false;
        }

        public bool IsCameraDisabled(int cameraIndex)
        {
            foreach (var e in Emps) if (e.CameraIndex == cameraIndex) return true;
            return false;
        }

        /// <summary>Remaining protected actions of the cloud on a node, or 0.</summary>
        public int SmokeLeftAt(GridCoord node)
        {
            foreach (var c in Clouds) if (c.Node == node) return c.ActionsLeft;
            return 0;
        }

        public int EmpLeftOn(int cameraIndex)
        {
            foreach (var e in Emps) if (e.CameraIndex == cameraIndex) return e.ActionsLeft;
            return 0;
        }

        /// <summary>
        /// Canonical identity for search: everything that influences the future. The schedule index is reduced modulo the
        /// stage period when <paramref name="period"/> is given (schedules depend only on T mod period).
        /// </summary>
        /// <summary>WAITs still allowed on this stage (int.MaxValue when unlimited).</summary>
        public int WaitsLeft(Stage stage) => stage.WaitLimit < 0 ? int.MaxValue : Math.Max(0, stage.WaitLimit - WaitsUsed);

        public string Key(int period = 0, bool trackWaits = false)
        {
            var sb = new StringBuilder(64);
            if (trackWaits) sb.Append('w').Append(WaitsUsed).Append('/');
            if (Mode != StageMode.Stealth || Defeated != 0UL) sb.Append('m').Append((int)Mode).Append('d').Append(Defeated).Append('/');
            sb.Append(Player.X).Append(',').Append(Player.Y).Append('/').Append(period > 0 ? T % period : T).Append('/').Append(HasKey ? 1 : 0).Append('/').Append(OpenDoors)
              .Append('/').Append(SmokeCharges).Append('/').Append(EmpCharges).Append('/');
            var clouds = new List<string>();
            foreach (var c in Clouds) clouds.Add(c.Node.X + "," + c.Node.Y + ":" + c.ActionsLeft);
            clouds.Sort(StringComparer.Ordinal);
            sb.Append(string.Join(";", clouds)).Append('/');
            var emps = new List<string>();
            foreach (var e in Emps) emps.Add(e.CameraIndex + ":" + e.ActionsLeft);
            emps.Sort(StringComparer.Ordinal);
            sb.Append(string.Join(";", emps)).Append('/').Append(Alarm).Append('/').Append(AlarmEver ? 1 : 0).Append('/').Append((int)Result);
            return sb.ToString();
        }

        public override string ToString() => "player " + Player + " t=" + T + (HasKey ? " key" : "") + " doors=" + OpenDoors + " smoke=" + SmokeCharges + " emp=" + EmpCharges +
            " clouds=" + Clouds.Count + " emps=" + Emps.Count + " alarm=" + Alarm + (AlarmEver ? "!" : "") + (Mode != StageMode.Stealth ? " " + StageModeUtil.Token(Mode) : "") + (Defeated != 0UL ? " ko=" + Defeated : "") + " " + Result + (Cause.Length > 0 ? " (" + Cause + ")" : "");
    }
}
