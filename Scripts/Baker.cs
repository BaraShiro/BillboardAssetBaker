using System.IO;
using UnityEngine;
using UnityEditor;

namespace BillboardAssetBaker
{
    /// <summary>
    /// Contains settings for the <see cref="Baker"/> class.
    /// </summary>
    public struct BakeSettings
    {
        /// <value>The width of a single texture in the texture atlas.</value>
        public int width;
        /// <value>The height of a single texture in the texture atlas.</value>
        public int height;
        /// <value>
        /// If true packs the material and textures into the <see cref="BillboardAsset"/> asset,
        /// otherwise creates separate assets.
        /// </value>
        public bool pack;
        /// <value>
        /// If true and pack is false the textures are written to disk as <c>.png</c> files and then imported as assets,
        /// otherwise textures are saved as native <see cref="Texture2D"/> assets.
        /// </value>
        public bool png;
    }
    
    /// <summary>
    /// A class for baking a 3D object into a 2D billboard that can be rendered with a <see cref="BillboardRenderer"/>.
    /// </summary>
    public static class Baker
    {
        private const int NumberOfTexCoords = 8;
        private const int NumberOfVertices = 6;
        private const int NumberOfIndices = 12;
        private const int AtlasRows = 2;
        private const int AtlasColumns = 4;
        private const int AtlasTextures = AtlasRows * AtlasColumns;
        private const int RotationStep = 360 / AtlasTextures;
        private const int RenderLayer = 31;
        private const string BillboardShader = "Nature/SpeedTree Billboard";
        private const string NormalShader = "Unlit/ScreenSpaceNormals";
        
        private static readonly int BumpMap = Shader.PropertyToID("_BumpMap");

        /// <summary>
        /// Bakes a 3D object into a 2D billboard.
        /// </summary>
        /// <param name="prefab">The 3D prefab object to be baked.</param>
        /// <param name="settings">Settings used in the baking process.</param>
        /// <param name="textureAtlas">
        /// When this method returns contains a texture atlas representing <paramref name="prefab"/>
        /// viewed from 8 angles. This parameter is passed uninitialized.
        /// </param>
        /// <param name="normalmap">
        /// When this method returns contains a normalmap for <paramref name="textureAtlas"/>.
        /// This parameter is passed uninitialized.
        /// </param>
        /// <param name="billboardMaterial">
        /// When this method returns contains a <see cref="Material"/> using the shader
        /// <c>"Nature/SpeedTree Billboard"</c> with <paramref name="textureAtlas"/> and <paramref name="normalmap"/>.
        /// This parameter is passed uninitialized.
        /// </param>
        /// <param name="billboardAsset">
        /// When this method returns contains a <see cref="BillboardAsset"/> using <paramref name="billboardMaterial"/>
        /// and with parameters set to match the original prefab object in position and size.
        /// This parameter is passed uninitialized.
        /// </param>
        public static void Bake(in GameObject prefab, 
                                BakeSettings settings, 
                                out Texture2D textureAtlas, 
                                out Texture2D normalmap, 
                                out Material billboardMaterial, 
                                out BillboardAsset billboardAsset)
        {
            if (!prefab)
            {
                Debug.LogError($"No prefab object selected!");
                textureAtlas = null;
                normalmap = null;
                billboardMaterial = null;
                billboardAsset = null;
                return;
            }

            Shader billboardShader = Shader.Find(BillboardShader);
            Shader normalShader = Shader.Find(NormalShader);

            if ((!billboardShader) || (!normalShader))
            {
                Debug.LogError($"Can't find shader! Make sure both \"{BillboardShader}\" and \"{NormalShader}\" are in the project.");
                textureAtlas = null;
                normalmap = null;
                billboardMaterial = null;
                billboardAsset = null;
                return;
            }

            Material normalMaterial = new Material(normalShader);
            
            CreateBillboardAsset(in prefab, out billboardAsset);
            
            RenderObjectToAtlas(in billboardAsset, in prefab, settings, out textureAtlas);
            RenderObjectToAtlas(in billboardAsset, in prefab, settings, out normalmap, in normalMaterial);

            Object.DestroyImmediate(normalMaterial);
            
            CreateMaterial(in billboardAsset, in textureAtlas, in normalmap, in billboardShader, settings, out billboardMaterial);
            
            FinalizeBillboard(in billboardAsset, in prefab, in billboardMaterial);
        }
        
