#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering.Universal;
using Game.Features.Vision.Data;
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
            // [008-G] Find or load PC_Renderer asset
            UniversalRendererData rendererData = AssetDatabase.LoadAssetAtPath<UniversalRendererData>(
                "Assets/Settings/PC_Renderer.asset"
            );

            if (rendererData == null)
            {
                EditorUtility.DisplayDialog(
                    "Fog of War Setup",
                    "PC_Renderer.asset not found at Assets/Settings/PC_Renderer.asset",
                    "OK"
                );
                return;
            }

            // [008-G] Check if FogOfWarFeature already exists
            foreach (var feature in rendererData.rendererFeatures)
            {
                if (feature is FogOfWarFeature)
                {
                    EditorUtility.DisplayDialog(
                        "Fog of War Setup",
                        "FogOfWarFeature is already added to PC_Renderer!",
                        "OK"
                    );
                    return;
                }
            }

            // [008-G] Create FogOfWarFeature instance
            FogOfWarFeature fogFeature = ScriptableObject.CreateInstance<FogOfWarFeature>();
            fogFeature.name = "FogOfWarFeature";

            // [008-G] Load or create VisionConfig
            VisionConfig visionConfig = AssetDatabase.LoadAssetAtPath<VisionConfig>(
                "Assets/_Configs/VisionConfig.asset"
            );

            if (visionConfig == null)
            {
                EditorUtility.DisplayDialog(
                    "Fog of War Setup",
                    "VisionConfig not found at Assets/_Configs/VisionConfig.asset\n" +
                    "Please create it first via: Assets/Create/Game/Vision/Vision Config",
                    "OK"
                );
                ScriptableObject.DestroyImmediate(fogFeature);
                return;
            }

            // [008-G] Assign config to feature (via reflection since we can't set it directly)
            var configField = typeof(FogOfWarFeature).GetField("_visionConfig", 
                System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            
            if (configField != null)
            {
                configField.SetValue(fogFeature, visionConfig);
            }

            // [008-G] Add feature to renderer
            rendererData.rendererFeatures.Add(fogFeature);

            // [008-G] Save changes
            EditorUtility.SetDirty(rendererData);
            AssetDatabase.AddObjectToAsset(fogFeature, rendererData);
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            EditorUtility.DisplayDialog(
                "Fog of War Setup",
                "FogOfWarFeature successfully added to PC_Renderer!\n" +
                "Remember to:\n" +
                "1. Set EnableFogOfWar = true in VisionConfig\n" +
                "2. Configure FogColor and FogEdgeSoftness\n" +
                "3. Play the game to test",
                "OK"
            );

            Debug.Log("[008-G] FogOfWarFeature added to PC_Renderer.asset");
        }

        /// <summary>
        /// Alternative: Setup on Mobile_Renderer
        /// </summary>
        [MenuItem("Tools/Vision System/Setup Fog of War on Mobile Renderer")]
        public static void SetupFogOfWarMobileFeature()
        {
            UniversalRendererData rendererData = AssetDatabase.LoadAssetAtPath<UniversalRendererData>(
                "Assets/Settings/Mobile_Renderer.asset"
            );

            if (rendererData == null)
            {
                EditorUtility.DisplayDialog(
                    "Fog of War Setup",
                    "Mobile_Renderer.asset not found at Assets/Settings/Mobile_Renderer.asset",
                    "OK"
                );
                return;
            }

            // Check if already added
            foreach (var feature in rendererData.rendererFeatures)
            {
                if (feature is FogOfWarFeature)
                {
                    EditorUtility.DisplayDialog(
                        "Fog of War Setup",
                        "FogOfWarFeature is already added to Mobile_Renderer!",
                        "OK"
                    );
                    return;
                }
            }

            // Same process as PC renderer
            FogOfWarFeature fogFeature = ScriptableObject.CreateInstance<FogOfWarFeature>();
            fogFeature.name = "FogOfWarFeature";

            VisionConfig visionConfig = AssetDatabase.LoadAssetAtPath<VisionConfig>(
                "Assets/_Configs/VisionConfig.asset"
            );

            if (visionConfig == null)
            {
                EditorUtility.DisplayDialog(
                    "Fog of War Setup",
                    "VisionConfig not found at Assets/_Configs/VisionConfig.asset",
                    "OK"
                );
                ScriptableObject.DestroyImmediate(fogFeature);
                return;
            }

            var configField = typeof(FogOfWarFeature).GetField("_visionConfig", 
                System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            
            if (configField != null)
            {
                configField.SetValue(fogFeature, visionConfig);
            }

            rendererData.rendererFeatures.Add(fogFeature);
            EditorUtility.SetDirty(rendererData);
            AssetDatabase.AddObjectToAsset(fogFeature, rendererData);
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            Debug.Log("[008-G] FogOfWarFeature added to Mobile_Renderer.asset");
        }
    }
}
#endif
