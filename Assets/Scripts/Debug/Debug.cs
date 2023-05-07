using UnityEngine;

public class Debug : MonoBehaviour
{
    [Header("Cloud Settings")]
    public float density;
    [Range(0.0f, 5.0f)]
    public float absorption;
    public float coverage;
    public float scale;
    public Vector4 octaveWeights;
    public Vector3 offset;
    public Vector4 phase;
    public Vector3 offsetSpeed;
    [Range(0.1f, 5.0f)]
    public float relativeStepSize;
    [Range(1, 50)]
    public int numLightSamplePoints;
    public float blueNoiseScale;
    public float startOffsetStrength;
    [Header("Layer Settings")]
    public float layerHeight;
    public float layerSpread;
    [Header("Container Settings")]
    public float surfaceRadius;
    public float maxCloudHeight;
    public Vector3 sphereOffset;
    [Header("References")]
    public Light directionalLight;
    public Texture2D blueNoise;

    private Material material;
    private Texture3D tex;
    private RenderTexture tempTex;
    private RenderTexture tempDepthTex;

    private void Start()
    {
        material = new Material(Shader.Find("Hidden/Debug"));

        CloudNoiseSettings noiseSettings = new CloudNoiseSettings(64, 2, 2.0f);

        tex = CloudNoise.GetWhorleyFBM(noiseSettings);
        material.SetTexture("CloudTex", tex);

        material.SetTexture("BlueNoiseTex", blueNoise);
    }

    private void OnRenderImage(RenderTexture source, RenderTexture destination)
    {
        material.SetFloat("cloudDensity", density);
        material.SetFloat("cloudAbsorption", absorption);
        material.SetFloat("cloudCoverage", coverage);
        material.SetFloat("cloudScale", Mathf.Max(0.1f, scale));
        material.SetVector("cloudShapeWeights", octaveWeights);
        material.SetVector("cloudOffset", offset);
        material.SetVector("phaseParams", phase);
        material.SetFloat("cloudLayerHeight", Mathf.Max(0.1f, layerHeight));
        material.SetFloat("cloudLayerSpread", Mathf.Max(0.1f, layerSpread));
        material.SetFloat("surfaceRadius", Mathf.Max(0.001f, surfaceRadius));
        material.SetFloat("maxCloudHeight", Mathf.Max(0.001f, maxCloudHeight));
        material.SetVector("sphereCenter", sphereOffset);
        material.SetVector("lightDir", directionalLight.transform.forward);
        material.SetColor("lightColor", directionalLight.color);
        material.SetFloat("time", Time.time);
        material.SetVector("offsetSpeed", offsetSpeed);
        material.SetFloat("relativeStepSize", Mathf.Max(0.1f, relativeStepSize));
        material.SetFloat("numLightSamplePoints", Mathf.Max(1, numLightSamplePoints));
        material.SetFloat("blueNoiseScale", blueNoiseScale);
        material.SetFloat("startOffsetStrength", startOffsetStrength);
        material.SetFloat("maxDepth", transform.GetComponent<Camera>().farClipPlane);

        if(tempTex == null)
        {
            tempTex = new RenderTexture(source.width / 4, source.height / 4, 0, RenderTextureFormat.ARGBFloat);
            tempTex.Create();
        }

        if(tempDepthTex == null)
        {
            tempDepthTex = new RenderTexture(source.width / 4, source.height / 4, 0, RenderTextureFormat.RFloat);
            tempDepthTex.Create();
        }

        Graphics.Blit(null, tempDepthTex, material, 0);
        Graphics.Blit(null, tempTex, material, 1);
        material.SetTexture("TempTex", tempTex);
        material.SetTexture("TempDepthTex", tempDepthTex);
        Graphics.Blit(source, destination, material, 2);
    }
}
