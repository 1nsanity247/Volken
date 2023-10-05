using ModApi.Packages.FastNoise;
using UnityEngine;

public class NoiseVisualizer : MonoBehaviour
{
    public float scale;
    public Vector2 offset;

    private Material mat;
    private Texture2D tex;

    void Start()
    {
        mat = new Material(Shader.Find("Hidden/NoiseVisualizer"));

        int resolution = 512;

        FastNoise fastNoise = new FastNoise();
        fastNoise.SetNoiseType(NoiseType.ValueFractal);
        fastNoise.SetFractalType(FractalType.FBM);
        fastNoise.SetFrequency(5.0f);
        fastNoise.SetFractalOctaves(8);
        fastNoise.SetFractalGain(0.5f);
        fastNoise.SetFractalLacunarity(2.0f);

        Color[] data = new Color[resolution * 2 * resolution];

        for (int y = 0; y < resolution; y++)
        {
            for (int x = 0; x < 2 * resolution; x++)
            {
                float lat = (((float)y / resolution) - 0.5f) * Mathf.PI;
                float lon = (float)x / resolution * Mathf.PI;

                Vector3 pos = new Vector3(Mathf.Cos(lon) * Mathf.Cos(lat), Mathf.Sin(lat), Mathf.Sin(lon) * Mathf.Cos(lat));

                data[x + y * 2 * resolution].r = (float)fastNoise.GetNoise(pos.x, pos.y, pos.z);
            }
        }

        tex = new Texture2D(2 * resolution, resolution, TextureFormat.RFloat, false);
        tex.wrapMode = TextureWrapMode.Repeat;
        tex.SetPixels(data);
        tex.Apply();
    }

    private void OnRenderImage(RenderTexture source, RenderTexture destination)
    {
        mat.SetFloat("scale", scale);
        mat.SetVector("offset", offset);
        mat.SetTexture("Tex", tex);
        
        Graphics.Blit(source, destination, mat);
    }
}
