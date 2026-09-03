using UnityEditor;
using UnityEngine;

namespace CloudWhale.Editor
{
    /// <summary>Creates serialized textured materials so their built-in shader is retained by Web builds.</summary>
    public static class GeneratePresentationMaterials
    {
        private static readonly string[] TextureNames = { "whale-skin", "meadow", "island-soil", "warm-wood" };

        [MenuItem("CloudWhale/Generate Presentation Materials #F10")]
        public static void Generate()
        {
            const string materialsDirectory = "Assets/Resources/Materials";
            if (!AssetDatabase.IsValidFolder(materialsDirectory)) AssetDatabase.CreateFolder("Assets/Resources", "Materials");

            var shader = Shader.Find("Standard");
            if (shader == null) throw new System.InvalidOperationException("The built-in Standard shader is unavailable.");

            foreach (var textureName in TextureNames)
            {
                var texture = AssetDatabase.LoadAssetAtPath<Texture2D>($"Assets/Resources/Textures/{textureName}.png");
                if (texture == null) throw new System.InvalidOperationException($"Missing texture: {textureName}");

                var materialPath = $"{materialsDirectory}/{textureName}.mat";
                var material = AssetDatabase.LoadAssetAtPath<Material>(materialPath);
                if (material == null)
                {
                    material = new Material(shader) { name = textureName };
                    AssetDatabase.CreateAsset(material, materialPath);
                }

                material.shader = shader;
                material.mainTexture = texture;
                material.color = Color.white;
                EditorUtility.SetDirty(material);
            }

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
        }
    }
}
