// Upgrade NOTE: commented out 'float4x4 _CameraToWorld', a built-in variable
// Upgrade NOTE: replaced '_CameraToWorld' with 'unity_CameraToWorld'

Shader "Custom/rayTraceCustom"
{
    Properties
    {
        _MainTex ("Texture", 2D) = "white" {}
        _MainTexOld ("Texture", 2D) = "white" {}
    }
    SubShader
    {
        Tags { "RenderType"="Opaque" }
        LOD 100

        Pass
        {
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
                float2 uv : TEXCOORD0;
                UNITY_FOG_COORDS(1)
                float4 vertex : SV_POSITION;
            };

            sampler2D _MainTex;
            float4 _MainTex_ST;

            v2f vert (appdata v)
            {
                v2f o;
                o.vertex = UnityObjectToClipPos(v.vertex);
                o.uv = TRANSFORM_TEX(v.uv, _MainTex);
                UNITY_TRANSFER_FOG(o,o.vertex);
                return o;
            }

            struct Sphere {
                float3 position;
                float radius;
                float3 colour;
                float3 emissionColour;
                float emissionStrength;
            };

            struct Ray {
                float3 src;
                float3 dir;
            };

            struct RayCollision {
                float3 position;
                float distance;
                float3 normal;
                float3 colour;
                float3 emissionColour;
                float emissionStrength;
            };

            // float4x4 _CameraToWorld;
            float3 _ViewParams;
            int _Bounces;
            StructuredBuffer<Sphere> _Spheres;
            int _NumSpheres;
            int _NumRays;
            int _FrameNum;
            RWTexture2D<float4> Result;
            RWTexture2D<float4> _PrevFrame;

            // PCG from shadertoy.com / Sebastian Lague
            float randomNumber(inout uint state) {
                
                state = state * 747796405 + 2891336453;
                uint result = ((state >> ((state >> 28) + 4)) ^ state) * 277803737;
                result = (result >> 22) ^ result;
                return result / 4294967295.0;
            } 

            float3 randomHemisphereDirection(float3 hitNormal, inout uint rngState) {

                float x = randomNumber(rngState);
                float y = randomNumber(rngState);
                float z = randomNumber(rngState);
                float3 newDir = normalize(float3(x, y, z));

                return newDir;// * sign(dot(hitNormal, newDir));
            }

            RayCollision sphereCollision(Ray ray) {

                RayCollision closestIntersect;
                closestIntersect.distance = 1.#INF;

                for (int i = 0; i < _NumSpheres; i++) {

                    Sphere sphere = _Spheres[i];

                    float3 offsetRaySrc = ray.src - sphere.position;

                    float a = dot(ray.dir, ray.dir);
                    float b = 2 * dot(offsetRaySrc, ray.dir);
                    float c = dot(offsetRaySrc, offsetRaySrc) - sphere.radius * sphere.radius;

                    float disc = b * b - 4 * a * c;

                    // no intersection
                    if (disc < 0) {
                        continue;
                    }

                    float distanceToIntersection = (-b - sqrt(disc)) / (2*a);

                    if (distanceToIntersection >= 0 && distanceToIntersection < closestIntersect.distance) {
                        closestIntersect.distance = distanceToIntersection;
                        closestIntersect.normal = normalize(closestIntersect.position - sphere.position.xyz);
                        closestIntersect.position = ray.src + ray.dir * distanceToIntersection;
                        closestIntersect.colour = sphere.colour;
                        closestIntersect.emissionColour = sphere.emissionColour;
                        closestIntersect.emissionStrength = sphere.emissionStrength;
                    }
                }

                return closestIntersect;
            }

            float3 Trace(Ray ray, inout uint rngState) {

                float3 colour = float3(1,1,1);
                float3 light = float3(0,0,0);

                for (int i = 0; i < _Bounces; i++) {
                    RayCollision rc = sphereCollision(ray);

                    if (rc.distance < 1.#INF) {
                        ray.src = rc.position;
                        ray.dir = randomHemisphereDirection(rc.normal, rngState);
                        // ray.dir = reflect(ray.dir, rc.normal);

                        light += rc.emissionColour * rc.emissionStrength * colour;
                        colour *= rc.colour;
                        
                    } else {
                        break;
                    }
                }

                return light;
            }

            fixed4 frag (v2f i) : SV_Target
            {
                uint width, height;
                Result.GetDimensions(width, height);

                float3 viewPointID = float3(i.uv - 0.5, 1) * _ViewParams;
                float3 viewPoint = mul(unity_CameraToWorld, float4(viewPointID, 1));

                Ray ray;
                ray.src = _WorldSpaceCameraPos;
                ray.dir = normalize(viewPoint - ray.src);

                uint2 numPixels = uint2(width, height);
                uint2 pixelCoord = i.uv * numPixels;
                uint pixelIndex = pixelCoord.y * numPixels.x + pixelCoord.x;
                uint rngState = pixelIndex + _FrameNum * 980912;

                float3 sum = float3(0, 0, 0);
                for (int i = 0; i < _NumRays; i++) {
                    sum += Trace(ray, rngState);
                }

                float3 output = sum/_NumRays;
                float4 old = tex2D(_MainTexOld, i.uv);
                float4 new = tex2D(_MainTex, i.uv);

                float frameWeight = 1.0 / (_FrameNum + 1);
                float4 avg = old * (1.0 - frameWeight) + new * frameWeight;

                return avg;
            }
            ENDCG
        }
    }
}
