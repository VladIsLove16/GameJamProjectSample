using UnityEditor;
using UnityEngine;

namespace JamStarter.Editor
{
    internal static class RoadIntroTestCommand
    {
        [MenuItem("Tools/Road of Life/Show Tutorial On Next Play", false, 106)]
        private static void ShowTutorialOnNextPlay()
        {
            SettingsService.RequestTutorialOnNextPlay();
            Debug.Log("Tutorial will be shown once on the next entry into the game scene.");
        }
    }
}
