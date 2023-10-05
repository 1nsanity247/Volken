using UnityEngine;

public class FarCameraScript : MonoBehaviour
{
    public static RenderTexture farDepthTex;
    public static float maxFarDepth;
    
    private Material mat;
    private Camera mainCam;

    public FarCameraScript()
    {
        mat = Volken.Instance.mat;
        mainCam = transform.GetComponent<Camera>();
    }

    private void OnRenderImage(RenderTexture source, RenderTexture destination)
    {
        maxFarDepth = mainCam.farClipPlane;

        if (farDepthTex == null)
        {
            farDepthTex = new RenderTexture(source.width, source.height, 0, RenderTextureFormat.RFloat);
            farDepthTex.Create();
        }

        mat.SetVector("clipPlanes", new Vector2(mainCam.nearClipPlane, mainCam.farClipPlane));
        Graphics.Blit(null, farDepthTex, mat, mat.FindPass("FarDepth"));
        Graphics.Blit(source, destination);
    }

    private void OnDestroy()
    {
        farDepthTex.Release();
    }
}
