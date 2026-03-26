using UnityEngine;
using System.Collections.Generic;
using Game.Features.Vision.Interfaces;
using Game.Features.Vision.Core;
using Game.Features.Vision.Data;
using Game.Features.Vision.Systems; // [005-E] For FadeEffectManager
using Game.Features.Companion; // [004-C] For companion integration

namespace Game.Features.Player
{
    /// <summary>
    /// Manages player vision instance. Adapter between player and VisionSystem.
    /// Updates player's vision range each frame and notifies listeners of visible objects.
    /// [004-C] Integrated with VisionCoordinator for companion vision merging.
    /// [005-E] Integrated with FadeEffectManager for fade effects.
    /// </summary>
    public class PlayerVisionManager : MonoBehaviour
    {
        [SerializeField] private VisionConfig _visionConfig;
        
        private IVisionService _visionService;
        private VisionCoordinator _visionCoordinator;
        private CompanionVisionManager _companionVisionManager;
        private FadeEffectManager _fadeEffectManager; // [005-E] Fade system
        private float _nextUpdateTime;
        private float _coordinatorInitTime; // [004-C] For delayed initialization
        private float _fadeInitTime; // [005-E] For delayed fade initialization

        /// <summary>
        /// Awake is called when the script instance is being loaded.
        /// Initializes the vision system.
        /// </summary>
        private void Awake()
        {
            // [002-A] Initialize vision service
            InitializeVision();
        }

        /// <summary>
        /// Initialize vision service with config and subscribe to events.
        /// </summary>
        private void InitializeVision()
        {
            // [002-A] Validate config
            if (_visionConfig == null)
            {
                // Runtime fallback for auto-added component: create config with script defaults.
                _visionConfig = ScriptableObject.CreateInstance<VisionConfig>();
            }

            // [002-A] Create and initialize service
            var visionSystem = new VisionSystem();
            visionSystem.Initialize(_visionConfig);
            _visionService = visionSystem;

            // [002-A] Subscribe to vision changes
            _visionService.OnVisibleObjectsChanged += OnVisibleObjectsChanged;

            // [002-A] Initialize update throttle
            _nextUpdateTime = Time.time;
            
            // [004-C] Setup coordinator initialization (delayed to allow companion to initialize)
            _coordinatorInitTime = Time.time + 0.1f;

            // [005-E] Setup fade manager initialization
            _fadeInitTime = Time.time + 0.15f;
            
            Debug.Log("[002-A] PlayerVisionManager initialized successfully.");
        }

        /// <summary>
        /// Update is called once per frame.
        /// Updates player vision position if update interval elapsed.
        /// </summary>
        private void Update()
        {
            // [004-C] Try to initialize coordinator if not done yet (allows companion to initialize first)
            if (_visionCoordinator == null && Time.time >= _coordinatorInitTime)
            {
                TryInitializeCoordinator();
            }

            // [005-E] Try to initialize fade effect manager
            if (_fadeEffectManager == null && Time.time >= _fadeInitTime)
            {
                TryInitializeFadeEffects();
            }

            // [002-B] Update player vision position
            UpdatePlayerVisionPosition();
        }

        /// <summary>
        /// Try to initialize fade effect manager.
        /// [009-E] Simplified: calls SetVisionSources() + SetFadeDistances() once.
        /// FadeEffectManager now self-manages evaluation based on distance only.
        /// </summary>
        private void TryInitializeFadeEffects()
        {
            // [005-E] Find or create FadeEffectManager
            _fadeEffectManager = FindFirstObjectByType<FadeEffectManager>();
            if (_fadeEffectManager == null)
            {
                var fadeObj = new GameObject("VisionFadeEffectManager");
                _fadeEffectManager = fadeObj.AddComponent<FadeEffectManager>();
                Debug.Log("[005-E] FadeEffectManager was auto-created.");
            }
            else
            {
                Debug.Log("[005-E] FadeEffectManager found in scene.");
            }

            // [009-E] Configure vision sources (player + companion)
            if (_companionVisionManager != null && _companionVisionManager.gameObject.activeInHierarchy)
            {
                _fadeEffectManager.SetVisionSources(transform, _companionVisionManager.transform);
                Debug.Log("[009-E] Fade system set to use player + companion as vision sources.");
            }
            else
            {
                _fadeEffectManager.SetVisionSources(transform);
                Debug.Log("[009-E] Fade system set to use player as vision source.");
            }

            // [009-E] Configure fade distances
            if (_visionConfig != null)
            {
                _fadeEffectManager.SetFadeDistances(
                    _visionConfig.FadeStartDistance,
                    _visionConfig.FadeCompleteDistance
                );
            }

            // [007-F] Set excluded transforms so player + companion never fade
            TrySetExcludedTransforms();
        }

