using UnityEngine;

namespace JamStarter
{
    /// <summary>
    /// Tiny neutral visual/input smoke test for the starter scene. Delete it together
    /// with the Sandbox scene when real gameplay is ready.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class SandboxShowcase : MonoBehaviour, IAppServicesConsumer
    {
        [SerializeField] private Transform target;
        [SerializeField, Min(0f)] private float movementSpeed = 4f;
        [SerializeField, Min(0f)] private float bounds = 3.5f;

        private InputReader input;

        public void Initialize(AppServices services)
        {
            input = services.Input;
        }

        private void Update()
        {
            if (input == null || target == null)
            {
                return;
            }

            Vector2 move = input.Move;
            Vector3 position = target.localPosition;
            position += new Vector3(move.x, 0f, move.y) * (movementSpeed * Time.deltaTime);
            position.x = Mathf.Clamp(position.x, -bounds, bounds);
            position.z = Mathf.Clamp(position.z, -bounds, bounds);
            target.localPosition = position;
            target.Rotate(Vector3.up, 35f * Time.deltaTime, Space.World);
        }
    }
}
