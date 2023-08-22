using UnityEngine;

public class CloudNoise : MonoBehaviour
{
    private static readonly Vector3Int[] offsets3D =
    {
        new Vector3Int(-1,-1,-1), new Vector3Int(0,-1,-1), new Vector3Int(1,-1,-1),
        new Vector3Int(-1,0,-1), new Vector3Int(0,0,-1), new Vector3Int(1,0,-1),
        new Vector3Int(-1,1,-1), new Vector3Int(0,1,-1), new Vector3Int(1,1,-1),

        new Vector3Int(-1,-1,0), new Vector3Int(0,-1,0), new Vector3Int(1,-1,0),
        new Vector3Int(-1,0,0), new Vector3Int(0,0,0), new Vector3Int(1,0,0),
        new Vector3Int(-1,1,0), new Vector3Int(0,1,0), new Vector3Int(1,1,0),

        new Vector3Int(-1,-1,1), new Vector3Int(0,-1,1), new Vector3Int(1,-1,1),
        new Vector3Int(-1,0,1), new Vector3Int(0,0,1), new Vector3Int(1,0,1),
        new Vector3Int(-1,1,1), new Vector3Int(0,1,1), new Vector3Int(1,1,1)
    };

    private static readonly Vector2Int[] offsets2D =
    {
        new Vector2Int(0, 0), new Vector2Int(1, 0),
        new Vector2Int(0, 1), new Vector2Int(1, 1)
    };

    private static int _seed = 0;

    public static void SetSeed(int seed) { _seed = seed; }
    
    public static Texture3D GetWhorleyFBM3D(int resolution, int cellCount, int octaves, float lacunarity)
    {
        Color[] data = new Color[resolution * resolution * resolution];

        int numCells;

        float[] weights = new float[octaves];
        float sum = 0.0f;

        for (int i = 0; i < octaves; i++)
        {
            weights[i] = Mathf.Pow(0.5f, i);
            sum += weights[i];
        }

        float norm = 1.0f / sum;

        for (int i = 0; i < octaves; i++)
        {
            numCells = Mathf.RoundToInt(cellCount * Mathf.Pow(lacunarity, i));
            if (numCells > resolution) break;

            WriteWhorley(ref data, norm * weights[i], resolution, numCells, i > 0);
        }

        Texture3D tex = new Texture3D(resolution, resolution, resolution, TextureFormat.RFloat, false);
        tex.wrapMode = TextureWrapMode.Repeat;
        tex.SetPixels(data);
        tex.Apply();

        return tex;
    }

    private static void WriteWhorley(ref Color[] data, float weight, int res, int numCells, bool accumulate)
    {
        float cellSize = (float)res / numCells;

        System.Random rand = new System.Random(_seed);
        int num = 10000;
        float invNum = 1.0f / num;
        Vector3[,,] cellPoints = new Vector3[numCells, numCells, numCells];
        for (int z = 0; z < numCells; z++)
        {
            for (int y = 0; y < numCells; y++)
            {
                for (int x = 0; x < numCells; x++)
                {
                    cellPoints[x, y, z] = cellSize * new Vector3(rand.Next(0, num) * invNum, rand.Next(0, num) * invNum, rand.Next(0, num) * invNum);
                }
            }
        }

        Vector3 pos;
        Vector3Int centerCell, cell;
        float value;
        for (int z = 0; z < res; z++)
        {
            for (int y = 0; y < res; y++)
            {
                for (int x = 0; x < res; x++)
                {
                    int id = x + y * res + z * res * res;
                    pos = new Vector3(x % cellSize, y % cellSize, z % cellSize);
                    centerCell = Vector3Int.FloorToInt(new Vector3(x, y, z) / cellSize);
                    value = float.MaxValue;

                    for (int i = 0; i < 27; i++)
                    {
                        cell = centerCell + offsets3D[i];
                        cell = new Vector3Int((cell.x + numCells) % numCells, (cell.y + numCells) % numCells, (cell.z + numCells) % numCells);
                        value = Mathf.Min(value, Vector3.SqrMagnitude(pos - (cellPoints[cell.x, cell.y, cell.z] + cellSize * (Vector3)offsets3D[i])));
                    }

                    data[id].r = (accumulate ? data[id].r : 0.0f) + weight * Mathf.Max(0.0f, 1.0f - Mathf.Sqrt(value) / cellSize);
                }
            }
        }
    }

    public static Texture2D GetPerlinFBM2D(int resolution, int cellCount, int octaves, float lacunarity)
    {
        Color[] data = new Color[resolution * resolution];

        int numCells;

        float[] weights = new float[octaves];
        float sum = 0.0f;

        for (int i = 0; i < octaves; i++)
        { 
            weights[i] = Mathf.Pow(0.5f, i);
            sum += weights[i];
        }
        
        float norm = 1.0f / sum;

        for (int i = 0; i < octaves; i++)
        {
            numCells = Mathf.RoundToInt(cellCount * Mathf.Pow(lacunarity, i));

            print(numCells);
            if (numCells > resolution) break;

            WritePerlin2D(ref data, norm * weights[i], resolution, numCells, i > 0);
        }

        Texture2D tex = new Texture2D(resolution, resolution, TextureFormat.RFloat, false);
        tex.wrapMode = TextureWrapMode.Repeat;
        tex.SetPixels(data);
        tex.Apply();

        return tex;
    }

    private static void WritePerlin2D(ref Color[] data, float weight, int res, int numCells, bool accumulate)
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

    private static void WritePerlin3D(ref Color[] data, float weight, int res, int numCells, bool accumulate)
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
                    data[x + y * res].r = (accumulate ? data[x + y * res].r : 0.0f) + weight * SmootherStep(ly0, ly1, fract.z);
                }
            }
        }
    }

    private static float SmootherStep(float a, float b, float t) { return a + (6.0f * t * t * t * t * t - 15.0f * t * t * t * t + 10.0f * t * t * t) * (b - a); }
}
