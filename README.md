

https://github.com/user-attachments/assets/3e002e1d-6627-4f78-9494-0dc68d8de98f
# procedural-grass-unity

A Unity URP grass system that generates all blade geometry on the GPU in parallel using a compute shader, one thread per source triangle, output streamed to an append buffer, drawn in a single indirect draw call. Runs at 300+ FPS.

## Features

- **Fully parallel blade generation** on the GPU via compute shader
- **Procedural wind animation** sampled per-blade in world space
- **Customizable blade appearance** — width, height, curvature, tip taper, and color gradient
- **Per-blade shading** with directional light + ambient, including wind-based shading variation
- **Distance-based LOD** — blades smoothly simplify as they get farther from the camera
- **Adjustable blade resolution** — control segment count to balance quality vs performance
