using Assets.Scripts;
using UnityEngine;

public class CloudRenderer : MonoBehaviour
{
    private Camera mainCam;
    private Material material;
    private Texture3D tex;

    public float[] data;

    public CloudRenderer()
    {
        data = new float[]
        {
            0.0025f, -0.002f, 25000.0f, 0.0f, 5000.0f, 5000.0f, 1274200.0f, 10000.0f
        };

        mainCam = transform.GetComponent<Camera>();

        Shader shader = Mod.Instance.ResourceLoader.LoadAsset<Shader>("Assets/Scripts/Volken/Clouds.shader");
        material = new Material(shader);

        CloudNoiseSettings noiseSettings = new CloudNoiseSettings(64, 2, 2.0f);

        tex = CloudNoise.GetWhorleyFBM(noiseSettings);
        material.SetTexture("CloudTex", tex);

        Texture2D blueNoise = Mod.Instance.ResourceLoader.LoadAsset<Texture2D>("Assets/Resources/BlueNoise.png");
        material.SetTexture("BlueNoiseTex", blueNoise);
    }

    private void OnRenderImage(RenderTexture source, RenderTexture destination)
    {
        var craftNode = Game.Instance.FlightScene.CraftNode;
        Vector3 planetCenter = craftNode.ReferenceFrame.PlanetToFramePosition(Vector3d.zero);

        material.SetFloat("cloudDensity", data[0]);
        material.SetFloat("cloudAbsorption", 0.5f);
        material.SetFloat("cloudCoverage", data[1]);
        material.SetFloat("cloudScale", Mathf.Max(0.1f, data[2]));
        material.SetVector("cloudShapeWeights", new Vector4(1.0f, 0.5f, 0.25f, 0.125f));
        material.SetVector("cloudOffset", Vector3.zero);
        material.SetVector("phaseParams", new Vector4(0.83f, 0.3f, 0.8f, 0.15f));
        material.SetFloat("cloudLayerHeight", Mathf.Max(0.1f, data[4]));
        material.SetFloat("cloudLayerSpread", Mathf.Max(0.1f, data[5]));
        material.SetFloat("surfaceRadius", Mathf.Max(0.001f, data[6]));
        material.SetFloat("maxCloudHeight", Mathf.Max(0.001f, data[7]));
        material.SetVector("sphereCenter", planetCenter);
        material.SetVector("lightDir", craftNode.CraftScript.FlightData.SolarRadiationFrameDirection);
        material.SetColor("lightColor", Color.white);
        material.SetFloat("time", Time.time);
        material.SetVector("offsetSpeed", data[3] * Vector3.one);
        material.SetFloat("relativeStepSize", Mathf.Max(0.1f, 0.5f));
        material.SetFloat("numLightSamplePoints", Mathf.Max(1, 10));
        material.SetFloat("blueNoiseScale", 1.5f);
        material.SetFloat("startOffsetStrength", 0.1f);
        material.SetFloat("maxDepth", mainCam.farClipPlane);

        Graphics.Blit(source, destination, material);
    }
}