        /// <summary>
        /// Render a 3D object into a 2D texture depicting it viewed from 8 different angles,
        /// rotating around the Y axis, starting from positive X axis direction and rotating 45 degrees each step.
        /// </summary>
        /// <param name="asset">The <see cref="BillboardAsset"/> that will use the produced texture.</param>
        /// <param name="objectToRender">The 3D object to be rendered.</param>
        /// <param name="settings">Settings used in the rendering process.</param>
        /// <param name="textureAtlas">
        /// When this method returns contains a texture representing <paramref name="objectToRender"/>
        /// viewed from 8 angles. This parameter is passed uninitialized.
        /// </param>
        /// <param name="normalMaterial">
        /// An optional material, if supplied all materials in <paramref name="objectToRender"/>
        /// are switched for this when rendering.
        /// </param>
        private static void RenderObjectToAtlas(in BillboardAsset asset, 
                                                in GameObject objectToRender, 
                                                BakeSettings settings, 
                                                out Texture2D textureAtlas, 
                                                in Material normalMaterial = null)
        {
            CreateVisualCopy(in objectToRender, out GameObject visualCopy, in normalMaterial);
            GetBounds(in visualCopy, out Bounds bounds);
            
            CreateRenderCamera(in bounds, out Camera renderCamera);
            
            Texture2D[] textures = new Texture2D[AtlasTextures];
            bool fixAlpha = normalMaterial is null;

            for (int i = 0; i < AtlasTextures; i++)
            {
                PositionCamera(in renderCamera, in bounds);
                Render(in renderCamera, settings, fixAlpha, out textures[i]);
                RotateCamera(in renderCamera);
            }

            Object.DestroyImmediate(visualCopy);
            Object.DestroyImmediate(renderCamera.gameObject);
            
            StitchTextureAtlas(in textures, settings, out textureAtlas);


            if (settings.pack)
            {
                textureAtlas.name = normalMaterial ? "Normalmap" : "Base Texture";

                AssetDatabase.AddObjectToAsset(textureAtlas, asset);
            }
            else
            {
                string path = AssetDatabase.GetAssetPath(asset);
                
                if (settings.png)
                {
                    string suffix = normalMaterial ? "Normalmap.png" : "Base Texture.png";
                    string newPath = Path.Combine(Path.GetDirectoryName(path), $"{asset.name} {suffix}");

                    byte[] bytes = textureAtlas.EncodeToPNG();
                    File.WriteAllBytes(newPath, bytes);
                
                    Object.DestroyImmediate(textureAtlas);
                    
                    AssetDatabase.Refresh();

                    if (normalMaterial)
                    {
                        TextureImporter textureImporter = AssetImporter.GetAtPath(newPath) as TextureImporter;
                        if (textureImporter)
                        {
                            textureImporter.textureType = TextureImporterType.NormalMap;
                        }
                    }
                    
                    AssetDatabase.ImportAsset(newPath);
                    textureAtlas = AssetDatabase.LoadAssetAtPath<Texture2D>(newPath);
                }
                else
                {
                    path = Path.Combine(Path.GetDirectoryName(path), $"{asset.name} Base Texture.texture2D");
                    string uniquePath = AssetDatabase.GenerateUniqueAssetPath(path);
                    AssetDatabase.CreateAsset(textureAtlas, uniquePath);
                }
            }
            
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
        }
        
