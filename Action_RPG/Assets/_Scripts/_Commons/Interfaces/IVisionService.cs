using UnityEngine;
using System.Collections.Generic;
using Game.Features.Vision.Core;
using Game.Features.Vision.Data;

namespace Game.Features.Vision.Interfaces
{
    /// <summary>
    /// Contract for vision system services.
    /// Implement for different entity types (player, companion, etc).
    /// </summary>
    public interface IVisionService
    {
        /// <summary>
        /// Initialize vision service with configuration.
        /// Must be called before other methods.
        /// </summary>
        /// <param name="config">Vision configuration containing ranges and parameters</param>
        void Initialize(VisionConfig config);

        /// <summary>
        /// Update vision from a specific position and range.
        /// Recalculates visible objects within the vision sphere.
        /// </summary>
        /// <param name="position">World position where vision originates</param>
        /// <param name="visionRange">Vision range radius in units</param>
        void UpdateVisionFromPosition(Vector3 position, float visionRange);

        /// <summary>
        /// Get list of currently visible colliders.
        /// </summary>
        /// <returns>List of visible colliders within vision range</returns>
        List<Collider> GetVisibleObjects();

        /// <summary>
        /// Get the internal model state (read-only access).
        /// </summary>
        /// <returns>VisionModel containing runtime state</returns>
        VisionModel GetModel();

        /// <summary>
        /// Event triggered when visible objects list changes.
        /// </summary>
        event System.Action<List<Collider>> OnVisibleObjectsChanged;
    }
}
