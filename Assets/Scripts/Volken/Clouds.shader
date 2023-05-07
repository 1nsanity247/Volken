Shader "Hidden/Clouds"
{
    Properties
    {
        _MainTex("Texture", 2D) = "white" {}
    }
        SubShader
    {
        Cull Off ZWrite Off ZTest Always

        Pass
        {
            Name "Depth Downsample"

            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag

            #include "UnityCG.cginc"

            struct appdata
            {
                float4 vertex : POSITION;
                float2 uv : TEXCOORD0;
            };

            struct v2f
            {
                float4 vertex : SV_POSITION;
                float2 uv : TEXCOORD0;
            };

            v2f vert(appdata v)
            {
                v2f o;
                o.vertex = UnityObjectToClipPos(v.vertex);
                o.uv = v.uv;
                return o;
            }

            sampler2D _CameraDepthTexture;

            float4 frag(v2f i) : SV_Target
            {
                return SAMPLE_DEPTH_TEXTURE(_CameraDepthTexture, i.uv);
            }
            ENDCG
        }

        Pass
        {
            Name "Clouds"

            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag

            #include "UnityCG.cginc"

            struct appdata
            {
                float4 vertex : POSITION;
                float2 uv : TEXCOORD0;
            };

            struct v2f
            {
                float4 vertex : SV_POSITION;
                float2 uv : TEXCOORD0;
                float3 viewDir : TEXCOORD1;
            };

            v2f vert(appdata v)
            {
                v2f o;
                o.vertex = UnityObjectToClipPos(v.vertex);
                o.uv = v.uv;
                o.viewDir = mul(unity_CameraInvProjection, float4(v.uv * 2 - 1, 0, -1));
                o.viewDir = mul(unity_CameraToWorld, float4(o.viewDir, 0));
                return o;
            }

            //Textures
            sampler2D _CameraDepthTexture;

            Texture3D<float4> CloudTex;
            SamplerState samplerCloudTex;

            Texture2D<float4> BlueNoiseTex;
            SamplerState samplerBlueNoiseTex;

            //Cloud Settings
            float cloudDensity;
            float cloudAbsorption;
            float cloudCoverage;
            float cloudScale;
            float4 cloudShapeWeights;
            float3 cloudOffset;
            float4 phaseParams;

            //Cloud Layer Settings
            float cloudLayerHeight;
            float cloudLayerSpread;

            //Container Settings
            float surfaceRadius;
            float maxCloudHeight;
            float3 sphereCenter;

            // Quality
            float relStepSize;
            int numLightSamplePoints;

            //Misc
            float3 lightDir;
            float4 lightColor;
            float time;
            float3 offsetSpeed;
            float blueNoiseScale;
            float startOffsetStrength;
            float maxDepth;

            float HenyeyGreenstein(float a, float g) {
                float g2 = g * g;
                return (1 - g2) / (4 * 3.1415 * pow(1 + g2 - 2 * g * (a), 1.5));
            }

            float Phase(float a) {
                float blend = .5;
                float hgBlend = HenyeyGreenstein(a, phaseParams.x) * (1 - blend) + HenyeyGreenstein(a, -phaseParams.y) * blend;
                return phaseParams.z + hgBlend * phaseParams.w;
            }

            float2 RaySphereIntersect(float3 pos, float3 dir, float radius)
            {
                float3 offset = pos - sphereCenter;

                float a = dot(dir, dir);
                float b = 2 * dot(offset, dir);
                float c = dot(offset, offset) - radius * radius;

                float d = b * b - 4 * a * c;

                if (d < 0.0)
                    return -1.0;

                float sqrtD = sqrt(d);
                return float2((-b - sqrtD) / (2 * a), (-b + sqrtD) / (2 * a));
            }

            float SampleDensity(float3 worldPos)
            {
                float4 shape = CloudTex.SampleLevel(samplerCloudTex, (worldPos - sphereCenter) / cloudScale + cloudOffset + offsetSpeed * time, 0);
                float falloff = exp(-(abs(cloudLayerHeight - (length(worldPos - sphereCenter) - surfaceRadius))) / cloudLayerSpread);
                return (dot(cloudShapeWeights, shape) * falloff + cloudCoverage - 1.0) * cloudDensity;
            }

            float SampleLightRay(float3 pos)
            {
                float3 rayPos = pos;
                float3 rayDir = -lightDir;

                float2 surfIntersect = RaySphereIntersect(rayPos, rayDir, surfaceRadius);
                if (surfIntersect.y > 0.0)
                    return 0.0;

                float2 intersect = RaySphereIntersect(rayPos, rayDir, surfaceRadius + maxCloudHeight);
                float step = (intersect.y - max(0.0, intersect.x)) / numLightSamplePoints;

                float density = 0.0;

                for (int i = 0; i < numLightSamplePoints; i++)
                {
                    density += step * max(0.0, SampleDensity(rayPos));
                    rayPos += step * rayDir;
                }

                return exp(-density * cloudAbsorption);
            }

            float4 frag(v2f i) : SV_Target
            {
                float3 camPos = _WorldSpaceCameraPos;
                float viewLength = length(i.viewDir);
                float3 viewDir = i.viewDir / viewLength;

                float2 intersect = RaySphereIntersect(camPos, viewDir, surfaceRadius + maxCloudHeight);

                if (intersect.y < 0.0)
                    return float4(0.0, 0.0, 0.0, 1.0);

                float2 surfIntersect = RaySphereIntersect(camPos, viewDir, surfaceRadius);
                float depth = LinearEyeDepth(SAMPLE_DEPTH_TEXTURE(_CameraDepthTexture, i.uv)) * viewLength;

                float rayDist = surfIntersect.x * surfIntersect.y < 0.0 ? surfIntersect.y : max(0.0, intersect.x);
                float maxRayDist = surfIntersect.y > 0.0 ? surfIntersect.x : intersect.y;
                maxRayDist = depth < maxDepth ? min(maxRayDist, depth) : maxRayDist;

                if (maxRayDist - rayDist <= 0.0)
                    return float4(0.0, 0.0, 0.0, 1.0);

                //rayDist += startOffsetStrength * BlueNoiseTex.SampleLevel(samplerBlueNoiseTex, blueNoiseScale * i.uv, 0).r;
                float step = 0.1 * relStepSize * maxCloudHeight;
                int iter = 0;

                float cosAngle = dot(viewDir, -lightDir);
                float phaseVal = Phase(cosAngle);

                float transmittance = 1.0;
                float lightEnergy = 0.0;
                
                float3 rayPos;
                float lightTransmittance;
                float density;

                while(rayDist < maxRayDist && iter < 150)
                {
                    rayPos = camPos + rayDist * viewDir;
                    density = SampleDensity(rayPos);

                    if (density > 0.0)
                    {
                        lightTransmittance = SampleLightRay(rayPos);
                        lightEnergy += density * step * transmittance * lightTransmittance * phaseVal;
                        transmittance *= exp(-density * step * cloudAbsorption);

                        if (transmittance < 0.01)
                            break;
                    }

                    rayDist += step;
                    iter++;
                }
                
                float3 cloudCol = lightEnergy * lightColor;
                return float4(cloudCol, transmittance);
            }
            ENDCG
        }

        Pass
        {
            Name "Composite"

            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag

            #include "UnityCG.cginc"

            struct appdata
            {
                float4 vertex : POSITION;
                float2 uv : TEXCOORD0;
            };

            struct v2f
            {
                float4 vertex : SV_POSITION;
                float2 uv : TEXCOORD0;
            };

            v2f vert(appdata v)
            {
                v2f o;
                o.vertex = UnityObjectToClipPos(v.vertex);
                o.uv = v.uv;
                return o;
            }

            //Textures
            sampler2D _MainTex;
            sampler2D _CameraDepthTexture;

            Texture2D<float4> TempTex;
            SamplerState samplerTempTex;

            Texture2D<float> TempDepthTex;
            SamplerState samplerTempDepthTex;

            int lowResWidth;
            int lowResHeight;

            float4 Cubic(float v) {
                float4 n = float4(1.0, 2.0, 3.0, 4.0) - v;
                float4 s = n * n * n;
                float x = s.x;
                float y = s.y - 4.0 * s.x;
                float z = s.z - 4.0 * s.y + 6.0 * s.x;
                float w = 6.0 - x - y - z;
                return float4(x, y, z, w) * (1.0 / 6.0);
            }

            float4 FilterBicubic(float2 texCoord)
            {
                float2 texScale = float2(lowResWidth, lowResHeight);
                float2 invTexScale = 1.0 / texScale;

                texCoord *= texScale;

                float fx = frac(texCoord.x);
                float fy = frac(texCoord.y);
                texCoord.x -= fx;
                texCoord.y -= fy;

                float4 xcubic = Cubic(fx);
                float4 ycubic = Cubic(fy);

                float4 c = float4(texCoord.x - 0.5, texCoord.x + 1.5, texCoord.y - 0.5, texCoord.y + 1.5);
                float4 s = float4(xcubic.x + xcubic.y, xcubic.z + xcubic.w, ycubic.x + ycubic.y, ycubic.z + ycubic.w);
                float4 offset = c + float4(xcubic.y, xcubic.w, ycubic.y, ycubic.w) / s;

                float4 sample0 = TempTex.Sample(samplerTempTex, float2(offset.x, offset.z) * invTexScale);
                float4 sample1 = TempTex.Sample(samplerTempTex, float2(offset.y, offset.z) * invTexScale);
                float4 sample2 = TempTex.Sample(samplerTempTex, float2(offset.x, offset.w) * invTexScale);
                float4 sample3 = TempTex.Sample(samplerTempTex, float2(offset.y, offset.w) * invTexScale);

                float sx = s.x / (s.x + s.y);
                float sy = s.z / (s.z + s.w);

                return lerp(lerp(sample3, sample2, sx), lerp(sample1, sample0, sx), sy);
            }

            float4 frag(v2f i) : SV_Target
            {
                float3 col = tex2D(_MainTex, i.uv);
                float4 clouds = float4(0, 0, 0, 1);

                float d0 = SAMPLE_DEPTH_TEXTURE(_CameraDepthTexture, i.uv);
                float d1 = TempDepthTex.Sample(samplerTempDepthTex, i.uv);
                float d2 = TempDepthTex.Sample(samplerTempDepthTex, i.uv, int2(-1, 0));
                float d3 = TempDepthTex.Sample(samplerTempDepthTex, i.uv, int2(1, 0));
                float d4 = TempDepthTex.Sample(samplerTempDepthTex, i.uv, int2(0, -1));
                float d5 = TempDepthTex.Sample(samplerTempDepthTex, i.uv, int2(0, 1));

                d1 = abs(d0 - d1);
                d2 = abs(d0 - d2);
                d3 = abs(d0 - d3);
                d4 = abs(d0 - d4);
                d5 = abs(d0 - d5);

                float dmin = min(d1, min(min(d2, d3), min(d4, d5)));

                if (dmin == d1)
                    clouds = TempTex.Sample(samplerTempTex, i.uv);
                else if (dmin == d2)
                    clouds = TempTex.Sample(samplerTempTex, i.uv, int2(-1, 0));
                else if (dmin == d3)
                    clouds = TempTex.Sample(samplerTempTex, i.uv, int2(1, 0));
                else if (dmin == d4)
                    clouds = TempTex.Sample(samplerTempTex, i.uv, int2(0, -1));
                else if (dmin == d5)
                    clouds = TempTex.Sample(samplerTempTex, i.uv, int2(0, 1));

                return float4(col * clouds.a + clouds.rgb, 0);
            }
            ENDCG
        }
    }
}
