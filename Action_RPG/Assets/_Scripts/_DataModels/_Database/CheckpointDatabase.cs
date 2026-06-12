using UnityEngine;
using System.Collections.Generic;

[CreateAssetMenu(
    fileName = "CheckpointDatabase",
    menuName = "Database/Checkpoint Database"
)]
public class CheckpointDatabase : ScriptableObject
{
    public List<CheckpointDB> checkpoints = new List<CheckpointDB>();

    private Dictionary<int, CheckpointDB> cache;

    /// <summary>
    /// [DAO Layer] Get checkpoint data by ID with caching.
    /// Only called from InAppPlayerStateDAO.LoadCheckpoint().
    /// </summary>
    public CheckpointDB GetCheckpoint(int id)
    {
        if (cache == null)
        {
            cache = new Dictionary<int, CheckpointDB>();
            foreach (var checkpoint in checkpoints)
            {
                if (!cache.ContainsKey(checkpoint.id))
                    cache.Add(checkpoint.id, checkpoint);
            }
        }

        return cache.TryGetValue(id, out var result) ? result : null;
    }
}