        /// <summary>
        /// Creates a visual copy of a 3D object by creating a new object and copying the mesh renderer and mesh filter,
        /// if present. Works recursively.
        /// </summary>
        /// <param name="prefab">The 3D object to create a copy of.</param>
        /// <param name="visualCopy">
        /// When this method returns contains a visual copy of <paramref name="prefab"/>.
        /// This parameter is passed uninitialized.
        /// </param>
        /// <param name="replacementMaterial">
        /// An optional material, if supplied all materials in <paramref name="prefab"/> are switched for this in the copy.
        /// </param>
        private static void CreateVisualCopy(in GameObject prefab, out GameObject visualCopy, in Material replacementMaterial = null)
        {
            Transform original = prefab.transform;
            visualCopy = new GameObject($"Visual copy of {prefab.name}")
            {
                transform =
                {
                    localPosition = Vector3.zero,
                    localRotation = original.localRotation,
                    localScale = original.localScale
                },
                layer = RenderLayer,
                hideFlags = HideFlags.HideAndDontSave
            };

            if(TryGetRenderComponents(in original, out MeshRenderer meshRenderer, out MeshFilter meshFilter))
            {
                MeshRenderer copyMeshRenderer = visualCopy.AddComponent<MeshRenderer>();
                if (replacementMaterial)
                {
                    Material[] newMaterials = new Material[meshRenderer.sharedMaterials.Length];
                    for (int i = 0; i < newMaterials.Length; i++)
                    {
                        newMaterials[i] = replacementMaterial;
                    }
                    copyMeshRenderer.sharedMaterials = newMaterials;
                }
                else
                {
                    copyMeshRenderer.sharedMaterials = meshRenderer.sharedMaterials;
                }
                
                MeshFilter copyMeshFilter = visualCopy.AddComponent<MeshFilter>();
                copyMeshFilter.sharedMesh = meshFilter.sharedMesh;
            }

            foreach (Transform child in original)
            {
                if (child.gameObject.activeSelf)
                {
                    CreateVisualCopyRecursive(in visualCopy, in child, in replacementMaterial);
                }
            }
        }

        /// <summary>
        /// The recursive part of <see cref="CreateVisualCopy">CreateVisualCopy</see>.
        /// </summary>
        /// <param name="parent">The object to parent the copy to.</param>
        /// <param name="original">The original 3D object to copy.</param>
        /// <param name="replacementMaterial">
        /// An optional material, if supplied all materials in <paramref name="original"/>
        /// are switched for this in the copy.
        /// </param>
        private static void CreateVisualCopyRecursive(in GameObject parent, in Transform original, in Material replacementMaterial = null)
        {
            GameObject visualCopy = new GameObject($"Visual copy of {original.name}")
            {
                transform =
                {
                    parent = parent.transform,
                    localPosition = original.localPosition,
                    localRotation = original.localRotation,
                    localScale = original.localScale
                },
                layer = RenderLayer,
                hideFlags = HideFlags.HideAndDontSave
            };

            if(TryGetRenderComponents(in original, out MeshRenderer meshRenderer, out MeshFilter meshFilter))
            {
                MeshRenderer copyMeshRenderer = visualCopy.AddComponent<MeshRenderer>();
                if (replacementMaterial)
                {
                    Material[] newMaterials = new Material[meshRenderer.sharedMaterials.Length];
                    for (int i = 0; i < newMaterials.Length; i++)
                    {
                        newMaterials[i] = replacementMaterial;
                    }
                    copyMeshRenderer.sharedMaterials = newMaterials;
                }
                else
                {
                    copyMeshRenderer.sharedMaterials = meshRenderer.sharedMaterials;
                }
                
                MeshFilter copyMeshFilter = visualCopy.AddComponent<MeshFilter>();
                copyMeshFilter.sharedMesh = meshFilter.sharedMesh;
            }
            
            foreach (Transform child in original)
            {
                if (child.gameObject.activeSelf)
                {
                    CreateVisualCopyRecursive(in visualCopy, in child, in replacementMaterial);
                }
            }
        }

