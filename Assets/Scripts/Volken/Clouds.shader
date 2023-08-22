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
            Name "FarDepth"

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

            float2 clipPlanes;

            float4 frag(v2f i) : SV_Target
            {
                float rawDepth = SAMPLE_DEPTH_TEXTURE(_CameraDepthTexture, i.uv);

                return rawDepth > 0.0 ? LinearEyeDepth(rawDepth) : clipPlanes.y;
            }
            ENDCG
        }

        Pass
        {
            Name "NearDepth"

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

            float4 frag(v2f i) : SV_Target
            {
                float rawDepth = SAMPLE_DEPTH_TEXTURE(_CameraDepthTexture, i.uv);
                float farDepth = tex2D(_MainTex, i.uv);

                return rawDepth > 0.0 ? LinearEyeDepth(rawDepth) : farDepth;
            }
            ENDCG
        }

        Pass
        {
            Name "DownsampleDepth"

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

            float4 frag(v2f i) : SV_Target
            {
                return tex2D(_MainTex, i.uv);
            }
            ENDCG
        }

        Pass
        {
            Name "TexVisualize"

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

            float4 frag(v2f i) : SV_Target
            {
                return tex2D(_MainTex, i.uv) / 100000.0;
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

            Texture2D<float> DepthTex;
            SamplerState samplerDepthTex;

            Texture3D<float> CloudShapeTex;
            SamplerState samplerCloudShapeTex;

            Texture3D<float> CloudDetailTex;
            SamplerState samplerCloudDetailTex;

            Texture2D<float> PerlinTex;
            SamplerState samplerPerlinTex;

            Texture2D<float> DomainWarpTex;
            SamplerState samplerDomainWarpTex;

            Texture2D<float4> BlueNoiseTex;
            SamplerState samplerBlueNoiseTex;

            //Cloud Settings
            float cloudDensity;
            float cloudAbsorption;
            float cloudCoverage;
            float cloudScale;
            float detailScale;
            float detailStrength;
            float perlinScale;
            float perlinStrength;
            float domainWarpStrength;
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
            float stepSize;
            int numLightSamplePoints;

            //Misc
            float3 lightDir;
            float4 lightColor;
            float blueNoiseScale;
            float startOffsetStrength;
            float maxDepth;
            float scatterStrength;

            float HenyeyGreenstein(float a, float g) {
                float g2 = g * g;
                return (1 - g2) / (4 * 3.1415 * pow(1 + g2 - 2 * g * (a), 1.5));
            }

            float Phase(float a) {
                float blend = .5;
                float hgBlend = HenyeyGreenstein(a, phaseParams.x) * (1 - blend) + HenyeyGreenstein(a, -phaseParams.y) * blend;
                return phaseParams.z + hgBlend * phaseParams.w;
            }

            float Beer(float d)
            {
                return exp(-d * cloudAbsorption);
            }

            float BeersPowder(float d)
            {
                return 2.0 * exp(-d * cloudAbsorption) * (1.0 - exp(-2.0 * d * cloudAbsorption));
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
                float3 offset = worldPos - sphereCenter;
                float r = length(offset);

                float shape = CloudShapeTex.SampleLevel(samplerCloudShapeTex, offset / cloudScale + cloudOffset, 0);
                float detail = CloudDetailTex.SampleLevel(samplerCloudDetailTex, offset / detailScale + cloudOffset, 0);

                float falloffExponent = log(max(0.01, ((r - surfaceRadius) - cloudLayerHeight) / cloudLayerSpread));
                float falloff = exp(-falloffExponent * falloffExponent);

                float2 spherical = float2(acos(offset.y / r), atan2(offset.z, offset.x));
                float domainWarp = domainWarpStrength * 0.1 * (DomainWarpTex.SampleLevel(samplerDomainWarpTex, perlinScale * spherical, 0) + 1.0);
                float weather = perlinStrength * PerlinTex.SampleLevel(samplerPerlinTex, perlinScale * spherical + domainWarp, 0);

                float detailNoise = (1.0 - shape) * (1.0 - shape) * detailStrength * detail;

                return ((shape - detailNoise + weather) * falloff + cloudCoverage - 1.0) * cloudDensity;
            }

            float2 SampleLightRay(float3 pos)
            {
                float3 rayPos = pos;
                float3 rayDir = -lightDir;

                float2 surfIntersect = RaySphereIntersect(rayPos, rayDir, surfaceRadius);
                if (surfIntersect.y > 0.0)
                    return 0.0;

                float2 intersect = RaySphereIntersect(rayPos, rayDir, surfaceRadius + maxCloudHeight);
                float step = min(4.0 * stepSize, (intersect.y - max(0.0, intersect.x)) / numLightSamplePoints);

                float density = 0.0;

                for (int i = 0; i < numLightSamplePoints; i++)
                {
                    density += step * max(0.0, SampleDensity(rayPos));
                    rayPos += step * rayDir;
                }

                return float2(density, intersect.y - max(0.0, intersect.x));
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
                float depth = viewLength * DepthTex.SampleLevel(samplerDepthTex, i.uv, 0);

                float rayDist = surfIntersect.x * surfIntersect.y < 0.0 ? surfIntersect.y : max(0.0, intersect.x);
                float maxRayDist = surfIntersect.y > 0.0 ? surfIntersect.x : intersect.y;
                maxRayDist = min(maxRayDist, depth);

                if (maxRayDist - rayDist <= 0.0)
                    return float4(0.0, 0.0, 0.0, 1.0);

                rayDist += startOffsetStrength * BlueNoiseTex.SampleLevel(samplerBlueNoiseTex, blueNoiseScale * i.uv, 0).r;
                int iter = 0;

                float cosAngle = dot(viewDir, -lightDir);
                float phaseVal = Phase(cosAngle);

                float transmittance = 1.0;
                float3 lightEnergy = 0.0;
                
                float3 rayPos;
                float3 lightTransmittance;
                float density;

                float3 wavelengths = float3(700, 530, 440);
                float3 scatterCoeff = pow(1.0 / wavelengths, 4) * scatterStrength;

                float stepSizeMultiplier = 1.0;
                int emptySamples = 0;

                while(rayDist < maxRayDist && iter < 350)
                {
                    rayPos = camPos + rayDist * viewDir;
                    density = SampleDensity(rayPos);

                    if(stepSizeMultiplier == 2.0 && density > 0.0)
                    {
                        rayDist -= stepSize * stepSizeMultiplier;
                        rayPos = camPos + rayDist * viewDir;
                        density = SampleDensity(rayPos);
                        stepSizeMultiplier = 1.0;
                        emptySamples = 0;
                    }

                    if (density > 0.0)
                    {
                        float2 lightSample = SampleLightRay(rayPos);

                        lightTransmittance = BeersPowder(lightSample.x) * exp(-lightSample.y * lightSample.y * scatterCoeff);
                        lightEnergy += density * stepSize * transmittance * phaseVal * lightTransmittance;
                        transmittance *= Beer(density * stepSize);

                        if (transmittance < 0.01)
                            break;
                    }
                    else if (stepSizeMultiplier == 1.0)
                    {
                        emptySamples++;
                        
                        if(emptySamples > 3)
                            stepSizeMultiplier = 2.0;
                    }

                    rayDist += stepSize * stepSizeMultiplier;
                    iter++;
                }
                
                float shadowTransmittance = 1.0;
                if (surfIntersect.y > 0.0 || depth < maxDepth)
                    shadowTransmittance = 0.25 + 0.75 * Beer(SampleLightRay(camPos + (maxRayDist - 50.0) * viewDir).x);
                transmittance *= shadowTransmittance;

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

            Texture2D<float4> CloudTex;
            SamplerState samplerCloudTex;

            Texture2D<float> CombinedDepthTex;
            SamplerState samplerCombinedDepthTex;

            Texture2D<float> LowResDepthTex;
            SamplerState samplerLowResDepthTex;

            float2 resolution;
            float depthDifferenceThreshold;
            
            /*
            float4 cubic(float x)
            {
                float x2 = x * x;
                float x3 = x2 * x;
                float4 w;
                w.x = -x3 + 3.0 * x2 - 3.0 * x + 1.0;
                w.y = 3.0 * x3 - 6.0 * x2 + 4.0;
                w.z = -3.0 * x3 + 3.0 * x2 + 3.0 * x + 1.0;
                w.w = x3;
                return w / 6.0;
            }

            float4 BicubicTexture(float2 coord)
            {
                coord *= resolution;

                float fx = frac(coord.x);
                float fy = frac(coord.y);
                coord.x -= fx;
                coord.y -= fy;

                fx -= 0.5;
                fy -= 0.5;

                float4 xcubic = cubic(fx);
                float4 ycubic = cubic(fy);

                float4 c = float4(coord.x - 0.5, coord.x + 1.5, coord.y - 0.5, coord.y + 1.5);
                float4 s = float4(xcubic.x + xcubic.y, xcubic.z + xcubic.w, ycubic.x + ycubic.y, ycubic.z + ycubic.w);
                float4 offset = c + float4(xcubic.y, xcubic.w, ycubic.y, ycubic.w) / s;

                float4 sample0 = CloudTex.Sample(samplerCloudTex, float2(offset.x, offset.z) / resolution);
                float4 sample1 = CloudTex.Sample(samplerCloudTex, float2(offset.y, offset.z) / resolution);
                float4 sample2 = CloudTex.Sample(samplerCloudTex, float2(offset.x, offset.w) / resolution);
                float4 sample3 = CloudTex.Sample(samplerCloudTex, float2(offset.y, offset.w) / resolution);

                float sx = s.x / (s.x + s.y);
                float sy = s.z / (s.z + s.w);

                return lerp(lerp(sample3, sample2, sx), lerp(sample1, sample0, sx), sy);
            }
            */

            float4 frag(v2f i) : SV_Target
            {
                
                float3 col = tex2D(_MainTex, i.uv);
                float4 clouds = float4(0, 0, 0, 1);
                

                float d0 = CombinedDepthTex.Sample(samplerCombinedDepthTex, i.uv);
                float d1 = LowResDepthTex.Sample(samplerLowResDepthTex, i.uv, int2(0, 1));
                float d2 = LowResDepthTex.Sample(samplerLowResDepthTex, i.uv, int2(0, -1));
                float d3 = LowResDepthTex.Sample(samplerLowResDepthTex, i.uv, int2(1, 0));
                float d4 = LowResDepthTex.Sample(samplerLowResDepthTex, i.uv, int2(-1, 0));

                d1 = abs(d0 - d1);
                d2 = abs(d0 - d2);
                d3 = abs(d0 - d3);
                d4 = abs(d0 - d4);

                float dmin = min(min(d1, d2), min(d3, d4));

                if(dmin / d0 < depthDifferenceThreshold)
                    clouds = CloudTex.Sample(samplerCloudTex, i.uv);
                else if(dmin == d1)
                    clouds = CloudTex.Sample(samplerCloudTex, i.uv, int2(0, 1));
                else if(dmin == d2)
                    clouds = CloudTex.Sample(samplerCloudTex, i.uv, int2(0, -1));
                else if(dmin == d3)
                    clouds = CloudTex.Sample(samplerCloudTex, i.uv, int2(1, 0));
                else if(dmin == d4)
                    clouds = CloudTex.Sample(samplerCloudTex, i.uv, int2(-1, 0));
                

                //clouds = BicubicTexture(i.uv);

                return float4(col * clouds.a + clouds.rgb, 0);
            }
            ENDCG
        }
    }
}
