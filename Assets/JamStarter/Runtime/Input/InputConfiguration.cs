using UnityEngine;
using UnityEngine.InputSystem;

namespace JamStarter
{
    [CreateAssetMenu(fileName = "InputConfiguration", menuName = "Jam Starter/Input Configuration")]
    public sealed class InputConfiguration : ScriptableObject
    {
        [SerializeField] private InputActionReference assetAnchor;

        public InputActionAsset Actions =>
            assetAnchor != null && assetAnchor.action != null
                ? assetAnchor.action.actionMap?.asset
                : null;
    }
}
