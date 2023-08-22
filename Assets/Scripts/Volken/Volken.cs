using Assets.Scripts;
using ModApi.Scenes.Events;
using ModApi.Ui.Inspector;
using UnityEngine;

public class CloudConfig
{
    public bool enabled;
    public float density;
    public float absorption;
    public float coverage;
    public float shapeScale;
    public float detailScale;
    public float detailStrength;
    public float weatherMapScale;
    public float weatherMapStrength;
    public float domainWarpStrength;
    public Vector4 phaseParameters;
    public Vector3 offset;
    public float windSpeed;
    public float windDirection;
    public float scatterStrength;
    public uint cloudColor;
    public float layerHeight;
    public float layerSpread;
    public float maxCloudHeight;
    public float stepSize;
    public int numLightSamplePoints;
    public float blueNoiseScale;
    public float blueNoiseStrength;
}

public class Volken
{
    public static Volken Instance;

    public CloudConfig cloudConfig;
    public float depthThreshold = 0.1f;

    public Material mat;
    public NearCameraScript cloudRenderer;
    public FarCameraScript farCam;

    public Texture3D whorleyTex;
    public Texture3D whorleyDetailTex;
    public Texture2D perlinTex;
    public Texture2D domainWarpTex;
    public Texture2D blueNoiseTex;

    public static void Initialize()
    {
        Instance = new Volken();
    }

    private Volken()
    {
        cloudConfig = new CloudConfig
        {
            enabled = true,
            density = 0.01f,
            absorption = 0.5f,
            coverage = 0.5f,
            shapeScale = 10000.0f,
            detailScale = 2000.0f,
            detailStrength = 0.75f,
            weatherMapScale = 2.0f,
            weatherMapStrength = 2.0f,
            domainWarpStrength = 1.0f,
            phaseParameters = new Vector4(0.83f, 0.3f, 0.5f, 0.5f),
            offset = Vector3.zero,
            windSpeed = 0.025f,
            windDirection = 0.0f,
            scatterStrength = 3.0f,
            cloudColor = uint.MaxValue,
            layerHeight = 1000.0f,
            layerSpread = 1250.0f,
            maxCloudHeight = 5000.0f,
            stepSize = 250.0f,
            numLightSamplePoints = 10,
            blueNoiseScale = 10.0f,
            blueNoiseStrength = 0.0f
        };

        mat = new Material(Mod.Instance.ResourceLoader.LoadAsset<Shader>("Assets/Scripts/Volken/Clouds.shader"));

        GenerateNoiseTextures();

        Game.Instance.SceneManager.SceneLoaded += OnSceneLoaded;
        Game.Instance.UserInterface.AddBuildInspectorPanelAction(InspectorIds.FlightView, OnBuildFlightViewInspectorPanel);
    }

    private void GenerateNoiseTextures()
    {
        whorleyTex = CloudNoise.GetWhorleyFBM3D(64, 2, 4, 2.0f);
        whorleyDetailTex = CloudNoise.GetWhorleyFBM3D(64, 4, 4, 2.0f);

        perlinTex = CloudNoise.GetPerlinFBM2D(512, 8, 4, 2.0f);
        domainWarpTex = CloudNoise.GetPerlinFBM2D(512, 16, 1, 2.0f);

        blueNoiseTex = Mod.Instance.ResourceLoader.LoadAsset<Texture2D>("Assets/Resources/Volken/BlueNoise.png");
    }

    private void OnSceneLoaded(object sender, SceneEventArgs e)
    {
        if (e.Scene == "Flight")
        {
            cloudConfig.enabled = Game.Instance.FlightScene.CraftNode.Parent.PlanetData.AtmosphereData.HasPhysicsAtmosphere;
            var gameCam = Game.Instance.FlightScene.ViewManager.GameView.GameCamera;
            cloudRenderer = gameCam.NearCamera.gameObject.AddComponent<NearCameraScript>();
            farCam = gameCam.FarCamera.gameObject.AddComponent<FarCameraScript>();
        }
    }

