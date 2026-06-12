using UnityEngine;
using System.Collections.Generic;
using Game.Features.Vision.Interfaces;
using Game.Features.Vision.Core;
using Game.Features.Companion;

namespace Game.Features.Vision.Core
{
    /// <summary>
    /// Coordinates vision merging between player and companion.
    /// [004-C] Merges player + companion vision into unified visible objects list.
    /// Publishes to UI, Fade systems, etc.
    /// </summary>
    public class VisionCoordinator : MonoBehaviour
    {
        private IVisionService _playerVisionService;
        private CompanionVisionManager _companionVisionManager;
        
        private List<Collider> _mergedVisibleObjects = new List<Collider>();

        /// <summary>
        /// Event fired when merged vision changes.
        /// </summary>
        public event System.Action<List<Collider>> OnMergedVisionChanged;

        /// <summary>
        /// Initialize coordinator with player and companion vision services.
        /// </summary>
        /// <param name="playerService">Player's vision service</param>
        /// <param name="companionManager">Companion's vision manager</param>
        public void Initialize(IVisionService playerService, CompanionVisionManager companionManager)
        {
            // [004-C] Validate inputs
            if (playerService == null)
            {
                Debug.LogError("[VisionCoordinator] Player vision service is null.");
                return;
            }

            _playerVisionService = playerService;
            _companionVisionManager = companionManager;

            // [004-C] Subscribe to player vision changes
            _playerVisionService.OnVisibleObjectsChanged += OnPlayerVisionChanged;

            // [004-C] Subscribe to companion vision changes if exists
            if (_companionVisionManager != null)
            {
                var companionService = _companionVisionManager.GetVisionService();
                if (companionService != null)
                {
                    companionService.OnVisibleObjectsChanged += OnCompanionVisionChanged;
                    Debug.Log("[004-C] VisionCoordinator subscribed to companion vision.");
                }
            }

            Debug.Log("[004-C] VisionCoordinator initialized.");
        }

        /// <summary>
        /// Called when player's visible objects change.
        /// [004-C] Triggers merge and publishes to subscribers.
        /// </summary>
        private void OnPlayerVisionChanged(List<Collider> playerVisible)
        {
            // [004-C] Perform merge
            MergeAndPublish(playerVisible);
        }

        /// <summary>
        /// Called when companion's visible objects change.
        /// [004-C] Triggers merge and publishes to subscribers.
        /// </summary>
        private void OnCompanionVisionChanged(List<Collider> companionVisible)
        {
            // [004-C] Get current player vision and merge
            if (_playerVisionService != null)
            {
                var playerVisible = _playerVisionService.GetVisibleObjects();
                MergeAndPublish(playerVisible);
            }
        }

        /// <summary>
        /// Merge player and companion vision, then publish.
        /// [004-C] Handles null safety and publishes merged results.
        /// </summary>
        private void MergeAndPublish(List<Collider> playerVisible)
        {
            // [004-C] Get companion visible objects
            List<Collider> companionVisible = _companionVisionManager != null
                ? _companionVisionManager.GetCompanionVisibleObjects()
                : new List<Collider>();

            // [004-C] Merge using VisionSystem static method
            _mergedVisibleObjects = VisionSystem.MergeVisionResults(playerVisible, companionVisible);

            // [004-C] Log for debugging
            int newCount = playerVisible?.Count ?? 0;
            int companionCount = companionVisible?.Count ?? 0;
            //Debug.Log($"[004-C] Merged vision: Player={newCount} + Companion={companionCount} = {_mergedVisibleObjects.Count} total");

            // [004-C] Publish merged vision
            OnMergedVisionChanged?.Invoke(_mergedVisibleObjects);
        }

        /// <summary>
        /// Get current merged visible objects.
        /// </summary>
        /// <returns>List of all visible objects (player + companion combined)</returns>
        public List<Collider> GetMergedVisibleObjects()
        {
            return _mergedVisibleObjects ?? new List<Collider>();
        }

        /// <summary>
        /// Clean up subscriptions.
        /// </summary>
        private void OnDestroy()
        {
            // [004-C] Unsubscribe from events
            if (_playerVisionService != null)
            {
                _playerVisionService.OnVisibleObjectsChanged -= OnPlayerVisionChanged;
            }

            if (_companionVisionManager != null)
            {
                var companionService = _companionVisionManager.GetVisionService();
                if (companionService != null)
                {
                    companionService.OnVisibleObjectsChanged -= OnCompanionVisionChanged;
                }
            }
        }
    }
}