        /// <summary>
        /// Get the combined bounding box of any <see cref="MeshRenderer"/> attached to the parent object
        /// and all of its children. 
        /// </summary>
        /// <param name="parentObject">The parent object.</param>
        /// <param name="bounds">
        /// When this method returns contains the bounding box of <paramref name="parentObject"/>
        /// and all of its children. This parameter is passed uninitialized.
        /// </param>
        private static void GetBounds(in GameObject parentObject, out Bounds bounds)
        {
            Transform transform = parentObject.transform;
            bounds = new Bounds(transform.position, Vector3.zero);
            MeshRenderer[] renderers = transform.GetComponentsInChildren<MeshRenderer>();
            foreach (MeshRenderer renderer in renderers)
            {
                bounds.Encapsulate(renderer.bounds);
            }
        }
        
        /// <summary>
        /// Tries to get the <see cref="MeshRenderer"/> and <see cref="MeshFilter"/> attached to
        /// <paramref name="transform"/>, and returns if it was successful.
        /// </summary>
        /// <param name="transform">The transform to get the mesh renderer and mesh filter from.</param>
        /// <param name="meshRenderer">
        /// When this method returns contains the mesh renderer attached to transform, or null if none exists.
        /// This parameter is passed uninitialized.
        /// </param>
        /// <param name="meshFilter">When this method returns contains the mesh filter attached to transform,
        /// or null if none exists. This parameter is passed uninitialized.
        /// </param>
        /// <returns>
        /// True if <paramref name="transform"/> has a mesh renderer and mesh filter, otherwise False.
        /// </returns>
        private static bool TryGetRenderComponents(in Transform transform, out MeshRenderer meshRenderer, out MeshFilter meshFilter)
        {
            meshRenderer = transform.GetComponent<MeshRenderer>();
            meshFilter = transform.GetComponent<MeshFilter>();

            return (meshRenderer) && (meshFilter);
        }
        
        /// <summary>
        /// Creates a new camera specifically set up to render a specific object.
        /// </summary>
        /// <param name="bounds">The bounds of the object the camera will render.</param>
        /// <param name="renderCamera">
        /// When this method returns contains the camera. This parameter is passed uninitialized.
        /// </param>
        private static void CreateRenderCamera(in Bounds bounds, out Camera renderCamera)
        {
            renderCamera = new GameObject("RenderCamera").AddComponent<Camera>();
            renderCamera.transform.position = Vector3.zero;
            renderCamera.transform.rotation = Quaternion.Euler(0f, -90f, 0f);
            renderCamera.clearFlags = CameraClearFlags.Depth;
            renderCamera.backgroundColor = new Color(0f, 0f, 0f, 0f);
            renderCamera.cullingMask = 1 << RenderLayer;
            renderCamera.orthographic = true;
            renderCamera.orthographicSize = bounds.extents.y;
            renderCamera.nearClipPlane = 0.01f;
            float diagonal = Mathf.Sqrt(Mathf.Pow(bounds.extents.x, 2) + Mathf.Pow(bounds.extents.z, 2));
            renderCamera.aspect = diagonal / bounds.extents.y;
            renderCamera.renderingPath = RenderingPath.Forward;
            renderCamera.allowMSAA = true;
            renderCamera.gameObject.hideFlags = HideFlags.HideAndDontSave;
        }

        /// <summary>
        /// Position the camera pointing towards the center of the bounds.
        /// </summary>
        /// <param name="camera">The camera to position.</param>
        /// <param name="bounds">The bounds to position towards.</param>
        private static void PositionCamera(in Camera camera, in Bounds bounds)
        {
            Transform cameraTransform = camera.transform;
            cameraTransform.position = bounds.center + (cameraTransform.forward * ((bounds.extents.magnitude + 1f) * -1));
        }

        /// <summary>
        /// Rotates the camera 45 degrees counterclockwise.
        /// </summary>
        /// <param name="camera">The camera to rotate.</param>
        private static void RotateCamera(in Camera camera)
        {
            Transform cameraTransform = camera.transform;
            Vector3 cameraRotation = cameraTransform.rotation.eulerAngles;
            cameraRotation.y -= RotationStep;
            cameraTransform.rotation = Quaternion.Euler(cameraRotation);
        }

