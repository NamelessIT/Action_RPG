using UnityEngine;
using System.Collections.Generic;

namespace Game.Features.Vision.Core
{
    /// <summary>
    /// Runtime state holder for vision data. Does NOT inherit MonoBehavior.
    /// Pure C# class - fully testable in EditMode.
    /// </summary>
    public class VisionModel
    {
        /// <summary>
        /// Current position of the vision origin (player or companion).
        /// </summary>
        public Vector3 Position { get; set; }

        /// <summary>
        /// Current vision range in units.
        /// </summary>
        public float VisionRange { get; set; }

        /// <summary>
        /// List of currently visible colliders within vision range.
        /// </summary>
        public List<Collider> VisibleObjects { get; private set; } = new List<Collider>();

        /// <summary>
        /// Timestamp of last vision update.
        /// </summary>
        public float LastUpdateTime { get; set; }

        /// <summary>
        /// Initializes a new VisionModel with starting position and vision range.
        /// </summary>
        /// <param name="startPosition">Initial position of vision origin</param>
        /// <param name="visionRange">Initial vision range in units</param>
        public VisionModel(Vector3 startPosition, float visionRange)
        {
            Position = startPosition;
            VisionRange = visionRange;
            LastUpdateTime = 0f;
        }

        /// <summary>
        /// Updates the list of visible objects.
        /// </summary>
        /// <param name="newVisibleObjects">New list of colliders to replace current visible objects</param>
        public void UpdateVisibleObjects(List<Collider> newVisibleObjects)
        {
            VisibleObjects.Clear();
            VisibleObjects.AddRange(newVisibleObjects);
        }

        /// <summary>
        /// Clears all visible objects.
        /// </summary>
        public void ClearVisibleObjects()
        {
            VisibleObjects.Clear();
        }
    }
}
