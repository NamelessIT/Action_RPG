using UnityEngine;
using System.Collections.Generic;
using Game.Features.Vision.Interfaces;
using Game.Features.Vision.Core;
using Game.Features.Vision.Data;

namespace Game.Features.Companion
{
    /// <summary>
    /// Manages companion vision instance. Adapter between companion and VisionSystem.
    /// Companion shares vision with player (range=8).
    /// [SESSION-2] TASK-003-A
    /// </summary>
    public class CompanionVisionManager : MonoBehaviour
    {
        [SerializeField] private VisionConfig _visionConfig;
        
        private IVisionService _visionService;
        private float _nextUpdateTime;

        /// <summary>
        /// Called when the script instance is being loaded.
        /// Initializes the companion vision system.
        /// </summary>
        private void Awake()
        {
            // [003-A] Initialize vision service
            InitializeVision();
        }

        /// <summary>
        /// Initialize vision service with config and subscribe to events.
        /// </summary>
        private void InitializeVision()
        {
            // [003-A] Validate config
            if (_visionConfig == null)
            {
                _visionConfig = ScriptableObject.CreateInstance<VisionConfig>();
            }

            // [003-A] Create and initialize service
            var visionSystem = new VisionSystem();
            visionSystem.Initialize(_visionConfig);
            _visionService = visionSystem;

            // [003-A] Subscribe to vision changes
            _visionService.OnVisibleObjectsChanged += OnCompanionVisibleObjectsChanged;

            // [003-A] Initialize update throttle
            _nextUpdateTime = Time.time;
            
            Debug.Log("[003-A] CompanionVisionManager initialized successfully.");
        }

        /// <summary>
        /// Update is called once per frame.
        /// Updates companion vision position if update interval elapsed.
        /// </summary>
        private void Update()
        {
            // [003-B] Update companion vision position
            UpdateCompanionVisionPosition();
        }

        /// <summary>
        /// Update companion vision position and range.
        /// Uses time-based throttling to avoid excessive calculations.
        /// [003-B] Implementation
        /// </summary>
        private void UpdateCompanionVisionPosition()
        {
            // [003-B] Throttle updates using interval
            if (_visionService == null || _visionConfig == null) 
                return;

            if (Time.time < _nextUpdateTime)
                return;

            // [003-B] Update vision with companion position and COMPANION RANGE (8)
            _visionService.UpdateVisionFromPosition(
                transform.position,
                _visionConfig.CompanionVisionRange
            );

            // [003-B] Schedule next update
            _nextUpdateTime = Time.time + _visionConfig.VisionUpdateInterval;
        }

        /// <summary>
        /// Callback when companion's visible objects list changes.
        /// </summary>
        /// <param name="visibleObjects">List of currently visible colliders</param>
        private void OnCompanionVisibleObjectsChanged(List<Collider> visibleObjects)
        {
            // [003-A] Log for debugging
            //Debug.Log($"[003-A] Companion sees {visibleObjects.Count} objects");
            
            // Will be merged with player vision in TASK-004
        }

        /// <summary>
        /// Get the current vision service (for external access and merging in TASK-004).
        /// </summary>
        /// <returns>The IVisionService instance</returns>
        public IVisionService GetVisionService()
        {
            return _visionService;
        }

        /// <summary>
        /// Get list of objects visible to companion.
        /// </summary>
        /// <returns>List of visible colliders</returns>
        public List<Collider> GetCompanionVisibleObjects()
        {
            return _visionService?.GetVisibleObjects() ?? new List<Collider>();
        }

        /// <summary>
        /// Called when the GameObject is destroyed.
        /// Unsubscribes from events.
        /// </summary>
        private void OnDestroy()
        {
            // [003-A] Cleanup - unsubscribe from events
            if (_visionService != null)
            {
                _visionService.OnVisibleObjectsChanged -= OnCompanionVisibleObjectsChanged;
            }
        }
    }
}
