using System;
using System.Reflection;
using UnityEditor;
using UnityEngine;

// Editor-only adapter. Runtime playback continues to use the public DSP clock.
// Probe the installed Unity version; unavailable preview audio never blocks scrubbing.
public static class StageEditorAudio {
    static readonly Type util=typeof(AudioImporter).Assembly.GetType("UnityEditor.AudioUtil");
    static MethodInfo Method(string name, params Type[] args) {
        return util==null?null:util.GetMethod(name,BindingFlags.Static|BindingFlags.Public|BindingFlags.NonPublic,null,args,null);
    }
    static readonly MethodInfo play=Method("PlayPreviewClip",typeof(AudioClip),typeof(int),typeof(bool));
    static readonly MethodInfo stop=Method("StopAllPreviewClips");
    static readonly MethodInfo position=Method("GetPreviewClipPosition");
    public static bool Available { get { return play!=null&&stop!=null&&position!=null; } }
    public static bool Play(AudioClip clip, double seconds) {
        if(!Available||clip==null) return false;
        try { Stop(); play.Invoke(null,new object[]{clip,Mathf.Clamp((int)(Math.Max(0,seconds)*clip.frequency),0,Math.Max(0,clip.samples-1)),false}); return true; }
        catch { return false; }
    }
    public static double Position { get { try { return Convert.ToDouble(position.Invoke(null,null)); } catch { return -1; } } }
    public static void Stop() { if(stop!=null) try { stop.Invoke(null,null); } catch { } }
}
