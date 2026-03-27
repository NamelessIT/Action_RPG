using UnityEngine;
using System.Collections.Generic;
using Game.Features.Vision.Interfaces;
using Game.Features.Vision.Core;
using Game.Features.Vision.Data;

namespace Game.Features.Vision.Core
{
    /// <summary>
    /// Main vision service implementation. Pure C# - NO MonoBehavior.
    /// Handles vision range calculations and visible object tracking.
    /// 100% testable in EditMode - contains NO Unity lifecycle methods.
    /// </summary>
    public class VisionSystem : IVisionService
    {
        private VisionConfig _config;
        private VisionModel _model;
        private Collider[] _overlapResults;

        /// <summary>
        /// Event fired when visible objects list changes.
        /// </summary>
        public event System.Action<List<Collider>> OnVisibleObjectsChanged;

        /// <summary>
        /// Initializes a new instance of VisionSystem.
        /// Pre-allocates OverlapSphere buffer for performance.
        /// </summary>
        public VisionSystem()
        {
            // [001-D] Pre-allocate buffer for Physics.OverlapSphereNonAlloc
            _overlapResults = new Collider[256];
        }

        /// <summary>
        /// Initialize vision service with configuration.
        /// Must be called before UpdateVisionFromPosition.
        /// </summary>
        /// <param name="config">Vision configuration containing ranges and settings</param>
        /// <exception cref="System.ArgumentNullException">Thrown if config is null</exception>
        public void Initialize(VisionConfig config)
        {
            // [001-D] Validate config
            if (config == null)
                throw new System.ArgumentNullException(nameof(config), "VisionConfig cannot be null");

            _config = config;
            
            // [001-D] Create model with initial state
            _model = new VisionModel(Vector3.zero, config.PlayerVisionRange);
        }

        /// <summary>
        /// Update vision from a specific position, recalculating visible objects.
        /// Uses Physics.OverlapSphereNonAlloc for performance.
        /// </summary>
        /// <param name="position">World position where vision originates</param>
        /// <param name="visionRange">Vision range radius in units</param>
        public void UpdateVisionFromPosition(Vector3 position, float visionRange)
        {
            // [001-D] Early exit checks
            if (_model == null)
            {
                Debug.LogWarning("[VisionSystem] Model is null. Call Initialize() first.");
                return;
            }

            if (_config == null)
            {
                Debug.LogWarning("[VisionSystem] Config is null. Call Initialize() first.");
                return;
            }

            // [001-D] Update model position and range
            _model.Position = position;
            _model.VisionRange = visionRange;
            _model.LastUpdateTime = Time.time;

            // [001-D] Physics sphere cast - non-allocating version
            int count = Physics.OverlapSphereNonAlloc(
                position,
                visionRange,
                _overlapResults,
                LayerMask.GetMask("Default")
            );

            // [001-D] Build list of visible objects (limit by config)
            var visibleObjects = new List<Collider>(count);
            int maxObjects = _config.MaxVisibleObjects;
            
            for (int i = 0; i < count && i < maxObjects; i++)
            {
                visibleObjects.Add(_overlapResults[i]);
            }

            // [001-D] Update model and notify observers
            _model.UpdateVisibleObjects(visibleObjects);
            OnVisibleObjectsChanged?.Invoke(visibleObjects);
        }

        /// <summary>
        /// Get list of currently visible colliders.
        /// </summary>
        /// <returns>Read-only list of visible colliders</returns>
        public List<Collider> GetVisibleObjects()
        {
            // [001-D] Return null-safe empty list if not initialized
            if (_model == null)
            {
                Debug.LogWarning("[VisionSystem] Model is null. Returning empty list.");
                return new List<Collider>();
            }

            return _model.VisibleObjects;
        }

        /// <summary>
        /// Get the internal model state for external inspection.
        /// </summary>
        /// <returns>VisionModel containing runtime state</returns>
        public VisionModel GetModel()
        {
            // [001-D] Return null if not initialized
            if (_model == null)
            {
                Debug.LogWarning("[VisionSystem] Model is null. Initialize() not called.");
                return null;
            }

            return _model;
        }

        /// <summary>
        /// Create a merged vision result from multiple vision sources.
        /// [004-A] Combines player + companion visible objects without duplicates.
        /// </summary>
        /// <param name="playerVisible">List of objects visible to player</param>
        /// <param name="companionVisible">List of objects visible to companion</param>
        /// <returns>Combined list of all visible objects (no duplicates)</returns>
        public static List<Collider> MergeVisionResults(
            List<Collider> playerVisible,
            List<Collider> companionVisible)
        {
            // [004-A] Validate inputs
            if (playerVisible == null)
                playerVisible = new List<Collider>();
            if (companionVisible == null)
                companionVisible = new List<Collider>();

            // [004-A] Create merged list starting with player visible
            var merged = new List<Collider>(playerVisible);

            // [004-A] Add companion objects (avoid duplicates)
            foreach (var obj in companionVisible)
            {
                if (obj != null && !merged.Contains(obj))
                {
                    merged.Add(obj);
                }
            }

            return merged;
        }

        /// <summary>
        /// Publish merged vision update to subscribers.
        /// [004-B] Used by VisionCoordinator to notify UI/Fade systems.
        /// </summary>
        /// <param name="mergedObjects">List of merged visible objects</param>
        public void PublishMergedVision(List<Collider> mergedObjects)
        {
            // [004-B] Notify subscribers
            OnMergedVisionChanged?.Invoke(mergedObjects);
        }

        /// <summary>
        /// Event fired when merged vision changes (player + companion combined).
        /// [004-B] Listeners: FadeEffectManager, UI systems, etc.
        /// </summary>
        public event System.Action<List<Collider>> OnMergedVisionChanged;
    }
}
