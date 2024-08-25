Shader "Hidden/RaymarchDebug"
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
            Name "Raymarch"

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

            Texture3D<float> NoiseTex;
            SamplerState samplerNoiseTex;

            Texture2D<float4> BlueNoiseTex;
            SamplerState samplerBlueNoiseTex;

            Texture2D<float4> HistoryTex;
            SamplerState samplerHistoryTex;

            float volumeDensity;
            float densityOffset;
            float3 noiseOffset;
            float noiseScale;
            float stepSize;
            int lightSamples;
            float lightSampleStepMult;
            float3 containerOffset;
            float containerRadius;
            float3 lightDir;
            float hgBlendVal;
            float4 hgPhaseParams;
            float2 blueNoiseScale;
            float2 blueNoiseOffset;
            float jitterFactor;
            float historyBlend;
            matrix reprojMat;

            float HenyeyGreenstein(float a, float g) {
                float g2 = g * g;
                return (1 - g2) / (4 * 3.14159265 * pow(1 + g2 - 2 * g * (a), 1.5));
            }

            float Phase(float a) {
                float blend = .5;
                float hgBlend = HenyeyGreenstein(a, hgPhaseParams.x) * (1 - blend) + HenyeyGreenstein(a, -hgPhaseParams.y) * blend;
                return hgPhaseParams.z + hgBlend * hgPhaseParams.w;
            }

            float2 BoxIntersect(float3 boundsMin, float3 boundsMax, float3 rayOrigin, float3 invRaydir) {
                float3 t0 = (boundsMin - rayOrigin) * invRaydir;
                float3 t1 = (boundsMax - rayOrigin) * invRaydir;
                float3 tmin = min(t0, t1);
                float3 tmax = max(t0, t1);
                
                float dstA = max(max(tmin.x, tmin.y), tmin.z);
                float dstB = min(tmax.x, min(tmax.y, tmax.z));

                // CASE 1: ray intersects box from outside (0 <= dstA <= dstB)
                // dstA is dst to nearest intersection, dstB dst to far intersection

                // CASE 2: ray intersects box from inside (dstA < 0 < dstB)
                // dstA is the dst to intersection behind the ray, dstB is dst to forward intersection

                // CASE 3: ray misses box (dstA > dstB)

                float dstToBox = max(0, dstA);
                float dstInsideBox = max(0, dstB - dstToBox);
                return float2(dstToBox, dstInsideBox);
            }

            float2 ContainerIntersect(float3 pos, float3 dir) {
                float3 posOffset = pos - containerOffset;
                float a = dot(dir, dir);
                float b = 2 * dot(posOffset, dir);
                float c = dot(posOffset, posOffset) - containerRadius * containerRadius;

                float d = b * b - 4 * a * c;

                if (d < 0.0) {
                    return -1.0;
                }

                float sqrtD = sqrt(d);
                return float2(max(0.0, (-b - sqrtD) / (2 * a)), (-b + sqrtD) / (2 * a));
            }

            float SampleDensity(float3 worldPos) {
                float noise = NoiseTex.SampleLevel(samplerNoiseTex, noiseOffset + noiseScale * worldPos, 0);
                
                float falloff = 1.0 - length(worldPos - containerOffset) / containerRadius;
                return volumeDensity * (noise + densityOffset - 1);
            }

            float SampleLightRay(float3 pos) {
                float3 rayDir = -lightDir;

                float2 intersect = ContainerIntersect(pos, rayDir);
                if (intersect.y - intersect.x < 1e-3) {
                    return 0.0;
                }

                float d = 0.0;
                float rayDist = intersect.x;
                float3 rayPos = pos + rayDist * rayDir;
                float step = lightSampleStepMult * stepSize;
                int iter = 0;
                while(rayDist < intersect.y && iter < lightSamples) {
                    float thisStep = min(intersect.y - rayDist, step);
                    rayDist += step;
                    rayPos += thisStep * rayDir;
                    d += thisStep * max(0.0, SampleDensity(rayPos));
                    iter++;
                }

                return exp(-d);
            }

            float4 frag(v2f i) : SV_Target
            {
                float3 camPos = _WorldSpaceCameraPos;
                float viewLength = length(i.viewDir);
                float3 viewDir = i.viewDir / viewLength;

                float2 intersect = ContainerIntersect(camPos, viewDir);
                float depth = LinearEyeDepth(SAMPLE_DEPTH_TEXTURE(_CameraDepthTexture, i.uv)) * viewLength;
                float rayDistOffset = jitterFactor * stepSize * BlueNoiseTex.SampleLevel(samplerBlueNoiseTex, blueNoiseScale * (i.uv + blueNoiseOffset), 0).r;

                if (intersect.y < 0.0) {
                    return float4(0.0, 0.0, 0.0, 1.0);
                }

                float phaseVal = Phase(dot(viewDir, -lightDir));

                float transmittance = 1.0;
                float3 lightEnergy = 0.0;
                
                float rayDist = intersect.x + rayDistOffset;
                float3 rayPos = camPos + rayDist * viewDir;
                float maxRayDist = min(depth, intersect.y);
                int iter = 0;

                float minHitDist = 1e10;

                while(rayDist < maxRayDist && iter < 500) {
                    float density = SampleDensity(rayPos);

                    if (density > 0.0) {
                        minHitDist = min(minHitDist, rayDist);

                        float lightTransmittance = SampleLightRay(rayPos);

                        lightEnergy += density * stepSize * transmittance * lightTransmittance * phaseVal;
                        transmittance *= exp(-density * stepSize);

                        if (transmittance < 0.01) {
                            break;
                        }
                    }

                    rayDist += stepSize;
                    rayPos += stepSize * viewDir;
                    iter++;
                }

                float4 raymarchOutput = float4(lightEnergy, (transmittance-0.01)/0.99);

                float4 reproj = mul(reprojMat, float4(camPos + minHitDist * viewDir, 1));
                float2 reprojUV = 0.5 * (reproj.xy / reproj.w) + 0.5;
                float4 history = HistoryTex.SampleLevel(samplerHistoryTex, reprojUV.xy, 0);
                
                bool badSample = minHitDist > intersect.y || min(reprojUV.x,reprojUV.y) < 0.0 || max(reprojUV.x,reprojUV.y) > 1.0;

                return badSample ? raymarchOutput : (1.0 - historyBlend) * raymarchOutput + historyBlend * history;
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

            Texture2D<float4> RaymarchTex;
            SamplerState samplerRaymarchTex;

            float4 frag(v2f i) : SV_Target
            {
                float3 col = tex2D(_MainTex, i.uv);
                float4 output = RaymarchTex.Sample(samplerRaymarchTex, i.uv);

                return float4(output.a * col + output.rgb , 0);
            }
            ENDCG
        }
    }
}
