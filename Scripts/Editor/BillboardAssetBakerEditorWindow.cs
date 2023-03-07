using UnityEditor;
using UnityEngine;

namespace BillboardAssetBaker.Editor
{
    /// <summary>
    /// The Editor Window for BillboardAssetBaker.
    /// </summary>
    public class BillboardAssetBakerEditorWindow : EditorWindow
    {
        private static Vector2 scrollPosition;
        private static GameObject prefab;
        private static bool powerOfTwo = true;
        private static Vector2 textureAtlasSize = Vector2.zero;
        private static Texture2D textureAtlas;
        private static Texture2D normalmap;
        private static Material billboardMaterial;
        private static BillboardAsset billboardAsset;

        private static BakeSettings settings = new BakeSettings()
        {
            width = 128,
            height = 256,
            pack = true,
            png = false
        };

        private const string TextureInfo1 = "The texture atlas is made up of eight textures arranged in two rows with four columns.";
        private const string TextureInfo2 = "It is recommended to use a power of 2 texture size. It is also recommended to have the texture height set to double that of the texture width, as this makes the texture atlas square.";

        [MenuItem("Window/BillboardAssetBaker")]
        private static void Init()
        {
            BillboardAssetBakerEditorWindow window = GetWindow(typeof( BillboardAssetBakerEditorWindow)) as BillboardAssetBakerEditorWindow;
            window.titleContent = new GUIContent("BillboardAssetBaker");
            window.minSize = new Vector2(500,600);
            window.Show();
        }

        private void OnGUI()
        {
            scrollPosition = EditorGUILayout.BeginScrollView(scrollPosition);
            
            GUILayout.Label("Settings", EditorStyles.boldLabel);

            EditorGUI.BeginChangeCheck();
            prefab = EditorGUILayout.ObjectField(
                new GUIContent("Prefab"), 
                prefab, 
                typeof(GameObject),
                false) 
                as GameObject;
            if (EditorGUI.EndChangeCheck())
            {
                textureAtlas = null;
                normalmap = null;
                billboardMaterial = null;
                billboardAsset = null;
            }

            EditorGUILayout.Space();
            
            EditorGUILayout.HelpBox($"{TextureInfo1}\n\n{TextureInfo2}", MessageType.Info, true);
            powerOfTwo = EditorGUILayout.Toggle(
                new GUIContent("Power of 2", "Forces the texture width and height to be powers of 2."), 
                powerOfTwo);
            settings.width = EditorGUILayout.IntSlider(
                new GUIContent("Width", "The width of a single texture in the texture atlas."), 
                settings.width, 32, 256);
            settings.height = EditorGUILayout.IntSlider(
                new GUIContent("Height", "The height of a single texture in the texture atlas."), 
                settings.height, 64, 512);
            if (powerOfTwo)
            {
                settings.width = Mathf.ClosestPowerOfTwo(settings.width);
                settings.height = Mathf.ClosestPowerOfTwo(settings.height);
            }

            textureAtlasSize.Set(settings.width * 4, settings.height * 2);
            
            EditorGUI.BeginDisabledGroup(true);
            EditorGUILayout.Vector2Field(
                new GUIContent(
                    "Texture atlas size", 
                    "The size of the generated texture atlas."), 
                textureAtlasSize);
            EditorGUI.EndDisabledGroup();
            
            settings.pack = EditorGUILayout.ToggleLeft(
                new GUIContent(
                    "Pack assets", 
                    "Pack the material and textures into the BillboardAsset."), 
                settings.pack);
            EditorGUI.indentLevel++;
            EditorGUI.BeginDisabledGroup(settings.pack);
            settings.png = EditorGUILayout.ToggleLeft(
                new GUIContent(
                    "Save as .png", 
                    "Write textures to disk as .png files and then import them as assets. This is only available if not packing the assets."), 
                settings.png);
            EditorGUI.EndDisabledGroup();
            EditorGUI.indentLevel--;
            
            EditorGUILayout.Space();

            if (GUILayout.Button(new GUIContent("Bake"), GUILayout.Height(30)))
            {
                Baker.Bake(in prefab, settings, out textureAtlas, out normalmap, out billboardMaterial, out billboardAsset);
            }

            EditorGUILayout.Space();
            
            GUILayout.Label("Generated assets (read-only)", EditorStyles.boldLabel);
            EditorGUI.BeginDisabledGroup(true);
            textureAtlas = EditorGUILayout.ObjectField(
                new GUIContent("Texture atlas"), 
                textureAtlas, 
                typeof(Texture2D),
                false, 
                GUILayout.Height(200)) 
                as Texture2D;
            EditorGUILayout.Space();
            normalmap = EditorGUILayout.ObjectField(
                new GUIContent("Normalmap"), 
                normalmap, 
                typeof(Texture2D),
                false, 
                GUILayout.Height(200)) 
                as Texture2D;
            EditorGUILayout.Space();
            billboardMaterial = EditorGUILayout.ObjectField(
                new GUIContent("Material"), 
                billboardMaterial, 
                typeof(Material), 
                false) 
                as Material;
            EditorGUILayout.Space();
            billboardAsset = EditorGUILayout.ObjectField(
                new GUIContent("BillboardAsset"), 
                billboardAsset, 
                typeof(BillboardAsset), 
                false) 
                as BillboardAsset;
            EditorGUI.EndDisabledGroup();

            EditorGUILayout.EndScrollView();
        }
    }
    
}
