using Assets.Scripts;
using ModApi.Craft;
using ModApi.Flight.Sim;
using UnityEngine;

public class NearCameraScript : MonoBehaviour
{
    private CloudConfig config;
    private Material mat;
    private RenderTexture cloudTex, upscaledCloudTex, cloudHistoryTex, combinedDepthTex, lowResDepthTex;
    private float currentResolutionScale;

    public NearCameraScript()
    {
        mat = Volken.Instance.mat;
        config = Volken.Instance.cloudConfig;
        currentResolutionScale = config.resolutionScale;

        CreateRenderTextures();
        SetShaderConstants();
        SetShaderProperties();

        Game.Instance.FlightScene.PlayerChangedSoi += OnSoiChanged;
    }

    private void OnSoiChanged(ICraftNode playerCraftNode, IPlanetNode newParent)
    {
        config.enabled = newParent.PlanetData.AtmosphereData.HasPhysicsAtmosphere;
        SetShaderConstants();
    }

    private void CreateRenderTextures()
    {
        var res = Screen.currentResolution;
        Vector2Int cloudRes = Vector2Int.RoundToInt(currentResolutionScale * new Vector2(res.width, res.height));

        cloudTex = new RenderTexture(cloudRes.x, cloudRes.y, 0, RenderTextureFormat.ARGB32);
        cloudTex.Create();

        upscaledCloudTex = new RenderTexture(res.width, res.height, 0, RenderTextureFormat.ARGB32);
        upscaledCloudTex.Create();

        cloudHistoryTex = new RenderTexture(cloudRes.x, cloudRes.y, 0, RenderTextureFormat.ARGB32);
        cloudHistoryTex.Create();

        combinedDepthTex = new RenderTexture(res.width, res.height, 0, RenderTextureFormat.RFloat);
        combinedDepthTex.Create();
        
        lowResDepthTex = new RenderTexture(cloudRes.x, cloudRes.y, 0, RenderTextureFormat.RFloat);
        lowResDepthTex.Create();
    }

    void ReleaseRenderTextures()
    {
        if (cloudTex != null && cloudTex.IsCreated())
            cloudTex.Release();
        if (upscaledCloudTex != null && upscaledCloudTex.IsCreated())
            upscaledCloudTex.Release();
        if (cloudHistoryTex != null && cloudHistoryTex.IsCreated())
            cloudHistoryTex.Release();
        if (combinedDepthTex != null && combinedDepthTex.IsCreated())
            combinedDepthTex.Release();
        if (lowResDepthTex != null && lowResDepthTex.IsCreated())
            lowResDepthTex.Release();
    }

    public void SetShaderConstants()
    {
        mat.SetVector("phaseParams", config.phaseParameters);
        mat.SetFloat("surfaceRadius", (float)Game.Instance.FlightScene.CraftNode.Parent.PlanetData.Radius);
        mat.SetFloat("blueNoiseScale", config.blueNoiseScale);
        mat.SetFloat("blueNoiseStrength", config.blueNoiseStrength);
        mat.SetFloat("historyBlend", 1.0f);

        float[] coeff =
        {
            1, 1, 2,  2, 2, 1, 1,
            1, 2, 2,  4, 2, 2, 1,
            2, 2, 4,  8, 4, 2, 2,
            2, 4, 8, 16, 8, 4, 2,
            2, 2, 4,  8, 4, 2, 2,
            1, 2, 2,  4, 2, 2, 1,
            1, 1, 2,  2, 2, 1, 1
        };

        float sum = 0.0f;
        foreach (float value in coeff) sum += value;

        mat.SetFloatArray("gaussianCoeff", coeff);
        mat.SetFloat("gaussianNorm", 1.0f / sum);
    }

