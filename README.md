# procedural-grass-unity
A Unity URP grass system that generates all blade geometry on the GPU in parallel using a compute shader, one thread per source triangle, output streamed to an append buffer, drawn in a single indirect draw call. Includes procedural wind animation.
