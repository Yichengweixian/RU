using System.Collections.Generic;

public sealed class VisibleNotes {
    readonly NoteData[] notes;
    readonly List<int> active = new List<int>(128);
    int next;
    double previous = double.NegativeInfinity;
    public IReadOnlyList<int> Active { get { return active; } }
    public VisibleNotes(NoteData[] notes) { this.notes = notes; }
    public void Update(double time, double lookAhead, NoteState[] states) {
        if (time < previous) { active.Clear(); next = 0; }
        previous = time;
        while (next < notes.Length && notes[next].time <= time + lookAhead) {
            if (notes[next].endTime >= time - .5 && states[next] != NoteState.Done) active.Add(next);
            next++;
        }
        int write = 0;
        for (int i = 0; i < active.Count; i++) {
            int id = active[i];
            if (states[id] != NoteState.Done && notes[id].endTime >= time - .5) active[write++] = id;
        }
        if (write < active.Count) active.RemoveRange(write, active.Count - write);
    }
}
