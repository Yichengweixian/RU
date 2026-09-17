using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;

// Preserve both edges of taps received between rendered frames.
public sealed class RhythmInput : MonoBehaviour {
    public struct Edge { public int Lane; public bool Down; public double Time; public long Order; }
    readonly List<Edge> pending = new List<Edge>(32);
    readonly InputAction[] actions = new InputAction[4];
    long order;
    public readonly bool[] Held = new bool[4];
    public double AcceptAfter;
    void Awake() { Initialize(); }
    public void Initialize() {
        if (actions[0] != null) return;
        string[] keys = { "a", "s", "d", "f" };
        for (int i = 0; i < 4; i++) {
            int lane = i;
            actions[i] = new InputAction("Lane" + i, InputActionType.Button, "<Keyboard>/" + keys[i]);
            actions[i].performed += c => Add(lane, true, c.time);
            actions[i].canceled += c => Add(lane, false, c.time);
            actions[i].Enable();
        }
    }
    void Add(int lane, bool down, double time) {
        Held[lane] = down;
        if (time >= AcceptAfter) pending.Add(new Edge { Lane = lane, Down = down, Time = time, Order = order++ });
    }
    static readonly Comparison<Edge> CompareEdges = (a,b) => { int c = a.Time.CompareTo(b.Time); return c != 0 ? c : a.Order.CompareTo(b.Order); };
    public void Drain(Action<Edge> consume) {
        pending.Sort(CompareEdges);
        for (int i = 0; i < pending.Count; i++) consume(pending[i]);
        pending.Clear();
    }
    public void Clear() { pending.Clear(); AcceptAfter = Time.realtimeSinceStartupAsDouble; }
    void OnDestroy() { foreach (var action in actions) if (action != null) action.Dispose(); }
}