        /// <summary>
        /// Renders the camera and writes the result to a texture.
        /// </summary>
        /// <param name="camera">The camera to render from.</param>
        /// <param name="settings">Settings used in the rendering process.</param>
        /// <param name="fixAlpha">If true sets the alpha to 1 for non-black pixels with alpha 0.</param>
        /// <param name="texture">
        /// When this method returns contains a texture containing the rendered image.
        /// This parameter is passed uninitialized.
        /// </param>
        private static void Render(in Camera camera, BakeSettings settings, bool fixAlpha, out Texture2D texture)
        {
            RenderTexture previousActive = RenderTexture.active;
            RenderTexture renderTexture = RenderTexture.GetTemporary(settings.width, settings.height, 16);
            RenderTexture.active = renderTexture;
            GL.Clear(false, true, Vector4.zero);
            camera.targetTexture = renderTexture;

            camera.Render();

            texture = new Texture2D(settings.width, settings.height, TextureFormat.RGBA32, false);
            texture.ReadPixels(new Rect(0, 0, settings.width, settings.height), 0, 0, false);
            
            // Compensate for shaders that don't write to alpha (e.g. "Nature/Tree Creator Leaves")
            if (fixAlpha)
            {
                for (int x = 0; x < settings.width; x++)
                {
                    for (int y = 0; y < settings.height; y++)
                    {
                        Color pixel = texture.GetPixel(x, y);
                        if (pixel is ({r: > 0f} or {g: > 0f} or{b: > 0f}) and {a: 0f})
                        {
                            pixel.a = 1f;
                            texture.SetPixel(x, y, pixel);
                        }
                    }
                }
            }

            texture.Apply(false, false);

            camera.targetTexture = null;
            RenderTexture.active = previousActive;
            RenderTexture.ReleaseTemporary(renderTexture);
        }
        
        /// <summary>
        /// Stitches eight textures into a texture atlas with two rows and four columns.
        /// </summary>
        /// <param name="textures">An array of textures to be stitched into a single texture.</param>
        /// <param name="settings">Settings used for stitching.</param>
        /// <param name="textureAtlas">
        /// When this method returns contains a texture atlas of the textures in <paramref name="textures"/>.
        /// This parameter is passed uninitialized.
        /// </param>
        private static void StitchTextureAtlas(in Texture2D[] textures, BakeSettings settings, out Texture2D textureAtlas)
        {
            textureAtlas = new Texture2D(
                settings.width * AtlasColumns, 
                settings.height * AtlasRows, 
                TextureFormat.RGBA32, 
                false);
            
            for (int row = 0; row < AtlasRows; row++)
            {
                for (int column = 0; column < AtlasColumns; column++)
                {
                    textureAtlas.SetPixels(
                        column * settings.width,
                        row * settings.height,
                        settings.width,
                        settings.height,
                        textures[(row * AtlasColumns) + column].GetPixels());
                }
            }
        }

        /// <summary>
        /// Creates a material that can be used by a <see cref="BillboardAsset"/>.
        /// </summary>
        /// <param name="asset">The BillboardAsset that will use the material.</param>
        /// <param name="textureAtlas">The materials base texture.</param>
        /// <param name="normalmap">The materials normalmap.</param>
        /// <param name="billboardShader">The materials shader.</param>
        /// <param name="settings">Settings for creating the material.</param>
        /// <param name="billboardMaterial">
        /// When this method returns contains a material using <paramref name="billboardShader"/>,
        /// <paramref name="textureAtlas"/>, and <paramref name="normalmap"/>.
        /// This parameter is passed uninitialized.
        /// </param>
        private static void CreateMaterial(in BillboardAsset asset, 
                                           in Texture2D textureAtlas, 
                                           in Texture2D normalmap, 
                                           in Shader billboardShader,  
                                           BakeSettings settings, 
                                           out Material billboardMaterial)
        {
            billboardMaterial = new Material(billboardShader)
            {
                name = "Billboard Material",
                mainTexture = textureAtlas,
                enableInstancing = true
            };
            billboardMaterial.SetTexture(BumpMap, normalmap);

            if (settings.pack)
            {
                AssetDatabase.AddObjectToAsset(billboardMaterial, asset);
            }
            else
            {
                string path = AssetDatabase.GetAssetPath(asset);
                path = Path.Combine(Path.GetDirectoryName(path), $"{asset.name} Material.mat");
                string uniquePath = AssetDatabase.GenerateUniqueAssetPath(path);
                AssetDatabase.CreateAsset(billboardMaterial, uniquePath);
            }
            
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
        }
        
