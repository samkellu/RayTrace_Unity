using System.Collections;
using System.Collections.Generic;
using UnityEngine;

[System.Serializable]
public struct Sphere {
    public Vector3 position;
    public float radius;
    public Vector3 colour;
    public Vector3 emissionColour;
    public float emissionStrength;
}

public class RayTraceCS : MonoBehaviour
{

    public ComputeShader shader;
    public int bounces;
    public Sphere[] spheres;
    public int numRays;
    public int width;
    public int height;
    ComputeBuffer sphereBuffer;

    public void OnRenderImage(RenderTexture src, RenderTexture dest) {

        float planeHeight = Camera.current.nearClipPlane * Mathf.Tan(Camera.current.fieldOfView * 0.5f * Mathf.Deg2Rad) * 2;
        float planeWidth = planeHeight * Camera.current.aspect;

        shader.SetMatrix("_CameraToWorld", Camera.current.transform.localToWorldMatrix);
        shader.SetVector("_ViewParams", new Vector3(planeWidth, planeHeight, Camera.current.nearClipPlane));

        shader.SetInt("_Bounces", bounces);
        shader.SetInt("_NumRays", numRays);
        shader.SetInt("_NumSpheres", spheres.Length);

        sphereBuffer = new ComputeBuffer(spheres.Length, sizeof(float) * 11);
        sphereBuffer.SetData(spheres);
        shader.SetBuffer(0, "_Spheres", sphereBuffer);

        RenderTexture rt = new RenderTexture(width, height, 0);
        rt.enableRandomWrite = true;
        rt.Create();
        shader.SetTexture(0, "Result", rt);

        int threadsX = Mathf.CeilToInt(width / 8.0f);
        int threadsY = Mathf.CeilToInt(height / 8.0f);
        shader.Dispatch(0, threadsX, threadsY, 1);

        Graphics.Blit(rt, dest);
        rt.Release();
        sphereBuffer.Release();
    }
}
