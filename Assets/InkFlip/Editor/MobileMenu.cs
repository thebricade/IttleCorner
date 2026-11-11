using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace InkFlip.EditorTools
{
    // Menu items for trying the game on a phone.
    public static class MobileMenu
    {
        const string ToggleMenu = "Tools/InkFlip/Mobile Controls In Editor";
        const string ScenePath = "Assets/InkFlip/Scenes/PaintTestRoom.unity";

        // Show the touch controls while playing in the editor. Use it with the Device Simulator window
        // (Window > General > Device Simulator), where the mouse acts as a finger.
        [MenuItem(ToggleMenu)]
        static void ToggleEditorControls()
        {
            bool on = !EditorPrefs.GetBool(MobileControls.EditorPrefKey, false);
            EditorPrefs.SetBool(MobileControls.EditorPrefKey, on);
            Debug.Log("InkFlip: touch controls in the editor are " + (on ? "ON (use the Device Simulator; the keyboard and mouse still work too)" : "OFF"));
        }

        [MenuItem(ToggleMenu, true)]
        static bool ToggleEditorControlsValidate()
        {
            Menu.SetChecked(ToggleMenu, EditorPrefs.GetBool(MobileControls.EditorPrefKey, false));
            return true;
        }

        // Sets up what an iPhone build needs from this project: landscape, and the game scene in the build.
        [MenuItem("Tools/InkFlip/Prepare For iPhone")]
        static void PrepareForIPhone()
        {
            // landscape only
            PlayerSettings.defaultInterfaceOrientation = UIOrientation.AutoRotation;
            PlayerSettings.allowedAutorotateToPortrait = false;
            PlayerSettings.allowedAutorotateToPortraitUpsideDown = false;
            PlayerSettings.allowedAutorotateToLandscapeLeft = true;
            PlayerSettings.allowedAutorotateToLandscapeRight = true;

            // the game scene goes first in the build
            var scenes = new List<EditorBuildSettingsScene>(EditorBuildSettings.scenes);
            scenes.RemoveAll(s => s.path == ScenePath);
            scenes.Insert(0, new EditorBuildSettingsScene(ScenePath, true));
            EditorBuildSettings.scenes = scenes.ToArray();

            Debug.Log("InkFlip: ready for iPhone. Landscape is set and " + ScenePath + " is the first scene in the build.\n" +
                      "Next: File > Build Profiles > iOS > Switch Platform > Build, then open the Xcode project, pick your team under Signing, and run on your phone.");
        }
    }
}
