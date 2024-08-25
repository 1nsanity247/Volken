using UnityEngine;

[ExecuteInEditMode]
public class NoiseVisualizer : MonoBehaviour
{
    [Header("View Settings")]
    public float scale;
    public Vector3 offset;
    [Header("Noise Settings")]
    public int resolution = 256;
    [Range(1, 64)]
    public int numCells = 4;
    [Range(1,8)]
    public int octaves = 1;
    [Range(0.0f, 1.0f)]
    public float gain = 0.5f;
    [Range(1.0f, 5.0f)]
    public float lacunarity = 2.0f;
    [Button("GenerateNoise", "Generate Noise")]
    public float field;
    
    public ComputeShader compute;

    private Material mat;
    private RenderTexture tex;
    private CloudNoise noise;

    private void Init()
    {
        if(mat == null) {
            mat = new Material(Shader.Find("Hidden/NoiseVisualizer"));
        }
        if(noise == null) { 
            noise = new CloudNoise(0, compute);
        }
    }

    public void GenerateNoise()
    {
        Init();

        if (tex != null) {
            tex.Release();
        }

        tex = noise.GetWhorleyFBM3D(resolution, numCells, octaves, gain, lacunarity);
        mat.SetTexture("Tex", tex);
    }

    private void OnRenderImage(RenderTexture source, RenderTexture destination)
    {
        mat.SetFloat("scale", scale);
        mat.SetVector("offset", offset);
        mat.SetFloat("aspect", (float)source.width / source.height);
        
        Graphics.Blit(source, destination, mat);
    }

    private void OnDestroy()
    {
        if(tex != null) {
            tex.Release();
        }
    }
}
