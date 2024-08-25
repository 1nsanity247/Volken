using UnityEngine;

public class RaymarchDebug : MonoBehaviour
{
    public float density = 1.0f;
    public float coverage = 0.5f;
    [Range(0.01f, 1.0f)]
    public float stepSize = 0.1f;
    [Range(1,50)]
    public int lightSamples = 5;
    [Range(1.0f, 10.0f)]
    public float lightSampleStepMult = 1.0f;
    public Vector3 noiseOffset = Vector3.zero;
    public float noiseScale = 1.0f;
    public Vector3 containerOffset = Vector3.zero;
    public float containerRadius = 0.5f;
    public float hgBlendVal;
    public Vector4 hgPhaseParams;
    public float jitterFactor = 1.0f;
    [Range(0.0f, 0.99f)]
    public float historyBlend = 0.9f;

    public Light directionalLight;
    public ComputeShader compute;
    public Texture2D blueNoiseTex;

    private Material material;
    private RenderTexture raymarchTex;
    private CloudNoise noise;
    private RenderTexture noiseTex;
    private RenderTexture historyTex;
    private Camera cam;
    private Matrix4x4 prevCamMat = Matrix4x4.identity;

    private void Start()
    {
        material = new Material(Shader.Find("Hidden/RaymarchDebug"));
        noise = new CloudNoise(0, compute);
        noiseTex = noise.GetWhorleyFBM3D(256, 4, 4, 0.5f, 2.0f);
        material.SetTexture("NoiseTex", noiseTex);
        material.SetTexture("BlueNoiseTex", blueNoiseTex);
        cam = GetComponent<Camera>();
    }

    private void OnRenderImage(RenderTexture source, RenderTexture destination)
    {
        material.SetFloat("volumeDensity", density);
        material.SetFloat("densityOffset", coverage);
        material.SetFloat("stepSize", Mathf.Max(0.01f, stepSize));
        material.SetInt("lightSamples", lightSamples);
        material.SetFloat("lightSampleStepMult", lightSampleStepMult);
        material.SetVector("noiseOffset", noiseOffset);
        material.SetFloat("noiseScale", 1.0f / noiseScale);
        material.SetVector("containerOffset", containerOffset);
        material.SetFloat("containerRadius", containerRadius);
        material.SetVector("lightDir", directionalLight.transform.forward);
        material.SetFloat("hgBlendVal", hgBlendVal);
        material.SetVector("hgPhaseParams", hgPhaseParams);
        material.SetFloat("jitterFactor", jitterFactor);
        material.SetFloat("historyBlend", historyBlend);
        material.SetVector("blueNoiseOffset", new Vector2(Random.Range(0.0f, 1.0f), Random.Range(0.0f, 1.0f)));

        if (raymarchTex == null)
        {
            material.SetVector("blueNoiseScale", new Vector2(source.width, source.height) / 512.0f);
            raymarchTex = new RenderTexture(source.width, source.height, 0, RenderTextureFormat.ARGBFloat);
            raymarchTex.Create();
            historyTex = new RenderTexture(source.width, source.height, 0, RenderTextureFormat.ARGBFloat);
            historyTex.Create();
        }

        Matrix4x4 reprojMat = prevCamMat;
        prevCamMat = cam.projectionMatrix * cam.worldToCameraMatrix;
        material.SetMatrix("reprojMat", reprojMat);
        material.SetTexture("HistoryTex", historyTex);
        Graphics.Blit(null, raymarchTex, material, material.FindPass("Raymarch"));
        Graphics.Blit(raymarchTex, historyTex);
        material.SetTexture("RaymarchTex", raymarchTex);
        Graphics.Blit(source, destination, material, material.FindPass("Composite"));
    }
}
