using UnityEngine;

public static class Bootstrap {
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    static void AutoSetup() {
        if (Object.FindObjectOfType<GameManager>() != null) return;
        Camera cam = Camera.main;
        if (cam == null) {
            var cameraObject = new GameObject("Main Camera", typeof(Camera), typeof(AudioListener));
            cameraObject.tag = "MainCamera";
            cam = cameraObject.GetComponent<Camera>();
        }
        cam.clearFlags = CameraClearFlags.SolidColor;
        cam.backgroundColor = Color.black;
        var game = new GameObject("Rhythm Game");
        game.AddComponent<AudioSource>().playOnAwake = false;
        game.AddComponent<GameManager>();
    }
}
