# BillboardAssetBaker

A script for baking a 3D object into a [BillboardAsset](https://docs.unity3d.com/ScriptReference/BillboardAsset.html) that can be rendered by a BillboardRenderer to show a lightweight billboard version of the 3D object, e.g. in a LOD group.

### Features

- Works recursively, and maintains position, rotation, and scale for child objects, so even objects with complex hierarchies can be baked.
- Generates a normalmap for more realistic lighting.
- Textures can be saved as native Texture2D assets, or saved to disk as .png files.
- Material and native textures can be packed into the BillboardAsset for tidier organization.
- Supports texture sizes from `128 x 128`  up to `1024 x 1024`.

### Limitations 

- Baking objects containing LOD groups are currently not supported.
- Only works for objects with mesh renderers, other renderers are not supported.
- The BillboardAsset mesh is not cut to exclude empty space, it's just a quadrilateral.

### Known issues

Trees created with the Unity tree creator will have visual artifacts if the billboard shader has alpha cutoff set to higher than 0.5. This is due to how the baker compensates for the tree creator's leaf shaders not writing to alpha.

### Usage

1. Open the main window from Window -> BillboardAssetBaker.
2. Fill in the settings.
3. Press the "Bake"-button.

### Settings

`Power of 2` Forces width and height to be powers of 2.

`Width` The width of a single texture in the texture atlas, in pixels.

`Height` The height of a single texture in the texture atlas, in pixels.

The final texture atlas will be `Width * 4 x Height * 2` pixels in size.

`Pack assets` Packs the assets into the BillboardAsset, otherwise saves them as individual assets.

`Save as .png` Saves textures to disk as .png files, otherwise saves them as native Texture2D assets. Only available if not packing.


GUI, and resulting billboard (left) creatad from a 3D object (right)

![BillboardAssetBaker](https://user-images.githubusercontent.com/17293533/223401551-1b31935e-645f-456e-b9c3-c4e87310f67c.png)

Base texture

![BillboardAssetBakerBaseTexture](https://user-images.githubusercontent.com/17293533/223401594-1b2d3eeb-290f-4297-8f59-70e6b0d0892b.png)

Normalmap

![BillboardAssetBakerNormalmap](https://user-images.githubusercontent.com/17293533/223401614-e3004318-797a-4da5-9a3c-8ad86410d5e0.png)

