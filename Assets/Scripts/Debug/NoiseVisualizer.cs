using UnityEngine;

public class NoiseVisualizer : MonoBehaviour
{
    public float scale;
    public Vector3 offset;

    private Material mat;
    public ComputeShader compute;
    private Texture3D tex;

    private const int threadGroupSize = 8;

    void Start()
    {
        mat = new Material(Shader.Find("Hidden/NoiseVisualizer"));

        tex = new Texture3D(64, 64, 64, TextureFormat.RFloat, false);
        tex.Apply();

        int handle = compute.FindKernel("CSMain");

        compute.SetInt("resolution", 64);
        compute.SetTexture(handle, "result", tex);

        int numGroups = Mathf.CeilToInt(64 / threadGroupSize);
        compute.Dispatch(handle, numGroups, numGroups, numGroups);
    }

    private void OnRenderImage(RenderTexture source, RenderTexture destination)
    {
        mat.SetFloat("scale", scale);
        mat.SetVector("offset", offset);
        mat.SetTexture("tex", tex);

        Graphics.Blit(source, destination, mat);
    }
}
