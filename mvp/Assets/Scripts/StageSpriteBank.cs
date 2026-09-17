using System;
using UnityEngine;

// Source-format-independent binding. The eventual Aseprite importer only needs
// to supply an ordered Sprite array; timing never depends on atlas layout/FPS.
[Serializable] public sealed class StageSpriteSequence {
    public string id = "";
    public Sprite[] frames = new Sprite[0];
}
[CreateAssetMenu(menuName="音游/可复用动画素材库",fileName="StageSpriteBank")]
public sealed class StageSpriteBank : ScriptableObject {
    public StageSpriteSequence[] sequences = new StageSpriteSequence[0];
    public Sprite[] Find(string id) {
        if(String.IsNullOrEmpty(id)) return null;
        foreach(var sequence in sequences) if(sequence!=null && sequence.id==id) return sequence.frames;
        return null;
    }
    public void Validate() {
        var ids=new System.Collections.Generic.HashSet<string>();
        if(sequences==null) throw new FormatException("动画素材库列表为空。");
        foreach(var sequence in sequences) {
            if(sequence==null||String.IsNullOrWhiteSpace(sequence.id)||!ids.Add(sequence.id)) throw new FormatException("动画素材名称不能为空或重复。");
            if(sequence.frames==null||sequence.frames.Length==0) throw new FormatException("动画“"+sequence.id+"”缺少帧。");
            foreach(var frame in sequence.frames) if(frame==null) throw new FormatException("动画“"+sequence.id+"”包含未指定的帧。");
        }
    }
}
