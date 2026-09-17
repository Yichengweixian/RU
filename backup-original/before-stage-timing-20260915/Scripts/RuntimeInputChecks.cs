#if UNITY_EDITOR || DEVELOPMENT_BUILD
using System;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;

public static class RuntimeInputChecks {
    public static string Run() {
        var previousBackground = InputSystem.settings.backgroundBehavior;
        InputSystem.settings.backgroundBehavior = InputSettings.BackgroundBehavior.IgnoreFocus;
        var go = new GameObject("Input regression fixture");
        var keyboard = InputSystem.AddDevice<Keyboard>();
        try {
            var input = go.AddComponent<RhythmInput>(); input.Initialize();
            int edges=0; double first=0,last=0; bool firstDown=false;
            double now=Time.realtimeSinceStartupAsDouble;
            InputSystem.QueueStateEvent(keyboard,new KeyboardState(Key.A),now-.02);
            InputSystem.QueueStateEvent(keyboard,new KeyboardState(),now-.01);
            InputSystem.Update();
            input.Drain(e => { if(edges==0) { first=e.Time; firstDown=e.Down; } last=e.Time; edges++; });
            if(edges!=2 || !firstDown || input.Held[0] || Math.Abs(last-first-.01)>.00001)
                throw new Exception("Input edge regression: edges="+edges+" interval="+(last-first));
            input.Clear(); input.Drain(e=>edges++);
            if(edges!=2) throw new Exception("Stale input was replayed.");
            return "PASS: same-frame press/release, original 10 ms timestamps, stale queue clear.";
        } finally { UnityEngine.Object.DestroyImmediate(go); InputSystem.RemoveDevice(keyboard); InputSystem.settings.backgroundBehavior = previousBackground; }
    }
}
#endif
