using UnityEngine;
using UnityEngine.UI;

// Optional binding for the eventual game layout. Several targets may share a bank.
[DefaultExecutionOrder(100)]
public sealed class StageSpriteTarget : MonoBehaviour {
    public StageTimeline timeline;
    public string targetName = "角色";
    public SpriteRenderer spriteRenderer;
    public Image image;
    void LateUpdate() {
        if(timeline==null) timeline=FindObjectOfType<StageTimeline>();
        if(timeline==null) return;
        Sprite sprite=null;
        if(timeline.PresentationVisible) foreach(var output in timeline.Animations) if(output.Clip.target==targetName && output.Frame>=0) { sprite=output.Sprite; break; }
        if(spriteRenderer!=null) { spriteRenderer.sprite=sprite; spriteRenderer.enabled=sprite!=null; }
        if(image!=null) { image.sprite=sprite; image.enabled=sprite!=null; }
    }
}
