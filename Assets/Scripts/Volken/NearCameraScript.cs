using Assets.Scripts;
using UnityEngine;

public class NearCameraScript : MonoBehaviour
{
    public CloudConfig config;

    private Material mat;
    private RenderTexture cloudTex, combinedDepthTex, lowResDepthTex;

    public NearCameraScript()
    {
        config = Volken.Instance.cloudConfig;

        mat = Volken.Instance.mat;
        mat.SetTexture("CloudShapeTex", Volken.Instance.whorleyTex);
        mat.SetTexture("CloudDetailTex", Volken.Instance.whorleyDetailTex);
        mat.SetTexture("PerlinTex", Volken.Instance.perlinTex);
        mat.SetTexture("DomainWarpTex", Volken.Instance.domainWarpTex);
        mat.SetTexture("BlueNoiseTex", Volken.Instance.blueNoiseTex);

        CreateTextures();
        UpdateShaderData();
    }

    private void CreateTextures()
    {
        var res = Screen.currentResolution;

        cloudTex = new RenderTexture(res.width / 4, res.height / 4, 0, RenderTextureFormat.ARGBFloat);
        cloudTex.Create();
        combinedDepthTex = new RenderTexture(res.width, res.height, 0, RenderTextureFormat.RFloat);
        combinedDepthTex.Create();
        lowResDepthTex = new RenderTexture(res.width / 4, res.height / 4, 0, RenderTextureFormat.RFloat);
        lowResDepthTex.Create();
    }

    public void UpdateShaderData()
    {
        mat.SetFloat("cloudDensity", config.density);
        mat.SetFloat("cloudAbsorption", config.absorption);
        mat.SetFloat("cloudCoverage", config.coverage);
        mat.SetFloat("cloudScale", Mathf.Max(0.1f, config.shapeScale));
        mat.SetFloat("detailScale", config.detailScale);
        mat.SetFloat("detailStrength", config.detailStrength);
        mat.SetFloat("perlinScale", config.weatherMapScale);
        mat.SetFloat("perlinStrength", config.weatherMapStrength);
        mat.SetFloat("domainWarpStrength", config.domainWarpStrength);
        mat.SetVector("phaseParams", config.phaseParameters);
        mat.SetFloat("cloudLayerHeight", Mathf.Max(0.1f, config.layerHeight));
        mat.SetFloat("cloudLayerSpread", Mathf.Max(0.1f, config.layerSpread));
        mat.SetFloat("maxCloudHeight", Mathf.Max(0.001f, config.maxCloudHeight));
        mat.SetFloat("surfaceRadius", (float)Game.Instance.FlightScene.CraftNode.Parent.PlanetData.Radius);
        mat.SetFloat("stepSize", Mathf.Max(0.01f, config.stepSize));
        mat.SetFloat("numLightSamplePoints", Mathf.Clamp(config.numLightSamplePoints, 1, 50));
        mat.SetFloat("blueNoiseScale", config.blueNoiseScale);
        mat.SetFloat("startOffsetStrength", config.blueNoiseStrength);
        mat.SetFloat("scatterStrength", config.scatterStrength);
    }

    private void OnRenderImage(RenderTexture source, RenderTexture destination)
    {
        if (!config.enabled || FarCameraScript.farDepthTex == null)
        {
            Graphics.Blit(source, destination);
            return;
        }

        var craftNode = Game.Instance.FlightScene.CraftNode;
        Vector3 planetCenter = craftNode.ReferenceFrame.PlanetToFramePosition(Vector3d.zero);
        Vector3 north = craftNode.ReferenceFrame.PlanetToFrameVector(craftNode.CraftScript.FlightData.North);
        Vector3 east = craftNode.ReferenceFrame.PlanetToFrameVector(craftNode.CraftScript.FlightData.East);
        Vector3 windVec = Mathf.Cos(Mathf.Deg2Rad * config.windDirection) * north + Mathf.Sin(Mathf.Deg2Rad * config.windDirection) * east;
        var sun = Game.Instance.FlightScene.ViewManager.GameView.SunLight;

        Color col = new Color(config.cloudColor & 0xff, config.cloudColor >> 4 & 0xff, config.cloudColor >> 8 & 0xff) / 255.0f;
        col.a = 1.0f;

        mat.SetFloat("maxDepth", 0.9f * FarCameraScript.maxFarDepth);
        mat.SetVector("sphereCenter", planetCenter);
        mat.SetVector("lightDir", sun.transform.forward);
        mat.SetColor("lightColor", col);
        config.offset += config.windSpeed * (float)Game.Instance.FlightScene.TimeManager.DeltaTime * windVec;
        mat.SetVector("cloudOffset", config.offset);

        Graphics.Blit(FarCameraScript.farDepthTex, combinedDepthTex, mat, mat.FindPass("NearDepth"));
        Graphics.Blit(combinedDepthTex, lowResDepthTex, mat, mat.FindPass("DownsampleDepth"));
        mat.SetTexture("DepthTex", lowResDepthTex);
        Graphics.Blit(null, cloudTex, mat, mat.FindPass("Clouds"));
        mat.SetTexture("CloudTex", cloudTex);
        mat.SetTexture("CombinedDepthTex", combinedDepthTex);
        mat.SetTexture("LowResDepthTex", lowResDepthTex);
        mat.SetVector("resolution", new Vector2(cloudTex.width, cloudTex.height));
        mat.SetFloat("depthDifferenceThreshold", 0.01f * Volken.Instance.depthThreshold);
        Graphics.Blit(source, destination, mat, mat.FindPass("Composite"));
    }
    
    private void OnDestroy()
    {
        cloudTex.Release();
        combinedDepthTex.Release();
        lowResDepthTex.Release();
    }
}
