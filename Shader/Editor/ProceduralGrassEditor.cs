// MIT License - same as the rest of the project

using UnityEngine;
using UnityEditor;
using System.IO;

// This attribute tells Unity to use this class as the inspector for ProceduralGrassRenderer
[CustomEditor(typeof(ProceduralGrassRenderer))]
public class ProceduralGrassRendererEditor : Editor {

    // Noise generation settings, remembered between clicks
    private float noiseScale = 8f;
    private int noiseSeed = 0;
    private const int TEXTURE_SIZE = 512;

    public override void OnInspectorGUI() {
        // Draw the default inspector first (all the normal fields)
        DrawDefaultInspector();

        // Add a separator and label
        EditorGUILayout.Space(10);
        EditorGUILayout.LabelField("Wind Noise Texture Generator", EditorStyles.boldLabel);

        // Inputs for the user to tweak the noise
        noiseScale = EditorGUILayout.Slider("Noise Scale", noiseScale, 1f, 32f);
        noiseSeed = EditorGUILayout.IntField("Seed", noiseSeed);

        // The button
        if (GUILayout.Button("Generate Wind Noise Texture")) {
            GenerateAndAssignNoiseTexture();
        }
    }

    private void GenerateAndAssignNoiseTexture() {
        ProceduralGrassRenderer renderer = (ProceduralGrassRenderer)target;

        // Create a new texture in memory
        Texture2D texture = new Texture2D(TEXTURE_SIZE, TEXTURE_SIZE, TextureFormat.RGBA32, false);

        // Use the seed to offset the Perlin sample so different seeds give different patterns
        System.Random rng = new System.Random(noiseSeed);
        float offsetX = (float)rng.NextDouble() * 10000f;
        float offsetY = (float)rng.NextDouble() * 10000f;

        // Generate two independent noise channels (R and G) so the wind has X/Z direction variation
        // The compute shader samples .xy from this texture to build a wind direction vector
        float offsetX2 = (float)rng.NextDouble() * 10000f;
        float offsetY2 = (float)rng.NextDouble() * 10000f;

        Color[] pixels = new Color[TEXTURE_SIZE * TEXTURE_SIZE];
        for (int y = 0; y < TEXTURE_SIZE; y++) {
            for (int x = 0; x < TEXTURE_SIZE; x++) {
                float u = (float)x / TEXTURE_SIZE;
                float v = (float)y / TEXTURE_SIZE;

                float r = TileablePerlin(u, v, noiseScale, offsetX, offsetY);
                float g = TileablePerlin(u, v, noiseScale, offsetX2, offsetY2);

                pixels[y * TEXTURE_SIZE + x] = new Color(r, g, 0f, 1f);
            }
        }
        texture.SetPixels(pixels);
        texture.Apply();

        // Save the PNG next to this script's folder
        string folderPath = "Assets/Shader";
        if (!AssetDatabase.IsValidFolder(folderPath)) {
            folderPath = "Assets";
        }
        string fileName = "WindNoise.png";
        string fullPath = Path.Combine(folderPath, fileName);

        byte[] pngData = texture.EncodeToPNG();
        File.WriteAllBytes(fullPath, pngData);
        AssetDatabase.Refresh();

        // Set the import settings so the texture is usable for shaders
        TextureImporter importer = AssetImporter.GetAtPath(fullPath) as TextureImporter;
        if (importer != null) {
            importer.textureType = TextureImporterType.Default;
            importer.wrapMode = TextureWrapMode.Repeat;
            importer.filterMode = FilterMode.Bilinear;
            importer.sRGBTexture = false; // noise is data, not color
            importer.textureCompression = TextureImporterCompression.Uncompressed;
            importer.SaveAndReimport();
        }

        // Load the imported asset and assign it to the renderer's nested windNoiseTexture field
        Texture2D loadedTexture = AssetDatabase.LoadAssetAtPath<Texture2D>(fullPath);

        // Access the nested field grassSettings.windNoiseTexture via SerializedProperty path
        SerializedObject so = new SerializedObject(renderer);
        SerializedProperty prop = so.FindProperty("grassSettings.windNoiseTexture");
        if (prop != null) {
            prop.objectReferenceValue = loadedTexture;
            so.ApplyModifiedProperties();
        } else {
            Debug.LogWarning("Could not find grassSettings.windNoiseTexture property. Texture was saved at " + fullPath + " but not auto-assigned.");
        }

        DestroyImmediate(texture);

        Debug.Log($"Generated wind noise texture at {fullPath}");
    }

    // Generates tileable Perlin noise by sampling 4 offset versions and blending them
    private float TileablePerlin(float u, float v, float scale, float offsetX, float offsetY) {
        float x = u * scale;
        float y = v * scale;

        float aa = Mathf.PerlinNoise(x + offsetX,         y + offsetY);
        float ba = Mathf.PerlinNoise(x + offsetX - scale, y + offsetY);
        float ab = Mathf.PerlinNoise(x + offsetX,         y + offsetY - scale);
        float bb = Mathf.PerlinNoise(x + offsetX - scale, y + offsetY - scale);

        float blendX = u;
        float blendY = v;

        float top    = Mathf.Lerp(aa, ba, blendX);
        float bottom = Mathf.Lerp(ab, bb, blendX);
        return Mathf.Lerp(top, bottom, blendY);
    }
}