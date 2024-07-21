using Assets.Scripts;
using Assets.Scripts.Noise;
using ModApi.Packages.FastNoise;
using System.Runtime.InteropServices;
using UnityEngine;

public class CloudNoise
{
    private static readonly Vector2Int[] offsets2D =
    {
        new Vector2Int(0, 0), new Vector2Int(1, 0),
        new Vector2Int(0, 1), new Vector2Int(1, 1)
    };

    private int _seed;
    private const int threadGroupSize = 8;
    private ComputeShader _noiseCompute;

    public CloudNoise(int seed = 0)
    {
        _seed = seed;
        _noiseCompute = Mod.Instance.ResourceLoader.LoadAsset<ComputeShader>("Assets/Scripts/Volken/CloudNoiseCompute.compute");
    }

    public RenderTexture GetWhorleyFBM3D(int resolution, int cellCount, int octaves, float gain, float lacunarity)
    {
        RenderTexture tex = new RenderTexture(resolution, resolution, 0, RenderTextureFormat.R8)
        {
            dimension = UnityEngine.Rendering.TextureDimension.Tex3D,
            volumeDepth = resolution,
            wrapMode = TextureWrapMode.Repeat,
            enableRandomWrite = true,
            useMipMap = false
        };

        float norm = (1.0f - gain) / (1.0f - Mathf.Pow(gain, octaves));

        for (int i = 0; i < octaves; i++)
        {
            float weight = Mathf.Pow(gain, i);
            int numCells = Mathf.RoundToInt(cellCount * Mathf.Pow(lacunarity, i));
            
            if (numCells > resolution) {
                break;
            }

            WriteWhorley(ref tex, norm * weight, resolution, numCells);
        }

        return tex;
    }

    private void WriteWhorley(ref RenderTexture tex, float weight, int res, int numCells)
    {
        System.Random rand = new System.Random(_seed);
        Vector3[] cellPoints = new Vector3[numCells * numCells * numCells];
        
        for (int z = 0; z < numCells; z++)
        {
            for (int y = 0; y < numCells; y++)
            {
                for (int x = 0; x < numCells; x++)
                {
                    cellPoints[x + y * numCells + z * numCells * numCells] = GetRandomOffset(rand);
                }
            }
        }

        ComputeBuffer buffer = new ComputeBuffer(numCells * numCells * numCells, Marshal.SizeOf(typeof(Vector3)));
        buffer.SetData(cellPoints);

        int handle = _noiseCompute.FindKernel("Whorley");
        _noiseCompute.SetInt("resolution", res);
        _noiseCompute.SetInt("numCells", numCells);
        _noiseCompute.SetFloat("weight", weight);
        _noiseCompute.SetBuffer(handle, "points", buffer);
        _noiseCompute.SetTexture(handle, "result", tex);

        int numGroups = res / threadGroupSize;
        _noiseCompute.Dispatch(handle, numGroups, numGroups, numGroups);

        buffer.Release();
    }

    public Texture2D GetPlanetMap(int resolution, float frequency, int octaves, float gain, float lacunarity)
    {
        FastNoiseNative fastNoise = new FastNoiseNative(1337);
        fastNoise.SetNoiseType(NoiseType.ValueFractal);
        fastNoise.SetFractalType(FractalType.FBM);
        fastNoise.SetFrequency(frequency);
        fastNoise.SetFractalOctaves(octaves);
        fastNoise.SetFractalGain(gain);
        fastNoise.SetFractalLacunarity(lacunarity);

        Color[] data = new Color[resolution * 2 * resolution];

        for (int y = 0; y < resolution; y++)
        {
            for (int x = 0; x < 2 * resolution; x++)
            {
                float lat = (((float)y / resolution) - 0.5f) * Mathf.PI;
                float lon = (float)x / resolution * Mathf.PI;

                Vector3 pos = new Vector3(Mathf.Cos(lon) * Mathf.Cos(lat), Mathf.Sin(lat), Mathf.Sin(lon) * Mathf.Cos(lat));

                fastNoise.SetFrequency(frequency);
                fastNoise.SetFractalOctaves(octaves);
                float density = (float)fastNoise.GetNoise(pos.x, pos.y, pos.z);
                fastNoise.SetFrequency(4.0f * frequency);
                fastNoise.SetFractalOctaves(octaves / 2);
                float height = (float)fastNoise.GetNoise(pos.x, pos.y, pos.z);

                data[x + y * 2 * resolution] = new Color(density, height, 0.0f);
            }
        }

        Texture2D tex = new Texture2D(2 * resolution, resolution, TextureFormat.RGFloat, false);
        tex.wrapMode = TextureWrapMode.Repeat;
        tex.SetPixels(data);
        tex.Apply();

        return tex;
    }

