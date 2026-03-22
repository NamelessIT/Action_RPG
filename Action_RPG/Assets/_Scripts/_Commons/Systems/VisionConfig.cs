using UnityEngine;

namespace Game.Features.Vision.Data
{
    /// <summary>
    /// Static configuration for vision system.
    /// Create asset via: Assets/Create/Game/Vision/Vision Config
    /// </summary>
    [CreateAssetMenu(menuName = "Game/Vision/Vision Config")]
    public class VisionConfig : ScriptableObject
    {
        [Header("Vision Ranges")]
        [SerializeField] private float _playerVisionRange = 20f;
        [SerializeField] private float _companionVisionRange = 8f;

        [Header("Fade Effect")]
        [SerializeField] private float _fadeStartDistance = 18f;  // When fade begins
        [SerializeField] private float _fadeCompleteDistance = 25f; // Complete fade (alpha=0)

        [Header("Performance")]
        [SerializeField] private float _visionUpdateInterval = 0.1f; // Update every 0.1s
        [SerializeField] private int _maxVisibleObjects = 256;

        /// <summary>
        /// Player vision range in units.
        /// </summary>
        public float PlayerVisionRange => _playerVisionRange;

        /// <summary>
        /// Companion vision range in units.
        /// </summary>
        public float CompanionVisionRange => _companionVisionRange;

        /// <summary>
        /// Distance at which fade effect starts.
        /// </summary>
        public float FadeStartDistance => _fadeStartDistance;

        /// <summary>
        /// Distance at which objects are completely faded (alpha=0).
        /// </summary>
        public float FadeCompleteDistance => _fadeCompleteDistance;

        /// <summary>
        /// Interval in seconds between vision updates.
        /// </summary>
        public float VisionUpdateInterval => _visionUpdateInterval;

        /// <summary>
        /// Maximum number of visible objects tracked.
        /// </summary>
        public int MaxVisibleObjects => _maxVisibleObjects;
    }
}