        /// <summary>
        /// Creates a new <see cref="BillboardAsset"/>.
        /// </summary>
        /// <param name="prefab">The prefab object this BillboardAsset is created for.</param>
        /// <param name="billboard">
        /// When this method returns contains a new BillboardAsset. This parameter is passed uninitialized.
        /// </param>
        private static void CreateBillboardAsset(in GameObject prefab, out BillboardAsset billboard)
        {
            billboard = new BillboardAsset();

            Vector4[] texCoords = new Vector4[NumberOfTexCoords];
            Vector2[] vertices = new Vector2[NumberOfVertices];
            ushort[] indices = new ushort[NumberOfIndices];
            
            // left, bottom, width, height
            texCoords[0].Set(0f, 0f, 0.25f, 0.5f);
            texCoords[1].Set(0.25f, 0f, 0.25f, 0.5f);
            texCoords[2].Set(0.5f, 0f, 0.25f, 0.5f);
            texCoords[3].Set(0.75f, 0f, 0.25f, 0.5f);
            texCoords[4].Set(0f, 0.5f, 0.25f, 0.5f);
            texCoords[5].Set(0.25f, 0.5f, 0.25f, 0.5f);
            texCoords[6].Set(0.5f, 0.5f, 0.25f, 0.5f);
            texCoords[7].Set(0.75f, 0.5f, 0.25f, 0.5f);
            
            vertices[0].Set(0f, 0f);
            vertices[1].Set(0f, 0.5f);
            vertices[2].Set(0f, 1f);
            vertices[3].Set(1f, 0f);
            vertices[4].Set(1f, 0.5f);
            vertices[5].Set(1f, 1f);
            
            indices[0] = 4;
            indices[1] = 3;
            indices[2] = 0;
            indices[3] = 1;
            indices[4] = 4;
            indices[5] = 0;
            indices[6] = 5;
            indices[7] = 4;
            indices[8] = 1;
            indices[9] = 2;
            indices[10] = 5;
            indices[11] = 1;
    
            billboard.SetImageTexCoords(texCoords);
            billboard.SetVertices(vertices);
            billboard.SetIndices(indices);
            
            string path = AssetDatabase.GetAssetPath(prefab);
            path = Path.Combine(Path.GetDirectoryName(path), $"{prefab.name} Billboard Asset.asset");
            string uniquePath = AssetDatabase.GenerateUniqueAssetPath(path);
            AssetDatabase.CreateAsset(billboard, uniquePath);
            
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
        }

        /// <summary>
        /// Gives a <see cref="BillboardAsset"/> a material, and sets it up to match an objects position and size.
        /// </summary>
        /// <param name="prefab">The object the BillboardAsset should match.</param>
        /// <param name="billboard">The BillboardAsset to finalize.</param>
        /// <param name="material">The material to give the BillboardAsset.</param>
        private static void FinalizeBillboard(in BillboardAsset billboard, in GameObject prefab, in Material material)
        {
            billboard.material = material;
            
            GetBounds(in prefab, out Bounds bounds);

            billboard.height = bounds.extents.y * 2;
            float diagonal = 2 * Mathf.Sqrt(Mathf.Pow(bounds.extents.x, 2) + Mathf.Pow(bounds.extents.z, 2));
            billboard.width = diagonal;

            float offset = bounds.center.y - prefab.transform.position.y;
            billboard.bottom = (bounds.extents.y - offset) * -1;
        }
    }
}