    private void OnBuildFlightViewInspectorPanel(BuildInspectorPanelRequest request)
    {
        GroupModel cloudShapeGroup = new GroupModel("Clouds");
        request.Model.AddGroup(cloudShapeGroup);

        var renderToggleModel = new ToggleModel("Main Toggle", () => cloudConfig.enabled, s =>
        {
            cloudConfig.enabled = s;

            if (s && !Game.Instance.FlightScene.CraftNode.Parent.PlanetData.AtmosphereData.HasPhysicsAtmosphere)
                Game.Instance.FlightScene.FlightSceneUI.ShowMessage("•`_´•");

            ValueChanged();
        });

        cloudShapeGroup.Add(renderToggleModel);

        var densityModel = new SliderModel("Density", () => cloudConfig.density, s => { cloudConfig.density = s; ValueChanged(); }, 0.0001f, 0.05f, false);
        densityModel.ValueFormatter = (f) => FormatValue(f, 4);
        cloudShapeGroup.Add(densityModel);

        var absorptionModel = new SliderModel("Absorption", () => cloudConfig.absorption, s => { cloudConfig.absorption = s; ValueChanged(); }, 0.0f, 1.0f, false);
        absorptionModel.ValueFormatter = (f) => FormatValue(f, 2);
        cloudShapeGroup.Add(absorptionModel);

        var coverageModel = new SliderModel("Coverage", () => cloudConfig.coverage, s => { cloudConfig.coverage = s; ValueChanged(); }, 0.0f, 1.0f, false);
        coverageModel.ValueFormatter = (f) => FormatValue(f, 2);
        cloudShapeGroup.Add(coverageModel);

        var shapeScaleModel = new SliderModel("Shape Scale", () => cloudConfig.shapeScale, s => { cloudConfig.shapeScale = s; ValueChanged(); }, 1000.0f, 50000.0f, false);
        shapeScaleModel.ValueFormatter = (f) => FormatValue(f, 0);
        cloudShapeGroup.Add(shapeScaleModel);

        var detailScaleModel = new SliderModel("Detail Scale", () => cloudConfig.detailScale, s => { cloudConfig.detailScale = s; ValueChanged(); }, 500.0f, 25000.0f, false);
        detailScaleModel.ValueFormatter = (f) => FormatValue(f, 0);
        cloudShapeGroup.Add(detailScaleModel);

        var detailStrengthModel = new SliderModel("Detail Strength", () => cloudConfig.detailStrength, s => { cloudConfig.detailStrength = s; ValueChanged(); }, 0.0f, 1.0f, false);
        detailStrengthModel.ValueFormatter = (f) => FormatValue(f, 2);
        cloudShapeGroup.Add(detailStrengthModel);

        var perlinScaleModel = new SliderModel("Weather Map Scale", () => cloudConfig.weatherMapScale, s => { cloudConfig.weatherMapScale = s; ValueChanged(); }, 0.1f, 2.0f, false);
        perlinScaleModel.ValueFormatter = (f) => FormatValue(f, 1);
        cloudShapeGroup.Add(perlinScaleModel);

        var perlinStrengthModel = new SliderModel("Weather Map Strength", () => cloudConfig.weatherMapStrength, s => { cloudConfig.weatherMapStrength = s; ValueChanged(); }, 0.1f, 2.0f, false);
        perlinStrengthModel.ValueFormatter = (f) => FormatValue(f, 1);
        cloudShapeGroup.Add(perlinStrengthModel);

        var domainStrengthModel = new SliderModel("Domain Warp Strength", () => cloudConfig.domainWarpStrength, s => { cloudConfig.domainWarpStrength = s; ValueChanged(); }, 0.0f, 1.0f, false);
        domainStrengthModel.ValueFormatter = (f) => FormatValue(f, 2);
        cloudShapeGroup.Add(domainStrengthModel);

        var speedModel = new SliderModel("Cloud Movement Speed", () => cloudConfig.windSpeed, s => { cloudConfig.windSpeed = s; ValueChanged(); }, -0.1f, 0.1f, false);
        speedModel.ValueFormatter = (f) => FormatValue(f, 2);
        cloudShapeGroup.Add(speedModel);

        var windDirectionModel = new SliderModel("Wind Direction", () => cloudConfig.windDirection, s => { cloudConfig.windDirection = s; ValueChanged(); }, 0.0f, 360.0f, true);
        windDirectionModel.ValueFormatter = (f) => FormatValue(f, 0);
        cloudShapeGroup.Add(windDirectionModel);

        var cloudColorModel = new SliderModel("Cloud Color (have fun)", () => cloudConfig.cloudColor, s => cloudConfig.cloudColor = (uint)s, 0, 0xffffff, true);
        cloudColorModel.ValueFormatter = (f) => FormatValue(f, 0);
        cloudShapeGroup.Add(cloudColorModel);

        var scatterModel = new SliderModel("Scatter Strength", () => cloudConfig.scatterStrength, s => { cloudConfig.scatterStrength = s; ValueChanged(); }, 0.0f, 5.0f, false);
        scatterModel.ValueFormatter = (f) => FormatValue(f, 2);
        cloudShapeGroup.Add(scatterModel);

        GroupModel containerSettingsGroup = new GroupModel("Cloud Container");
        request.Model.AddGroup(containerSettingsGroup);

        var layerHeightModel = new SliderModel("Layer Height", () => cloudConfig.layerHeight, s => { cloudConfig.layerHeight = s; ValueChanged(); }, 1000.0f, 25000.0f, false);
        layerHeightModel.ValueFormatter = (f) => FormatValue(f, 0);
        containerSettingsGroup.Add(layerHeightModel);

        var layerWidthModel = new SliderModel("Layer Spread", () => cloudConfig.layerSpread, s => { cloudConfig.layerSpread = s; ValueChanged(); }, 100.0f, 15000.0f, false);
        layerWidthModel.ValueFormatter = (f) => FormatValue(f, 0);
        containerSettingsGroup.Add(layerWidthModel);

        var maxHeightModel = new SliderModel("Max Cloud Height", () => cloudConfig.maxCloudHeight, s => { cloudConfig.maxCloudHeight = s; ValueChanged(); }, 1000.0f, 25000.0f, false);
        maxHeightModel.ValueFormatter = (f) => FormatValue(f, 0);
        containerSettingsGroup.Add(maxHeightModel);

        GroupModel qualityGroup = new GroupModel("Cloud Quality");
        request.Model.AddGroup(qualityGroup);

        var stepSizeModel = new SliderModel("Step Size", () => cloudConfig.stepSize, s => { cloudConfig.stepSize = s; ValueChanged(); }, 100.0f, 2000.0f, false);
        stepSizeModel.ValueFormatter = (f) => FormatValue(f, 0);
        qualityGroup.Add(stepSizeModel);

        var numLightSamplesModel = new SliderModel("Number of Light Samples", () => cloudConfig.numLightSamplePoints, s => { cloudConfig.numLightSamplePoints = Mathf.RoundToInt(s); ValueChanged(); }, 1, 25, true);
        numLightSamplesModel.ValueFormatter = (f) => FormatValue(f, 0);
        qualityGroup.Add(numLightSamplesModel);

        var thresholdModel = new SliderModel("Threshold", () => depthThreshold, s => { depthThreshold = s; ValueChanged(); }, 0.0f, 1.0f, false);
        thresholdModel.ValueFormatter = (f) => FormatValue(f, 2);
        qualityGroup.Add(thresholdModel);
    }

    private void ValueChanged()
    {
        if (cloudRenderer != null)
            cloudRenderer.UpdateShaderData();
    }

    private string FormatValue(float arg, int decimals) { return arg.ToString("n" + Mathf.Max(0, decimals)); }
}