    public void SetShaderProperties()
    {
        mat.SetFloat("cloudDensity", config.density);
        mat.SetFloat("cloudAbsorption", config.absorption);
        mat.SetFloat("ambientLight", config.ambientLight);
        mat.SetFloat("cloudCoverage", config.coverage);
        mat.SetFloat("cloudScale", 1.0f / Mathf.Max(0.1f, config.shapeScale));
        mat.SetFloat("detailScale", 1.0f / Mathf.Max(0.1f, config.detailScale));
        mat.SetFloat("detailStrength", config.detailStrength);
        mat.SetVector("cloudLayerHeights", config.layerHeights);
        mat.SetVector("cloudLayerSpreads", config.layerSpreads);
        mat.SetVector("cloudLayerStrengths", config.layerStrengths);
        mat.SetFloat("maxCloudHeight", Mathf.Max(0.001f, config.maxCloudHeight));
        mat.SetFloat("stepSize", Mathf.Max(0.01f, config.stepSize));
        mat.SetFloat("stepSizeFalloff", config.stepSizeFalloff);
        mat.SetFloat("numLightSamplePoints", Mathf.Clamp(config.numLightSamplePoints, 1, 50));
        mat.SetFloat("scatterStrength", config.scatterStrength);
        mat.SetColor("cloudColor", config.cloudColor);
        mat.SetFloat("depthThreshold", 0.01f * config.depthThreshold);
        mat.SetFloat("gaussianRadius", config.blurRadius);
    }

    public void SetDynamicProperties()
    {
        var craftNode = Game.Instance.FlightScene.CraftNode;
        Vector3 planetCenter = craftNode.ReferenceFrame.PlanetToFramePosition(Vector3d.zero);
        Vector3 north = craftNode.ReferenceFrame.PlanetToFrameVector(craftNode.CraftScript.FlightData.North);
        Vector3 east = craftNode.ReferenceFrame.PlanetToFrameVector(craftNode.CraftScript.FlightData.East);
        Vector3 windVec = Mathf.Cos(Mathf.Deg2Rad * config.windDirection) * north + Mathf.Sin(Mathf.Deg2Rad * config.windDirection) * east;
        config.offset += config.windSpeed * (float)Game.Instance.FlightScene.TimeManager.DeltaTime * windVec;
        config.offset.Set(config.offset.x % 1.0f, config.offset.y % 1.0f, config.offset.z % 1.0f);
        var sun = Game.Instance.FlightScene.ViewManager.GameView.SunLight;

        mat.SetFloat("maxDepth", 0.9f * FarCameraScript.maxFarDepth);
        mat.SetVector("sphereCenter", planetCenter);
        mat.SetVector("lightDir", sun.transform.forward);
        mat.SetVector("cloudOffset", config.offset);
        mat.SetVector("blueNoiseOffset", Random.insideUnitCircle);
        mat.SetVector("resolution", new Vector2(Screen.width, Screen.height));
    }

    private void OnRenderImage(RenderTexture source, RenderTexture destination)
    {
        if (!config.enabled || FarCameraScript.farDepthTex == null)
        {
            Graphics.Blit(source, destination);
            return;
        }

        if (currentResolutionScale != config.resolutionScale)
        {
            ReleaseRenderTextures();
            currentResolutionScale = config.resolutionScale;
            CreateRenderTextures();
        }

        SetDynamicProperties();

        // write near depth to combined depth texture
        Graphics.Blit(FarCameraScript.farDepthTex, combinedDepthTex, mat, mat.FindPass("NearDepth"));
        // downsample combined depth texture
        Graphics.Blit(combinedDepthTex, lowResDepthTex, mat, mat.FindPass("DownsampleDepth"));
        // main cloud pass + history buffer blend
        mat.SetTexture("DepthTex", lowResDepthTex);
        mat.SetTexture("HistoryTex", cloudHistoryTex);
        Graphics.Blit(null, cloudTex, mat, mat.FindPass("Clouds"));
        // write output to history buffer
        Graphics.Blit(cloudTex, cloudHistoryTex);
        // depth aware upscaling
        mat.SetTexture("CombinedDepthTex", combinedDepthTex);
        mat.SetTexture("LowResDepthTex", lowResDepthTex);
        mat.SetInt("isNativeRes", (cloudTex.width == source.width && cloudTex.height == source.height) ? 1 : 0);
        Graphics.Blit(cloudTex, upscaledCloudTex, mat, mat.FindPass("Upscale"));
        // blur + composite
        mat.SetTexture("UpscaledCloudTex", upscaledCloudTex);
        Graphics.Blit(source, destination, mat, mat.FindPass("Composite"));
    }
    
    private void OnDestroy()
    {
        ReleaseRenderTextures();
    }
}
