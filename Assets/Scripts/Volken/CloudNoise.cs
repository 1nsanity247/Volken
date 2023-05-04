using UnityEngine;

public struct CloudNoiseSettings
{
    public CloudNoiseSettings(int res, int startCount, float lacunarity)
    {
        resolution = res;
        cellCounts = new int[4];

        for (int i = 0; i < 4; i++)
            cellCounts[i] = Mathf.Max(1, Mathf.RoundToInt(startCount * Mathf.Pow(lacunarity, i)));
    }

    public int resolution;
    public int[] cellCounts;
}

public class CloudNoise : MonoBehaviour
{
    private static readonly Vector3Int[] offsets =
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

    private static int _seed = 0;

    public static void SetSeed(int seed) { _seed = seed; }

    private static void WriteWhorley(ref Color[] data, int channel, int res, int numCells)
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
                    cellPoints[x, y, z] = cellSize * new Vector3(x + rand.Next(0, num) * invNum, y + rand.Next(0, num) * invNum, z + rand.Next(0, num) * invNum);
                }
            }
        }

        Vector3 pos, cellOffset;
        Vector3Int centerCell, cell;
        float value;
        for (int z = 0; z < res; z++)
        {
            for (int y = 0; y < res; y++)
            {
                for (int x = 0; x < res; x++)
                {
                    int id = x + y * res + z * res * res;
                    pos = new Vector3(x, y, z);
                    centerCell = Vector3Int.FloorToInt(pos / cellSize);
                    value = float.MaxValue;

                    for (int i = 0; i < 27; i++)
                    {
                        cell = centerCell + offsets[i];
                        cellOffset = Vector3Int.zero;

                        cellOffset.x = cell.x == -1 ? -1 : (cell.x == numCells ? 1 : 0);
                        cell.x = (cell.x + numCells) % numCells;
                        cellOffset.y = cell.y == -1 ? -1 : (cell.y == numCells ? 1 : 0);
                        cell.y = (cell.y + numCells) % numCells;
                        cellOffset.z = cell.z == -1 ? -1 : (cell.z == numCells ? 1 : 0);
                        cell.z = (cell.z + numCells) % numCells;

                        cellOffset *= res;

                        value = Mathf.Min(value, Vector3.SqrMagnitude(pos - (cellPoints[cell.x, cell.y, cell.z] + cellOffset)));
                    }

                    data[id][channel] = Mathf.Max(0.0f, 1.0f - Mathf.Sqrt(value) / cellSize);
                }
            }
        }
    }

    public static Texture3D GetWhorleyFBM(CloudNoiseSettings noiseSettings)
    {
        int res = noiseSettings.resolution;
        
        Color[] data = new Color[res * res * res];

        for (int i = 0; i < 4; i++)
            WriteWhorley(ref data, i, res, noiseSettings.cellCounts[i]);

        Texture3D tex = new Texture3D(res, res, res, TextureFormat.RGBAFloat, false);
        tex.wrapMode = TextureWrapMode.Repeat;
        tex.SetPixels(data);
        tex.Apply();

        return tex;
    }
}
