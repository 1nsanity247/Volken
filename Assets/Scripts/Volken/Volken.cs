using Assets.Scripts;
using ModApi.Scenes.Events;
using ModApi.Ui.Inspector;
using UnityEngine;

public class CloudConfig
{
    public bool enabled;
    public float density;
    public float absorption;
    public float ambientLight;
    public float coverage;
    public float shapeScale;
    public float detailScale;
    public float detailStrength;
    public float weatherMapStrength;
    public Vector4 phaseParameters;
    public Vector3 offset;
    public float windSpeed;
    public float windDirection;
    public float scatterStrength;
    public Color cloudColor;
    public float layerHeight;
    public float layerSpread;
    public float maxCloudHeight;
    public float resolutionScale;
    public float stepSize;
    public int numLightSamplePoints;
    public float blueNoiseScale;
    public float blueNoiseStrength;
    public float depthThreshold;
    public float blurRadius;
}

public class Volken
{
    public static Volken Instance { get; private set; }

    public CloudConfig cloudConfig;

    public Material mat;
    public NearCameraScript cloudRenderer;
    public FarCameraScript farCam;

    public RenderTexture whorleyTex;
    public RenderTexture whorleyDetailTex;
    public Texture2D weatherTex;
    public Texture2D blueNoiseTex;

    private CloudNoise _noise;

    public static void Initialize()
    {
        Instance ??= new Volken();
    }

