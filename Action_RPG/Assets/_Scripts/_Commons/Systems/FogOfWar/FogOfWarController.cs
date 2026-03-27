using UnityEngine;
using Game.Features.Vision.Data;

namespace Game.Features.Vision.Rendering
{
    /// <summary>
    /// [008-E] MonoBehaviour that sets shader globals for FogOfWar shader every frame.
    /// Passes player/companion positions and vision config to the GPU via Shader.SetGlobalXXX.
    /// The FogOfWarFeature/Pass reads these globals automatically.
    /// </summary>
    public class FogOfWarController : MonoBehaviour
    {
        private Transform _playerTransform;
        private Transform _companionTransform;
        private VisionConfig _config;

        /// <summary>
        /// [008-E] Initialize with player, companion, and config references.
        /// Called once by PlayerVisionManager during setup.
        /// </summary>
        public void Initialize(Transform player, Transform companion, VisionConfig config)
        {
            _playerTransform = player;
            _companionTransform = companion;
            _config = config;
            Debug.Log("[008-E] FogOfWarController initialized.");
        }

        private void Update()
        {
            if (_playerTransform == null || _config == null || !_config.EnableFogOfWar)
                return;

            // [008-E] Set player vision data
            Shader.SetGlobalVector("_FoW_PlayerPos", _playerTransform.position);
            Shader.SetGlobalFloat("_FoW_PlayerRange", _config.PlayerVisionRange);

            // [008-E] Set fog appearance
            Shader.SetGlobalColor("_FoW_FogColor", _config.FogColor);
            Shader.SetGlobalFloat("_FoW_EdgeSoftness", _config.FogEdgeSoftness);

            // [008-E] Set companion data (if present and active)
            if (_companionTransform != null && _companionTransform.gameObject.activeInHierarchy)
            {
                Shader.SetGlobalVector("_FoW_CompanionPos", _companionTransform.position);
                Shader.SetGlobalFloat("_FoW_CompanionRange", _config.CompanionVisionRange);
                Shader.SetGlobalFloat("_FoW_HasCompanion", 1f);
            }
            else
            {
                Shader.SetGlobalFloat("_FoW_HasCompanion", 0f);
            }
        }

        private void OnDisable()
        {
            // [008-E] Reset fog when disabled so scene doesn't stay fogged
            Shader.SetGlobalFloat("_FoW_HasCompanion", 0f);
        }
    }
}
