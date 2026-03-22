using UnityEngine;
using System.Collections.Generic;
using Game.Features.Vision.Interfaces;
using Game.Features.Vision.Core;
using Game.Features.Vision.Data;

namespace Game.Features.Player
{
    /// <summary>
    /// Manages player vision instance. Adapter between player and VisionSystem.
    /// Updates player's vision range each frame and notifies listeners of visible objects.
    /// </summary>
    public class PlayerVisionManager : MonoBehaviour
    {
        [SerializeField] private VisionConfig _visionConfig;
        
        private IVisionService _visionService;
        private float _nextUpdateTime;

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
                Debug.LogError("[PlayerVisionManager] VisionConfig is not assigned. Aborting vision initialization.");
                return;
            }

            // [002-A] Create and initialize service
            var visionSystem = new VisionSystem();
            visionSystem.Initialize(_visionConfig);
            _visionService = visionSystem;

            // [002-A] Subscribe to vision changes
            _visionService.OnVisibleObjectsChanged += OnVisibleObjectsChanged;

            // [002-A] Initialize update throttle
            _nextUpdateTime = Time.time;
            
            Debug.Log("[002-A] PlayerVisionManager initialized successfully.");
        }

        /// <summary>
        /// Update is called once per frame.
        /// Updates player vision position if update interval elapsed.
        /// </summary>
        private void Update()
        {
            // [002-B] Update player vision position
            UpdatePlayerVisionPosition();
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
        /// Callback when visible objects list changes.
        /// </summary>
        /// <param name="visibleObjects">List of currently visible colliders</param>
        private void OnVisibleObjectsChanged(List<Collider> visibleObjects)
        {
            // [002-A] Log for debugging
            Debug.Log($"[002-A] Player sees {visibleObjects.Count} objects");
            
            // Will integrate with rendering/fade system later
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
