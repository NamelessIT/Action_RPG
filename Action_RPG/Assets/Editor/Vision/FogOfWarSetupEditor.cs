#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering.Universal;
using Game.Features.Vision.Rendering;

namespace Game.Features.Vision.Editor
{
    /// <summary>
    /// [008-G] Editor helper for setting up Fog of War feature in URP renderer.
    /// Provides menu options to automatically configure the renderer asset.
    /// </summary>
    public static class FogOfWarSetupEditor
    {
        private const string MenuPath = "Tools/Vision System/Setup Fog of War on PC Renderer";

        [MenuItem(MenuPath)]
        public static void SetupFogOfWarFeature()
        {
            SetupOnRenderer("Assets/Settings/PC_Renderer.asset", "PC_Renderer");
        }

        /// <summary>
        /// Alternative: Setup on Mobile_Renderer
        /// </summary>
        [MenuItem("Tools/Vision System/Setup Fog of War on Mobile Renderer")]
        public static void SetupFogOfWarMobileFeature()
        {
            SetupOnRenderer("Assets/Settings/Mobile_Renderer.asset", "Mobile_Renderer");
        }

        private static void SetupOnRenderer(string rendererPath, string rendererName)
        {
            // [008-G] Load renderer asset
            UniversalRendererData rendererData = AssetDatabase.LoadAssetAtPath<UniversalRendererData>(rendererPath);
            if (rendererData == null)
            {
                EditorUtility.DisplayDialog("Fog of War Setup",
                    $"{rendererName}.asset not found at {rendererPath}", "OK");
                return;
            }

            // [008-G] Check if FogOfWarFeature already exists
            foreach (var feature in rendererData.rendererFeatures)
            {
                if (feature is FogOfWarFeature)
                {
                    EditorUtility.DisplayDialog("Fog of War Setup",
                        $"FogOfWarFeature is already added to {rendererName}!", "OK");
                    return;
                }
            }

            // [008-G] Find FogOfWar shader
            Shader fogShader = Shader.Find("Game/Vision/FogOfWar");
            if (fogShader == null)
            {
                EditorUtility.DisplayDialog("Fog of War Setup",
                    "Shader 'Game/Vision/FogOfWar' not found.\n" +
                    "Make sure FogOfWar.shader exists and compiles correctly.", "OK");
                return;
            }

            // [008-G] Create FogOfWarFeature instance
            FogOfWarFeature fogFeature = ScriptableObject.CreateInstance<FogOfWarFeature>();
            fogFeature.name = "FogOfWarFeature";

            // [008-G] Assign shader via reflection
            var shaderField = typeof(FogOfWarFeature).GetField("_fogShader",
                System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);

            if (shaderField != null)
            {
                shaderField.SetValue(fogFeature, fogShader);
            }

            // [008-G] Add feature to renderer and save
            rendererData.rendererFeatures.Add(fogFeature);
            EditorUtility.SetDirty(rendererData);
            AssetDatabase.AddObjectToAsset(fogFeature, rendererData);
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            EditorUtility.DisplayDialog("Fog of War Setup",
                $"FogOfWarFeature added to {rendererName}!\n\n" +
                "Setup:\n" +
                "1. Set EnableFogOfWar = true in VisionConfig\n" +
                "2. Configure FogColor and FogEdgeSoftness\n" +
                "3. Play to test", "OK");

            Debug.Log($"[008-G] FogOfWarFeature added to {rendererName}.asset");
        }
    }
}
#endif
