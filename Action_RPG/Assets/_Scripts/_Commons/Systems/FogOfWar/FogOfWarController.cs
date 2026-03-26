using System.Collections.Generic;
using UnityEngine;
using Game.Features.Vision.Data;

namespace Game.Features.Vision.Core
{
    /// <summary>
    /// [008-E] Manages fog of war visibility sheet (explored vs unexplored areas).
    /// Maintains a runtime texture that tracks which regions have been revealed by vision.
    /// </summary>
    public class FogOfWarController
    {
        private Texture2D _fogSheet;
        private Color[] _fogPixels;
        
        private int _sheetWidth;
        private int _sheetHeight;
        private float _pixelsPerUnit;
        private Vector2 _mapOrigin;

        /// <summary>
        /// Initialize fog sheet with dimensions.
        /// </summary>
        /// <param name="worldWidth">World width in units</param>
        /// <param name="worldHeight">World height in units</param>
        /// <param name="pixelsPerUnit">Resolution: higher = more detailed fog</param>
        public void Initialize(float worldWidth, float worldHeight, float pixelsPerUnit = 0.1f)
        {
            _pixelsPerUnit = pixelsPerUnit;
            _sheetWidth = (int)(worldWidth * pixelsPerUnit);
            _sheetHeight = (int)(worldHeight * pixelsPerUnit);
            _mapOrigin = new Vector2(-worldWidth / 2f, -worldHeight / 2f);

            // [008-E] Create fog sheet (white = unexplored, black = explored)
            _fogSheet = new Texture2D(_sheetWidth, _sheetHeight, TextureFormat.RFloat, false);
            _fogPixels = new Color[_sheetWidth * _sheetHeight];

            // Initialize all as unexplored (white)
            for (int i = 0; i < _fogPixels.Length; i++)
            {
                _fogPixels[i] = Color.white;
            }

            _fogSheet.SetPixels(_fogPixels);
            _fogSheet.Apply();
        }

        /// <summary>
        /// Update fog revealing based on vision circle.
        /// Called each frame or periodically.
        /// </summary>
        public void UpdateFogReveal(Vector3 visionSourcePos, float visionRange, float revealSoftness = 1f)
        {
            if (_fogSheet == null)
                return;

            // [008-E] World pos → sheet pixel coordinates
            Vector2 sheetPos = WorldToSheetCoords(new Vector2(visionSourcePos.x, visionSourcePos.z));
            float revealRadiusPixels = visionRange * _pixelsPerUnit;

            // [008-E] Reveal pixels within vision range
            for (int y = 0; y < _sheetHeight; y++)
            {
                for (int x = 0; x < _sheetWidth; x++)
                {
                    float pixelDistance = Vector2.Distance(sheetPos, new Vector2(x, y));
                    
                    if (pixelDistance < revealRadiusPixels)
                    {
                        // [008-E] Smooth reveal with falloff
                        float falloff = 1f - Mathf.Clamp01(pixelDistance / revealRadiusPixels);
                        int pixelIndex = y * _sheetWidth + x;
                        
                        // Lerp towards black (explored)
                        _fogPixels[pixelIndex] = Color.Lerp(_fogPixels[pixelIndex], Color.black, falloff);
                    }
                }
            }

            _fogSheet.SetPixels(_fogPixels);
            _fogSheet.Apply();
        }

        /// <summary>
        /// Convert world position to fog sheet pixel coordinates.
        /// </summary>
        private Vector2 WorldToSheetCoords(Vector2 worldPos)
        {
            return (worldPos - _mapOrigin) * _pixelsPerUnit;
        }

        /// <summary>
        /// Get current fog sheet texture.
        /// Can be assigned to shader for real-time update.
        /// </summary>
        public Texture2D GetFogSheet() => _fogSheet;

        /// <summary>
        /// Clear all fog (reveal entire map).
        /// </summary>
        public void RevealAll()
        {
            for (int i = 0; i < _fogPixels.Length; i++)
            {
                _fogPixels[i] = Color.black;
            }
            _fogSheet.SetPixels(_fogPixels);
            _fogSheet.Apply();
        }

        /// <summary>
        /// Restore all fog (hide entire map).
        /// </summary>
        public void ConcealAll()
        {
            for (int i = 0; i < _fogPixels.Length; i++)
            {
                _fogPixels[i] = Color.white;
            }
            _fogSheet.SetPixels(_fogPixels);
            _fogSheet.Apply();
        }

        /// <summary>
        /// Cleanup texture resources.
        /// </summary>
        public void Dispose()
        {
            if (_fogSheet != null)
            {
                Object.Destroy(_fogSheet);
            }
        }
    }
}