    public Texture2D GetPerlinFBM2D(int resolution, int cellCount, int octaves, float gain, float lacunarity)
    {
        Color[] data = new Color[resolution * resolution];

        float norm = (1.0f - gain) / (1.0f - Mathf.Pow(gain, octaves));

        for (int i = 0; i < octaves; i++)
        {
            float weight = Mathf.Pow(gain, i);
            int numCells = Mathf.RoundToInt(cellCount * Mathf.Pow(lacunarity, i));

            if (numCells > resolution) break;

            WritePerlin2D(ref data, norm * weight, resolution, numCells, i > 0);
        }

        Texture2D tex = new Texture2D(resolution, resolution, TextureFormat.RFloat, false);
        tex.wrapMode = TextureWrapMode.Repeat;
        tex.SetPixels(data);
        tex.Apply();

        return tex;
    }

    private void WritePerlin2D(ref Color[] data, float weight, int res, int numCells, bool accumulate)
    {
        Vector2[,] gradients = new Vector2[numCells, numCells];
        System.Random rand = new System.Random(_seed);
        int num = 10000;
        float invNum = 1.0f / num;

        for (int x = 0; x < numCells; x++)
        {
            for (int y = 0; y < numCells; y++)
            {
                float phi = rand.Next(0, num) * invNum * 2.0f * Mathf.PI;
                gradients[x, y] = new Vector2(Mathf.Cos(phi), Mathf.Sin(phi));
            }
        }

        float cellSize = res / numCells;
        float[] values = new float[4];

        for (int y = 0; y < res; y++)
        {
            for (int x = 0; x < res; x++)
            {
                Vector2 pos = new Vector2(x, y);
                Vector2Int baseCell = Vector2Int.FloorToInt(pos / cellSize);
                Vector2 fract = pos / cellSize - baseCell;

                for (int i = 0; i < 4; i++)
                {
                    Vector2Int cell = baseCell + offsets2D[i];
                    values[i] = Vector2.Dot(pos / cellSize - (Vector2)cell, gradients[cell.x % numCells, cell.y % numCells]);
                }

                float l1 = SmootherStep(values[0], values[1], fract.x);
                float l2 = SmootherStep(values[2], values[3], fract.x);
                data[x + y * res].r = (accumulate ? data[x + y * res].r : 0.0f) + weight * SmootherStep(l1, l2, fract.y);
            }
        }
    }

    /*
    private void WritePerlin3D(ref Color[] data, float weight, int res, int numCells, bool accumulate)
    {
        Vector3[,,] gradients = new Vector3[numCells, numCells, numCells];
        System.Random rand = new System.Random(_seed);
        int num = 10000;
        float invNum = 1.0f / num;

        for (int x = 0; x < numCells; x++)
        {
            for (int y = 0; y < numCells; y++)
            {
                for (int z = 0; z < numCells; z++)
                {
                    float phi = rand.Next(0, num) * invNum * 2.0f * Mathf.PI;
                    float theta = rand.Next(0, num) * invNum * 2.0f * Mathf.PI;
                    gradients[x, y, z] = new Vector3(Mathf.Sin(theta) * Mathf.Cos(phi), Mathf.Sin(theta) * Mathf.Sin(phi), Mathf.Cos(theta));
                }
            }
        }

        float cellSize = res / numCells;
        float[] values = new float[8];
        int[] offsetIndices = { 13, 14, 16, 17, 22, 23, 25, 26 };

        for (int z = 0; z < res; z++)
        {
            for (int y = 0; y < res; y++)
            {
                for (int x = 0; x < res; x++)
                {
                    Vector3 pos = new Vector3(x, y, z);
                    Vector3Int baseCell = Vector3Int.FloorToInt(pos / cellSize);
                    Vector3 fract = pos / cellSize - baseCell;

                    for (int i = 0; i < 8; i++)
                    {
                        Vector3Int cell = baseCell + offsets3D[offsetIndices[i]];
                        values[i] = Vector3.Dot(pos / cellSize - (Vector3)cell, gradients[cell.x % numCells, cell.y % numCells, cell.z % numCells]);
                    }

                    float lx0 = SmootherStep(values[0], values[1], fract.x);
                    float lx1 = SmootherStep(values[2], values[3], fract.x);
                    float lx2 = SmootherStep(values[4], values[5], fract.x);
                    float lx3 = SmootherStep(values[6], values[7], fract.x);
                    float ly0 = SmootherStep(lx0, lx1, fract.y);
                    float ly1 = SmootherStep(lx2, lx3, fract.y);
                    data[x + y * res + z * res * res].r = (accumulate ? data[x + y * res].r : 0.0f) + weight * SmootherStep(ly0, ly1, fract.z);
                }
            }
        }
    }
    */

    private float SmootherStep(float a, float b, float t) { return a + (6.0f * t * t * t * t * t - 15.0f * t * t * t * t + 10.0f * t * t * t) * (b - a); }

    private Vector3 GetRandomOffset(System.Random rand) 
    {
        int num = 0xffff;
        return new Vector3(rand.Next(num), rand.Next(num), rand.Next(num)) / num;
    }
}
