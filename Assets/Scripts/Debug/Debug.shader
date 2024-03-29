Shader "Hidden/Debug"
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
                o.viewDir = mul(unity_CameraToWorld, float4(o.viewDir,0));
                return o;
            }

            sampler2D _CameraDepthTexture;

            Texture3D<float4> CloudTex;
            SamplerState samplerCloudTex;
            
            Texture2D<float> PerlinTex;
            SamplerState samplerPerlinTex;

            Texture2D<float4> BlueNoiseTex;
            SamplerState samplerBlueNoiseTex;

            //Cloud Settings
            float cloudDensity;
            float cloudAbsorption;
            float cloudCoverage;
            float cloudScale;
            float perlinScale;
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
            float relativeStepSize;
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
                float a = dot(dir, dir);
                float b = 2 * dot((pos - sphereCenter), dir);
                float c = dot((pos - sphereCenter), (pos - sphereCenter)) - radius * radius;

                float d = b * b - 4 * a * c;

                if (d < 0.0)
                    return -1.0;

                return float2(max(0.0, (-b - sqrt(d)) / (2 * a)), (-b + sqrt(d)) / (2 * a));
            }

            float SampleDensity(float3 worldPos)
            {
                float3 offset = worldPos - sphereCenter;
                float r = length(offset);
                float4 shape = CloudTex.SampleLevel(samplerCloudTex, offset / cloudScale + cloudOffset + offsetSpeed * time, 0);
                float falloff = exp(-(abs(cloudLayerHeight - (r - surfaceRadius))) / cloudLayerSpread);
                float2 spherical = float2(acos(offset.y / r), atan2(offset.z, offset.x));
                float weather = 0.5 * (PerlinTex.SampleLevel(samplerPerlinTex, perlinScale * spherical, 0) + 1.0);
                return (dot(cloudShapeWeights, shape) * falloff * weather + cloudCoverage - 1.0) * cloudDensity;
            }

            float SampleLightRay(float3 pos)
            {
                float3 rayPos = pos;
                float3 rayDir = -lightDir;

                float2 surfIntersect = RaySphereIntersect(rayPos, rayDir, surfaceRadius);
                if (surfIntersect.y > 0.0)
                    return 0.0;

                float2 intersect = RaySphereIntersect(rayPos, rayDir, surfaceRadius + maxCloudHeight);
                float step = (intersect.y - intersect.x) / numLightSamplePoints;

                float density = 0.0;

                for (int i = 0; i < numLightSamplePoints; i++)
                {
                    density += step * max(0.0, SampleDensity(rayPos));
                    rayPos += step * rayDir;
                }

                //return exp(-density * cloudAbsorption);
                return 2.0 * exp(-density * cloudAbsorption) * (1.0 - exp(-2.0 * density * cloudAbsorption));
            }

            float4 frag(v2f i) : SV_Target
            {
                float3 camPos = _WorldSpaceCameraPos;
                float viewLength = length(i.viewDir);
                float3 viewDir = i.viewDir / viewLength;

                float2 intersect = RaySphereIntersect(camPos, viewDir, surfaceRadius + maxCloudHeight);
                float2 surfIntersect = RaySphereIntersect(camPos, viewDir, surfaceRadius);
                float nonLinerarDepth = SAMPLE_DEPTH_TEXTURE(_CameraDepthTexture, i.uv);
                float depth = LinearEyeDepth(nonLinerarDepth) * viewLength;

                if (intersect.y < 0.0)
                    return float4(0.0, 0.0, 0.0, 1.0);

                float rayDist = 0;
                float maxRayDist = 0;

                if (surfIntersect.y > 0.0 && surfIntersect.x < 0.0)
                {
                    rayDist = surfIntersect.y;
                    maxRayDist = intersect.y - surfIntersect.y;
                }
                else
                {
                    rayDist = max(0.0, intersect.x);
                    maxRayDist = surfIntersect.x > 0.0 ? surfIntersect.x : intersect.y;
                }

                if (depth < maxDepth)
                    maxRayDist = min(maxRayDist, depth);

                float cosAngle = dot(viewDir, -lightDir);
                float phaseVal = Phase(cosAngle);

                float3 rayPos;
                rayDist += startOffsetStrength * BlueNoiseTex.SampleLevel(samplerBlueNoiseTex, blueNoiseScale * i.uv, 0).r;
                float step = 0.1 * relativeStepSize * maxCloudHeight;
                int iter = 0;

                float transmittance = 1.0;
                float lightEnergy = 0.0;

                while (rayDist < maxRayDist && iter < 250)
                {
                    rayPos = camPos + rayDist * viewDir;

                    float density = SampleDensity(rayPos);

                    if (density > 0.0)
                    {
                        float lightTransmittance = SampleLightRay(rayPos);

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

            sampler2D _MainTex;
            sampler2D _CameraDepthTexture;

            Texture2D<float4> TempTex;
            SamplerState samplerTempTex;

            Texture2D<float> TempDepthTex;
            SamplerState samplerTempDepthTex;

            float4 frag(v2f i) : SV_Target
            {
                float3 col = tex2D(_MainTex, i.uv);
                float4 clouds;

                float d0 = SAMPLE_DEPTH_TEXTURE(_CameraDepthTexture, i.uv);
                float d1 = TempDepthTex.SampleLevel(samplerTempDepthTex, i.uv, int2(1, 0), 0);
                float d2 = TempDepthTex.SampleLevel(samplerTempDepthTex, i.uv, int2(-1, 0), 0);
                float d3 = TempDepthTex.SampleLevel(samplerTempDepthTex, i.uv, int2(0, 1), 0);
                float d4 = TempDepthTex.SampleLevel(samplerTempDepthTex, i.uv, int2(0, -1), 0);

                d1 = abs(d0 - d1);
                d2 = abs(d0 - d2);
                d3 = abs(d0 - d3);
                d4 = abs(d0 - d4);

                float dmin = min(min(d1, d2), min(d3, d4));

                if(dmin == d1)
                    clouds = TempTex.SampleLevel(samplerTempTex, i.uv, int2(1, 0), 0);
                else if(dmin == d2)
                    clouds = TempTex.SampleLevel(samplerTempTex, i.uv, int2(-1, 0), 0);
                else if (dmin == d3)
                    clouds = TempTex.SampleLevel(samplerTempTex, i.uv, int2(0, 1), 0);
                else if (dmin == d4)
                    clouds = TempTex.SampleLevel(samplerTempTex, i.uv, int2(0, -1), 0);

                return float4(col * clouds.a + clouds.rgb, 0);
            }
            ENDCG
        }
    }
}
