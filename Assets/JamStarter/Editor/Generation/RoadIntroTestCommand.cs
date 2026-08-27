using UnityEditor;
using UnityEngine;

namespace JamStarter.Editor
{
    internal static class RoadIntroTestCommand
    {
        [MenuItem("Tools/Road of Life/Show Tutorial On Next Play", false, 106)]
        private static void ShowTutorialOnNextPlay()
        {
            PlayerPrefs.SetInt(MainMenuController.ShowIntroNextLaunchKey, 1);
            PlayerPrefs.Save();
            Debug.Log("Tutorial will be shown on the next play launch only.");
        }
    }
}
