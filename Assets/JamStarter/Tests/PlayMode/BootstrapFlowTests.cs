using System.Collections;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

namespace JamStarter.Tests
{
    public sealed class BootstrapFlowTests
    {
        [UnityTest]
        public IEnumerator Bootstrap_LoadsMainMenuAndKeepsApplicationRoot()
        {
            Time.timeScale = 1f;
            SceneManager.LoadScene(SceneNames.Bootstrap, LoadSceneMode.Single);

            const float timeout = 5f;
            float startedAt = Time.realtimeSinceStartup;
            while (SceneManager.GetActiveScene().name != SceneNames.MainMenu &&
                   Time.realtimeSinceStartup - startedAt < timeout)
            {
                yield return null;
            }

            Assert.That(SceneManager.GetActiveScene().name, Is.EqualTo(SceneNames.MainMenu));
            Assert.That(Object.FindFirstObjectByType<AppBootstrap>(), Is.Not.Null);
            Assert.That(Object.FindFirstObjectByType<MainMenuController>(), Is.Not.Null);
        }

        [UnityTest]
        public IEnumerator PauseService_RestoresPreviousTimeScale()
        {
            var gameObject = new GameObject("Pause Service Test");
            GamePauseService pause = gameObject.AddComponent<GamePauseService>();
            Time.timeScale = 0.5f;

            Assert.That(pause.Pause(), Is.True);
            Assert.That(Time.timeScale, Is.Zero);
            Assert.That(pause.Resume(), Is.True);
            Assert.That(Time.timeScale, Is.EqualTo(0.5f));

            Object.Destroy(gameObject);
            Time.timeScale = 1f;
            yield return null;
        }
    }
}
