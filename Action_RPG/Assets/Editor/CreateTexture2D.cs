using UnityEngine;
using UnityEditor;
using System.IO;

public class CreateWhiteTexture
{
    [MenuItem("Assets/Create/White-base Texture")]
    public static void CreateWhiteTextureAsset()
    {
        int size = 512;

        Texture2D texture = new Texture2D(size, size, TextureFormat.RGBA32, false);

        Color[] pixels = new Color[size * size];
        for (int i = 0; i < pixels.Length; i++)
        {
            pixels[i] = Color.white;
        }

        texture.SetPixels(pixels);
        texture.Apply();

        byte[] pngData = texture.EncodeToPNG();
        File.WriteAllBytes("Assets/White-base.png", pngData);

        AssetDatabase.Refresh();
    }
}
