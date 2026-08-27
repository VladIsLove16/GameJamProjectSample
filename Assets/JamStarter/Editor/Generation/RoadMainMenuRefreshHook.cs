using System.IO;
using UnityEditor;
using UnityEngine;

namespace JamStarter.Editor
{
    [InitializeOnLoad]
    internal static class RoadMainMenuRefreshHook
    {
        private const string RequestPath = "Temp/RoadOfLife.RefreshMainMenu.request";

        static RoadMainMenuRefreshHook()
        {
            EditorApplication.update -= ConsumeRequest;
            EditorApplication.update += ConsumeRequest;
        }

        private static void ConsumeRequest()
        {
            if (EditorApplication.isCompiling || EditorApplication.isUpdating ||
                EditorApplication.isPlayingOrWillChangePlaymode)
            {
                return;
            }

            string path = Path.GetFullPath(Path.Combine(Application.dataPath, "..", RequestPath));
            if (!File.Exists(path))
            {
                return;
            }

            File.Delete(path);
            EditorApplication.update -= ConsumeRequest;
            JamStarterProjectGenerator.RefreshMainMenuSettings();
        }
    }
}
