// MIT License
// Copyright (c) 2020 NedMakesGames
// Modified for procedural flower quad rendering

using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

[ExecuteInEditMode]
public class ProceduralFlowerRenderer : MonoBehaviour {
    [System.Serializable]
    public class FlowerSettings {
        [Tooltip("Flower size")]
        public float flowerSize = 1;
        [Tooltip("The variance of the flower sizes")]
        public float flowerSizeVariance = 1;
        public Texture2D windNoiseTexture = null;
        [Tooltip("The scale of the wind texture")]
        public float windTextureScale = 1;
        [Tooltip("A multiplier to time when creating the wind texture UV")]
        public float windPeriod = 1;
        [Tooltip("A multiplier to world space XZ when creating the wind texture UV")]
        public float windScale = 1;
        [Tooltip("The maximim wind offset length")]
        public float windAmplitude = 0;
        [Tooltip("The minimum distance from the camera before blades will begin to be simplified")]
        public float cameraLODMin = 3;
        [Tooltip("The distance from the camera at which blades will be most simplified")]
        public float cameraLODMax = 30;
        [Tooltip("Controls how quickly blades are simplified")]
        public float cameraLODFactor = 1;

        [Tooltip("Fraction of triangles that spawn a flower (0-1)")]
        [Range(0f, 1f)]
        public float flowerDensity = 0.05f;

        [Tooltip("Random seed — change to get a different flower arrangement")]
        public float seed = 0f;
    }

    [Tooltip("A mesh to create flowers from. A quad sprouts from the center of every triangle")]
    [SerializeField] private Mesh sourceMesh = default;
    [Tooltip("The flower geometry creating compute shader")]
    [SerializeField] private ComputeShader flowerComputeShader = default;
    [Tooltip("The material to render the flower mesh")]
    [SerializeField] private Material material = default;

    [SerializeField] private FlowerSettings flowerSettings = default;

    [System.Runtime.InteropServices.StructLayout(System.Runtime.InteropServices.LayoutKind.Sequential)]
    private struct SourceVertex {
        public Vector3 position;
    }

    private bool initialized;
    private ComputeBuffer sourceVertBuffer;
    private ComputeBuffer sourceTriBuffer;
    private ComputeBuffer drawBuffer;
    private ComputeBuffer argsBuffer;
    
    private ComputeShader instantiatedFlowerComputeShader;
    private Material instantiatedMaterial;
    
    private int idFlowerKernel;
    private int dispatchSize;
    private Bounds localBounds;

    private const int SOURCE_VERT_STRIDE = sizeof(float) * 3;
    private const int SOURCE_TRI_STRIDE = sizeof(int);
    
    // NEW STRIDE: positionWS (3) + (normalWS (3) + uv (2) + windStrength (1)) * 3 vertices per triangle
    private const int DRAW_STRIDE = sizeof(float) * (3 + (3 + 2 + 1) * 3);
    private const int INDIRECT_ARGS_STRIDE = sizeof(int) * 4;
    
    // A flower quad is comprised of exactly 2 triangles
    private const int maxFlowerTriangles = 2; 

    private int[] argsBufferReset = new int[] { 0, 1, 0, 0 };

