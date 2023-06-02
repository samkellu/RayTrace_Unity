using System.Collections;
using System.IO;
using System.Text;
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

[System.Serializable]
public struct Triangle {
    public Vector3 p1;
    public Vector3 p2;
    public Vector3 p3;
    public Vector3 colour;
    public Vector3 emissionColour;
    public float emissionStrength;

    public Triangle(Vector3 p1, Vector3 p2, Vector3 p3, Vector3 colour, Vector3 emissionColour, float emissionStrength) {
        this.p1 = p1;
        this.p2 = p2;
        this.p3 = p3;
        this.colour = colour;
        this.emissionColour = emissionColour;
        this.emissionStrength = emissionStrength;
    }
}

[System.Serializable]
public struct Int3 {
    public int x;
    public int y;
    public int z;

    public Int3(int x, int y, int z) {
        this.x = x;
        this.y = y;
        this.z = z;
    }
}

[ExecuteAlways, ImageEffectAllowedInSceneView]
public class RayTraceCS : MonoBehaviour
{

    public Sphere[] spheres;
    public Triangle[] tris;
    public ComputeShader shader;
    public int bounces;
    public int numRays;
    public int width;
    public int height;
    public int frameNum = 0;
    public bool lockFrame;
    public string fp;
    ComputeBuffer sphereBuffer;
    ComputeBuffer triBuffer;
    RenderTexture prevFrame;

    public void Start() {
        prevFrame = new RenderTexture(width, height, 0);
        prevFrame.enableRandomWrite = true;
        prevFrame.Create();
        // tris = FileToTris(fp);
    }

    public Triangle[] FileToTris(string fp) {

        List<Vector3> vertices = new List<Vector3>();
        List<Int3> indices = new List<Int3>();

        using (var fStream = File.OpenRead(fp)) {
            using (var sReader = new StreamReader(fStream, Encoding.UTF8, true, 128)) {
                string line;
                while ((line = sReader.ReadLine()) != null) {
                    string[] components = line.Split(' ');

                    if (components.Length <= 3) {
                        continue;
                    }

                    switch (components[0][0]) {
                        case 'v':
                            Debug.Log(line);
                            vertices.Add(new Vector3(float.Parse(components[1]), float.Parse(components[2]), float.Parse(components[3])));
                            break;

                        case 'f':
                            indices.Add(new Int3(int.Parse(components[1]), int.Parse(components[2]), int.Parse(components[3])));
                            break;
                    }
                }
            }
        }

        Triangle[] tris = new Triangle[indices.Count];
        int count = 0;

        foreach (Int3 i in indices) {
            tris[count++] = new Triangle(vertices[i.x - 1], vertices[i.y - 1], vertices[i.z - 1], new Vector3(1,1,1), new Vector3(1,1,1), 0.0f);
        }

        return tris;
    }

    public void OnRenderImage(RenderTexture src, RenderTexture dest) {

        float planeHeight = Camera.current.nearClipPlane * Mathf.Tan(Camera.current.fieldOfView * 0.5f * Mathf.Deg2Rad) * 2;
        float planeWidth = planeHeight * Camera.current.aspect;

        shader.SetMatrix("_CameraToWorld", Camera.current.transform.localToWorldMatrix);
        shader.SetVector("_ViewParams", new Vector3(planeWidth, planeHeight, Camera.current.nearClipPlane));

        shader.SetInt("_Bounces", bounces);
        shader.SetInt("_NumRays", numRays);

        shader.SetInt("_FrameNum", frameNum++);
        if (lockFrame) {
            shader.SetInt("_FrameNum", 0);
        }
        shader.SetInt("_NumSpheres", spheres.Length);
        shader.SetInt("_NumTris", tris.Length);

        sphereBuffer = new ComputeBuffer(spheres.Length, sizeof(float) * 11);
        sphereBuffer.SetData(spheres);
        shader.SetBuffer(0, "_Spheres", sphereBuffer);

        // triBuffer = new ComputeBuffer(tris.Length, sizeof(float) * 16);
        // triBuffer.SetData(tris);
        // shader.SetBuffer(0, "_Triangles", triBuffer);

        RenderTexture rt = new RenderTexture(width, height, 0);
        rt.enableRandomWrite = true;
        rt.Create();
        shader.SetTexture(0, "Result", rt);
        shader.SetTexture(0, "_PrevFrame", prevFrame);

        int threadsX = Mathf.CeilToInt(width / 8.0f);
        int threadsY = Mathf.CeilToInt(height / 8.0f);
        shader.Dispatch(0, threadsX, threadsY, 1);

        Graphics.Blit(rt, dest);
        rt.Release();
        sphereBuffer.Release();
        // triBuffer.Release();
    }

    public void OnDestroy() {
        prevFrame.Release();
    }
}