    private Volken()
    {
        cloudConfig = new CloudConfig
        {
            enabled = true,
            density = 0.025f,
            absorption = 0.5f,
            ambientLight = 0.05f,
            coverage = 0.5f,
            shapeScale = 20000.0f,
            detailScale = 4000.0f,
            detailStrength = 0.75f,
            weatherMapStrength = 2.0f,
            phaseParameters = new Vector4(0.83f, 0.3f, 0.5f, 0.5f),
            offset = Vector3.zero,
            windSpeed = 0.01f,
            windDirection = 0.0f,
            scatterStrength = 5.0f,
            cloudColor = Color.white,
            layerHeight = 1000.0f,
            layerSpread = 1250.0f,
            maxCloudHeight = 5000.0f,
            resolutionScale = 0.5f,
            stepSize = 200.0f,
            numLightSamplePoints = 10,
            blueNoiseScale = 1.0f,
            blueNoiseStrength = 150.0f,
            depthThreshold = 0.1f,
            blurRadius = 0.5f
        };

        mat = new Material(Mod.Instance.ResourceLoader.LoadAsset<Shader>("Assets/Scripts/Volken/Clouds.shader"));
        
        _noise = new CloudNoise();
        GenerateNoiseTextures();

        Game.Instance.SceneManager.SceneLoaded += OnSceneLoaded;
        Game.Instance.UserInterface.AddBuildInspectorPanelAction(InspectorIds.FlightView, OnBuildFlightViewInspectorPanel);
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

    private void GenerateNoiseTextures()
    {
        whorleyTex = _noise.GetWhorleyFBM3D(128, 4, 4, 0.5f, 2.0f);
        mat.SetTexture("CloudShapeTex", whorleyTex);
        
        whorleyDetailTex = _noise.GetWhorleyFBM3D(128, 8, 4, 0.5f, 2.0f);
        mat.SetTexture("CloudDetailTex", whorleyDetailTex);
        
        weatherTex = _noise.GetPlanetMap(1024, 15.0f, 8, 0.5f, 2.0f);
        mat.SetTexture("WeatherTex", weatherTex);
        
        blueNoiseTex = Mod.Instance.ResourceLoader.LoadAsset<Texture2D>("Assets/Resources/Volken/BlueNoise.png");
        mat.SetTexture("BlueNoiseTex", blueNoiseTex);
    }

    private void OnBuildFlightViewInspectorPanel(BuildInspectorPanelRequest request)
    {
        GroupModel cloudShapeGroup = new GroupModel("Clouds");
        request.Model.AddGroup(cloudShapeGroup);

        var renderToggleModel = new ToggleModel("Main Toggle", () => cloudConfig.enabled, s =>
        {
            cloudConfig.enabled = s;

            if (s && !Game.Instance.FlightScene.CraftNode.Parent.PlanetData.AtmosphereData.HasPhysicsAtmosphere)
                Game.Instance.FlightScene.FlightSceneUI.ShowMessage("�`_��");

            ValueChanged();
        });

        cloudShapeGroup.Add(renderToggleModel);

        var densityModel = new SliderModel("Density", () => cloudConfig.density, s => { cloudConfig.density = s; ValueChanged(); }, 0.0001f, 0.05f);
        densityModel.ValueFormatter = (f) => FormatValue(f, 4);
        cloudShapeGroup.Add(densityModel);

        var absorptionModel = new SliderModel("Absorption", () => cloudConfig.absorption, s => { cloudConfig.absorption = s; ValueChanged(); }, 0.0f, 1.0f);
        absorptionModel.ValueFormatter = (f) => FormatValue(f, 2);
        cloudShapeGroup.Add(absorptionModel);

        var ambientModel = new SliderModel("Ambient Light", () => cloudConfig.ambientLight, s => { cloudConfig.ambientLight = s; ValueChanged(); }, 0.0f, 0.5f);
        ambientModel.ValueFormatter = (f) => FormatValue(f, 2);
        cloudShapeGroup.Add(ambientModel);

        var coverageModel = new SliderModel("Coverage", () => cloudConfig.coverage, s => { cloudConfig.coverage = s; ValueChanged(); }, 0.0f, 1.0f);
        coverageModel.ValueFormatter = (f) => FormatValue(f, 2);
        cloudShapeGroup.Add(coverageModel);

        var shapeScaleModel = new SliderModel("Shape Scale", () => cloudConfig.shapeScale, s => { cloudConfig.shapeScale = s; ValueChanged(); }, 1000.0f, 50000.0f);
        shapeScaleModel.ValueFormatter = (f) => FormatValue(f, 0);
        cloudShapeGroup.Add(shapeScaleModel);

        var detailScaleModel = new SliderModel("Detail Scale", () => cloudConfig.detailScale, s => { cloudConfig.detailScale = s; ValueChanged(); }, 500.0f, 25000.0f);
        detailScaleModel.ValueFormatter = (f) => FormatValue(f, 0);
        cloudShapeGroup.Add(detailScaleModel);

        var detailStrengthModel = new SliderModel("Detail Strength", () => cloudConfig.detailStrength, s => { cloudConfig.detailStrength = s; ValueChanged(); }, 0.0f, 1.0f);
        detailStrengthModel.ValueFormatter = (f) => FormatValue(f, 2);
        cloudShapeGroup.Add(detailStrengthModel);

        var perlinStrengthModel = new SliderModel("Weather Map Strength", () => cloudConfig.weatherMapStrength, s => { cloudConfig.weatherMapStrength = s; ValueChanged(); }, 0.1f, 2.0f);
        perlinStrengthModel.ValueFormatter = (f) => FormatValue(f, 1);
        cloudShapeGroup.Add(perlinStrengthModel);

        var speedModel = new SliderModel("Cloud Movement Speed", () => cloudConfig.windSpeed, s => { cloudConfig.windSpeed = s; ValueChanged(); }, -0.05f, 0.05f);
        speedModel.ValueFormatter = (f) => FormatValue(f, 2);
        cloudShapeGroup.Add(speedModel);

        var windDirectionModel = new SliderModel("Wind Direction", () => cloudConfig.windDirection, s => { cloudConfig.windDirection = s; ValueChanged(); }, 0.0f, 360.0f, true);
        windDirectionModel.ValueFormatter = (f) => FormatValue(f, 0);
        cloudShapeGroup.Add(windDirectionModel);

        var cloudColorRedModel = new SliderModel("Cloud Color Red", () => cloudConfig.cloudColor.r, s => { cloudConfig.cloudColor.r = s; ValueChanged(); }, 0.0f, 1.0f, false);
        cloudColorRedModel.ValueFormatter = (f) => FormatValue(f, 2);
        cloudShapeGroup.Add(cloudColorRedModel);
        
        var cloudColorGreenModel = new SliderModel("Cloud Color Green", () => cloudConfig.cloudColor.g, s => { cloudConfig.cloudColor.g = s; ValueChanged();}, 0.0f, 1.0f, false);
        cloudColorGreenModel.ValueFormatter = (f) => FormatValue(f, 2);
        cloudShapeGroup.Add(cloudColorGreenModel);
        
        var cloudColorBlueModel = new SliderModel("Cloud Color Blue", () => cloudConfig.cloudColor.b, s => { cloudConfig.cloudColor.b = s; ValueChanged();}, 0.0f, 1.0f, false);
        cloudColorBlueModel.ValueFormatter = (f) => FormatValue(f, 2);
        cloudShapeGroup.Add(cloudColorBlueModel);

        var scatterModel = new SliderModel("Scatter Strength", () => cloudConfig.scatterStrength, s => { cloudConfig.scatterStrength = s; ValueChanged(); }, 0.0f, 20.0f);
        scatterModel.ValueFormatter = (f) => FormatValue(f, 2);
        cloudShapeGroup.Add(scatterModel);

        GroupModel containerSettingsGroup = new GroupModel("Cloud Container");
        request.Model.AddGroup(containerSettingsGroup);

        var layerHeightModel = new SliderModel("Layer Height", () => cloudConfig.layerHeight, s => { cloudConfig.layerHeight = s; ValueChanged(); }, 1000.0f, 25000.0f);
        layerHeightModel.ValueFormatter = (f) => FormatValue(f, 0);
        containerSettingsGroup.Add(layerHeightModel);

        var layerWidthModel = new SliderModel("Layer Spread", () => cloudConfig.layerSpread, s => { cloudConfig.layerSpread = s; ValueChanged(); }, 100.0f, 15000.0f);
        layerWidthModel.ValueFormatter = (f) => FormatValue(f, 0);
        containerSettingsGroup.Add(layerWidthModel);

        var maxHeightModel = new SliderModel("Max Cloud Height", () => cloudConfig.maxCloudHeight, s => { cloudConfig.maxCloudHeight = s; ValueChanged(); }, 1000.0f, 25000.0f);
        maxHeightModel.ValueFormatter = (f) => FormatValue(f, 0);
        containerSettingsGroup.Add(maxHeightModel);

        GroupModel qualityGroup = new GroupModel("Cloud Quality");
        request.Model.AddGroup(qualityGroup);

        var resolutionScaleModel = new SliderModel("Resolution Scale", () => cloudConfig.resolutionScale, s => { cloudConfig.resolutionScale = Mathf.Clamp(s, 0.1f, 1.0f); }, 0.1f, 1.0f);
        resolutionScaleModel.ValueFormatter = (f) => FormatValue(f, 2);
        qualityGroup.Add(resolutionScaleModel);

        var stepSizeModel = new SliderModel("Step Size", () => cloudConfig.stepSize, s => { cloudConfig.stepSize = s; ValueChanged(); }, 100.0f, 2000.0f);
        stepSizeModel.ValueFormatter = (f) => FormatValue(f, 0);
        qualityGroup.Add(stepSizeModel);

        var numLightSamplesModel = new SliderModel("Number of Light Samples", () => cloudConfig.numLightSamplePoints, s => { cloudConfig.numLightSamplePoints = Mathf.RoundToInt(s); ValueChanged(); }, 1, 25, true);
        numLightSamplesModel.ValueFormatter = (f) => FormatValue(f, 0);
        qualityGroup.Add(numLightSamplesModel);

        var thresholdModel = new SliderModel("Threshold", () => cloudConfig.depthThreshold, s => { cloudConfig.depthThreshold = s; ValueChanged(); }, 0.0f, 1.0f);
        thresholdModel.ValueFormatter = (f) => FormatValue(f, 2);
        qualityGroup.Add(thresholdModel);

        var blurModel = new SliderModel("Blur Radius", () => cloudConfig.blurRadius, s => { cloudConfig.blurRadius = s; ValueChanged(); }, 0.0f, 10.0f);
        blurModel.ValueFormatter = (f) => FormatValue(f, 1);
        qualityGroup.Add(blurModel);
    }

    private void ValueChanged()
    {
        if(cloudRenderer != null)
            cloudRenderer.SetShaderProperties();
    }

    private string FormatValue(float arg, int decimals) { return arg.ToString("n" + Mathf.Max(0, decimals)); }
}