        /// <summary>
        /// Try to initialize vision coordinator (merges player + companion vision).
        /// [004-C] Safe initialization that waits for companion manager if needed.
        /// [009-F] Simplified: no longer subscribes to OnMergedVisionChanged.
        /// FadeEffectManager now self-manages via vision sources.
        /// </summary>
        private void TryInitializeCoordinator()
        {
            // [004-C] Try to find companion manager
            if (_companionVisionManager == null)
            {
                _companionVisionManager = FindFirstObjectByType<CompanionVisionManager>();
            }

            // [004-C] Create coordinator if not exists
            if (_visionCoordinator == null)
            {
                var coordinatorObj = new GameObject("VisionCoordinator");
                _visionCoordinator = coordinatorObj.AddComponent<VisionCoordinator>();
            }

            // [004-C] Initialize coordinator with player and companion services
            if (_visionCoordinator != null && _visionService != null)
            {
                _visionCoordinator.Initialize(_visionService, _companionVisionManager);
                // [009-F] No longer subscribe to OnMergedVisionChanged — fade system is now self-managed
                Debug.Log("[004-C] VisionCoordinator initialized in PlayerVisionManager.");
            }

            // [007-F] Set excluded transforms so player + companion never fade
            TrySetExcludedTransforms();
        }

        /// <summary>
        /// Update player vision position and range.
        /// Uses time-based throttling to avoid excessive calculations.
        /// </summary>
        private void UpdatePlayerVisionPosition()
        {
            // [002-B] Throttle updates using interval
            if (_visionService == null || _visionConfig == null) 
                return;

            if (Time.time < _nextUpdateTime)
                return;

            // [002-B] Update vision with player position and range
            _visionService.UpdateVisionFromPosition(
                transform.position,
                _visionConfig.PlayerVisionRange
            );

            // [002-B] Schedule next update
            _nextUpdateTime = Time.time + _visionConfig.VisionUpdateInterval;
        }

        /// <summary>
        /// Callback when player-only visible objects list changes.
        /// [009-F] Simplified: no fade logic here anymore.
        /// FadeEffectManager is now self-managed via vision sources.
        /// </summary>
        /// <param name="visibleObjects">List of currently visible colliders</param>
        private void OnVisibleObjectsChanged(List<Collider> visibleObjects)
        {
            // [002-A] Log for debugging
            Debug.Log($"[002-A] Player sees {visibleObjects.Count} objects");
            
            // Fade system is now self-managed — no action needed here
        }

        /// <summary>
        /// Set excluded transforms on FadeEffectManager so player + companion never fade.
        /// [007-F] Called after coordinator and fade manager are initialized.
        /// </summary>
        private void TrySetExcludedTransforms()
        {
            if (_fadeEffectManager == null)
                return;

            // [007-F] Build exclusion list: player + companion
            if (_companionVisionManager != null)
            {
                _fadeEffectManager.SetExcludedTransforms(transform, _companionVisionManager.transform);
                Debug.Log("[007-F] Excluded player + companion from fade.");
            }
            else
            {
                _fadeEffectManager.SetExcludedTransforms(transform);
                Debug.Log("[007-F] Excluded player from fade (no companion found).");
            }
        }

        /// <summary>
        /// OnDestroy is called when the GameObject is destroyed.
        /// Unsubscribes from events.
        /// </summary>
        private void OnDestroy()
        {
            // [002-A] Cleanup - unsubscribe from events
            if (_visionService != null)
            {
                _visionService.OnVisibleObjectsChanged -= OnVisibleObjectsChanged;
            }

            // [009-F] Cleanup coordinator (no OnMergedVisionChanged unsubscribe — fade is now self-managed)
            if (_visionCoordinator != null)
            {
                Destroy(_visionCoordinator.gameObject);
            }

            // [005-E] Cleanup fade manager
            if (_fadeEffectManager != null)
            {
                _fadeEffectManager.ClearFadeState();
                Destroy(_fadeEffectManager.gameObject);
            }
        }

        /// <summary>
        /// Get the current vision service (for testing or external access).
        /// </summary>
        /// <returns>The IVisionService instance</returns>
        public IVisionService GetVisionService()
        {
            return _visionService;
        }

        /// <summary>
        /// Get the vision coordinator (for merged vision access).
        /// [004-C] Returns coordinator managing player + companion merged vision.
        /// </summary>
        /// <returns>The VisionCoordinator instance (may be null if not initialized yet)</returns>
        public VisionCoordinator GetVisionCoordinator()
        {
            return _visionCoordinator;
        }

        /// <summary>
        /// Get the fade effect manager.
        /// [005-E] Returns fade manager for direct fade effect control if needed.
        /// </summary>
        /// <returns>The FadeEffectManager instance (may be null if not initialized yet)</returns>
        public FadeEffectManager GetFadeEffectManager()
        {
            return _fadeEffectManager;
        }

        /// <summary>
        /// Get list of currently visible objects.
        /// </summary>
        /// <returns>List of visible colliders</returns>
        public List<Collider> GetVisibleObjects()
        {
            if (_visionService == null)
            {
                Debug.LogWarning("[PlayerVisionManager] VisionService not initialized.");
                return new List<Collider>();
            }

            return _visionService.GetVisibleObjects();
        }

        /// <summary>
        /// OnDrawGizmos is called for debugging in scene view.
        /// Draws vision range sphere.
        /// </summary>
        private void OnDrawGizmos()
        {
            // [002-F] Debug draw vision range
            if (_visionConfig == null)
                return;

            Gizmos.color = Color.green;
            Gizmos.DrawWireSphere(transform.position, _visionConfig.PlayerVisionRange);
        }
    }
}
