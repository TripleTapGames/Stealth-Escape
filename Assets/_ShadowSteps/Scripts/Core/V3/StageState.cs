using System;
using System.Collections.Generic;
using System.Text;
using ShadowSteps.Core;

namespace ShadowSteps.V3
{
    public enum StageResult { Playing, Won, Caught, Lockdown }

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

        public StageState(GridCoord player, int t, bool hasKey, int openDoors, IReadOnlyList<SmokeCloud> clouds, IReadOnlyList<EmpEffect> emps,
            int smokeCharges, int empCharges, int alarm, bool alarmEver, StageResult result, string cause, int waitsUsed = 0)
        {
            Player = player; T = t; HasKey = hasKey; OpenDoors = openDoors; WaitsUsed = waitsUsed;
            Clouds = clouds ?? Array.Empty<SmokeCloud>(); Emps = emps ?? Array.Empty<EmpEffect>();
            SmokeCharges = smokeCharges; EmpCharges = empCharges; Alarm = alarm; AlarmEver = alarmEver; Result = result; Cause = cause ?? string.Empty;
        }

        public static StageState Initial(Stage stage) =>
            new StageState(stage.Start, 0, false, 0, null, null, stage.SmokeCharges, stage.EmpCharges, -1, false, StageResult.Playing, null);

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
            " clouds=" + Clouds.Count + " emps=" + Emps.Count + " alarm=" + Alarm + (AlarmEver ? "!" : "") + " " + Result + (Cause.Length > 0 ? " (" + Cause + ")" : "");
    }
}
