# URP Water Reflection for Unity 6 RenderGraph

This repository contains a **Unity 6 (URP 17+) RenderGraph** implementation of a water reflection rendering feature.
It generates a reflection texture by rendering the scene from a mirrored camera viewpoint, which can be used in water shaders.

This project is a port and optimization of the original work by [rngtm](https://github.com/rngtm).

## Features & Improvements

This fork focuses on full compatibility with **Unity 6 RenderGraph**:

* **Full RenderGraph Support**: Completely rewritten using the RenderGraph API. Compatibility Mode dependencies have been removed.
* **Optimized Memory Usage**: 
    * Uses `GraphicsFormat.None` for the depth buffer to minimize VRAM usage and bandwidth.
    * Efficiently manages transient resources within the RenderGraph pipeline.
* **Exposed Depth Texture**: 
    * Both `_CameraReflectionTexture` (Color) and `_CameraReflectionDepthTexture` (Depth) are exposed globally for shaders.

## Requirements

* Unity 6 (6000.0+)
* Universal Render Pipeline (URP)

## Usage

1.  Add the `WaterReflectionPassFeature.cs` script to your project.
2.  Select your URP Renderer Data asset.
3.  Click **Add Renderer Feature** and select **Water Reflection Pass Feature**.
4.  Configure the settings:
    * **Water Y**: The Y-coordinate of the water surface.
    * **Render Skybox**: Whether to include the skybox in the reflection.
    * **Culling Mask**: Layers to include in the reflection.
    * **Render Object Pass Event**: Typically `BeforeRenderingOpaques`.

### Shader Implementation

In your water shader (HLSL or Shader Graph), you can sample the reflection textures using the following global variables:

* `_CameraReflectionTexture` (Texture2D)
* `_CameraReflectionDepthTexture` (Texture2D)

## Credits / Attribution

This project is based on the excellent work by **rngtm**.

* **Original Repository**: [rngtm/URP-WaterReflectionTest](https://github.com/rngtm/URP-WaterReflectionTest)
* **Original Article (Japanese)**: [【Unity】URPを拡張して水面反射を作ってみた](https://zenn.dev/r_ngtm/articles/urp-water-reflection)

Ported and optimized for Unity 6 RenderGraph by [yuki4080](https://github.com/yuki4080).