    private void OnEnable() {
        Debug.Assert(flowerComputeShader != null, "The flower compute shader is null", gameObject);
        Debug.Assert(material != null, "The material is null", gameObject);

        if(initialized) {
            OnDisable();
        }
        initialized = true;

        instantiatedFlowerComputeShader = Instantiate(flowerComputeShader);
        instantiatedMaterial = Instantiate(material);

        Vector3[] positions = sourceMesh.vertices;
        int[] tris = sourceMesh.triangles;

        SourceVertex[] vertices = new SourceVertex[positions.Length];
        for(int i = 0; i < vertices.Length; i++) {
            vertices[i] = new SourceVertex() {
                position = positions[i],
            };
        }

        int numSourceTriangles = tris.Length / 3; 

        sourceVertBuffer = new ComputeBuffer(vertices.Length, SOURCE_VERT_STRIDE, ComputeBufferType.Structured, ComputeBufferMode.Immutable);
        sourceVertBuffer.SetData(vertices);
        sourceTriBuffer = new ComputeBuffer(tris.Length, SOURCE_TRI_STRIDE, ComputeBufferType.Structured, ComputeBufferMode.Immutable);
        sourceTriBuffer.SetData(tris);
        drawBuffer = new ComputeBuffer(numSourceTriangles * maxFlowerTriangles, DRAW_STRIDE, ComputeBufferType.Append);
        drawBuffer.SetCounterValue(0);
        argsBuffer = new ComputeBuffer(1, INDIRECT_ARGS_STRIDE, ComputeBufferType.IndirectArguments);

        idFlowerKernel = instantiatedFlowerComputeShader.FindKernel("Main");

        // Set properties updated for flowers
        instantiatedFlowerComputeShader.SetBuffer(idFlowerKernel, "_SourceVertices", sourceVertBuffer);
        instantiatedFlowerComputeShader.SetBuffer(idFlowerKernel, "_SourceTriangles", sourceTriBuffer);
        instantiatedFlowerComputeShader.SetBuffer(idFlowerKernel, "_DrawTriangles", drawBuffer);
        instantiatedFlowerComputeShader.SetBuffer(idFlowerKernel, "_IndirectArgsBuffer", argsBuffer);
        instantiatedFlowerComputeShader.SetInt("_NumSourceTriangles", numSourceTriangles);
        
        // Flower structural parameters
        instantiatedFlowerComputeShader.SetFloat("_FlowerSize", flowerSettings.flowerSize);
        instantiatedFlowerComputeShader.SetFloat("_FlowerSizeVariance", flowerSettings.flowerSizeVariance);
        instantiatedFlowerComputeShader.SetFloat("_FlowerDensity", flowerSettings.flowerDensity);
        instantiatedFlowerComputeShader.SetFloat("_Seed", flowerSettings.seed);
        
        // Wind parameters
        instantiatedFlowerComputeShader.SetTexture(idFlowerKernel, "_WindNoiseTexture", flowerSettings.windNoiseTexture);
        instantiatedFlowerComputeShader.SetFloat("_WindTexMult", flowerSettings.windTextureScale);
        instantiatedFlowerComputeShader.SetFloat("_WindTimeMult", flowerSettings.windPeriod);
        instantiatedFlowerComputeShader.SetFloat("_WindPosMult", flowerSettings.windScale);
        instantiatedFlowerComputeShader.SetFloat("_WindAmplitude", flowerSettings.windAmplitude);
        
        instantiatedFlowerComputeShader.SetVector("_CameraLOD",
            new Vector4(flowerSettings.cameraLODMin, flowerSettings.cameraLODMax, Mathf.Max(0, flowerSettings.cameraLODFactor), 0));

        instantiatedMaterial.SetBuffer("_DrawTriangles", drawBuffer);

        instantiatedFlowerComputeShader.GetKernelThreadGroupSizes(idFlowerKernel, out uint threadGroupSize, out _, out _);
        dispatchSize = Mathf.CeilToInt((float)numSourceTriangles / threadGroupSize);

        localBounds = sourceMesh.bounds;
        localBounds.Expand(flowerSettings.flowerSize + flowerSettings.flowerSizeVariance);
    }

    private void OnDisable() {
        if(initialized) {
            if(Application.isPlaying) {
                Destroy(instantiatedFlowerComputeShader);
                Destroy(instantiatedMaterial);
            } else {
                DestroyImmediate(instantiatedFlowerComputeShader);
                DestroyImmediate(instantiatedMaterial);
            }
            sourceVertBuffer.Release();
            sourceTriBuffer.Release();
            drawBuffer.Release();
            argsBuffer.Release();
        }
        initialized = false;
    }

    public Bounds TransformBounds(Bounds boundsOS) {
        var center = transform.TransformPoint(boundsOS.center);

        var extents = boundsOS.extents;
        var axisX = transform.TransformVector(extents.x, 0, 0);
        var axisY = transform.TransformVector(0, extents.y, 0);
        var axisZ = transform.TransformVector(0, extents.z, 0);

        extents.x = Mathf.Abs(axisX.x) + Mathf.Abs(axisY.x) + Mathf.Abs(axisZ.x);
        extents.y = Mathf.Abs(axisX.y) + Mathf.Abs(axisY.y) + Mathf.Abs(axisZ.y);
        extents.z = Mathf.Abs(axisX.z) + Mathf.Abs(axisY.z) + Mathf.Abs(axisZ.z);

        return new Bounds { center = center, extents = extents };
    }

    private void LateUpdate() {
        if(Application.isPlaying == false) {
            OnDisable();
            OnEnable();
        }

        drawBuffer.SetCounterValue(0);
        argsBuffer.SetData(argsBufferReset);

        Bounds bounds = TransformBounds(localBounds);

        instantiatedFlowerComputeShader.SetVector("_Time", new Vector4(0, Time.timeSinceLevelLoad, 0, 0));
        instantiatedFlowerComputeShader.SetMatrix("_LocalToWorld", transform.localToWorldMatrix);
        instantiatedFlowerComputeShader.SetVector("_CameraPosition", Camera.main.transform.position);

        instantiatedFlowerComputeShader.Dispatch(idFlowerKernel, dispatchSize, 1, 1);

        Graphics.DrawProceduralIndirect(instantiatedMaterial, bounds, MeshTopology.Triangles, argsBuffer, 0,
            null, null, ShadowCastingMode.Off, true, gameObject.layer);
    }
}