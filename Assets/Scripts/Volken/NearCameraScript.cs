using Assets.Scripts;
using ModApi.Craft;
using ModApi.Flight.Sim;
using UnityEngine;

/*
    Volken Pipeline Overview:

    1. Write depth from far camera to a render texture
    2. Write depth from near camera to the same texture
    3. Downsample the combined depth texture for later use in depth aware upscaling
    4. Render volumetrics to "cloudTex" texture (optionally blend with history buffer)
    5. Write the output to the history buffer
    6. Upscale "cloudTex" to the game view resolution using the previously generated depth textures
    7. Blur the upscaled clouds and add them into the main image
*/

public class NearCameraScript : MonoBehaviour
{
    private CloudConfig config;
    private Material mat;
    private RenderTexture cloudTex, upscaledCloudTex, cloudHistoryTex, combinedDepthTex, lowResDepthTex;
    private float currentResolutionScale;
    private Camera cam;
    private Matrix4x4 prevViewProjMat;

    public NearCameraScript()
    {
        mat = Volken.Instance.mat;
        config = Volken.Instance.cloudConfig;
        currentResolutionScale = config.resolutionScale;
        cam = GetComponent<Camera>();
        prevViewProjMat = cam.projectionMatrix * cam.worldToCameraMatrix;

        CreateRenderTextures();
        SetShaderProperties();

        Game.Instance.FlightScene.PlayerChangedSoi += OnSoiChanged;
    }

    private void OnSoiChanged(ICraftNode playerCraftNode, IPlanetNode newParent)
    {
        config.enabled = newParent.PlanetData.AtmosphereData.HasPhysicsAtmosphere;
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

    public void SetShaderProperties()
    {
        // TODO: use a structured buffer instead
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
        mat.SetFloat("atmoBlendFactor", config.atmoBlendFactor * 4e-5f);
        mat.SetColor("cloudColor", config.cloudColor);
        mat.SetFloat("depthThreshold", 0.01f * config.depthThreshold);
        mat.SetFloat("blueNoiseStrength", config.blueNoiseStrength);
        mat.SetFloat("historyBlend", config.historyBlend);
        mat.SetVector("phaseParams", config.phaseParameters);
        mat.SetFloat("surfaceRadius", (float)Game.Instance.FlightScene.CraftNode.Parent.PlanetData.Radius);
        mat.SetVector("blueNoiseScale", currentResolutionScale * new Vector2(Screen.width, Screen.height) / 512.0f);
    }

    public void SetDynamicProperties()
    {
        var craftNode = Game.Instance.FlightScene.CraftNode;
        // position of the center of the parent body in view space
        Vector3 planetCenter = craftNode.ReferenceFrame.PlanetToFramePosition(Vector3d.zero);
        var sun = Game.Instance.FlightScene.ViewManager.GameView.SunLight;
        
        // wind stuff
        Vector3 north = craftNode.ReferenceFrame.PlanetToFrameVector(craftNode.CraftScript.FlightData.North);
        Vector3 east = craftNode.ReferenceFrame.PlanetToFrameVector(craftNode.CraftScript.FlightData.East);
        Vector3 windVec = Mathf.Cos(Mathf.Deg2Rad * config.windDirection) * north + Mathf.Sin(Mathf.Deg2Rad * config.windDirection) * east;
        config.offset += config.windSpeed * (float)Game.Instance.FlightScene.TimeManager.DeltaTime * windVec;
        config.offset.Set(config.offset.x % 1.0f, config.offset.y % 1.0f, config.offset.z % 1.0f);

        mat.SetFloat("maxDepth", 0.9f * FarCameraScript.maxFarDepth);
        mat.SetVector("sphereCenter", planetCenter);
        mat.SetVector("lightDir", sun.transform.forward);
        mat.SetVector("cloudOffset", config.offset);
        mat.SetVector("blueNoiseOffset", Random.insideUnitCircle);
        mat.SetMatrix("reprojMat", prevViewProjMat);
        
        prevViewProjMat = cam.projectionMatrix * cam.worldToCameraMatrix;
    }

    private void OnRenderImage(RenderTexture source, RenderTexture destination)
    {
        if (!config.enabled || FarCameraScript.farDepthTex == null)
        {
            // return unchanged image
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
