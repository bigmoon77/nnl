Shader "NNL/Unlit MLP"
{
    Properties
    {
        _BaseColor ("Base Color", Color) = (1, 1, 1, 1)
        _NNLWeights ("MLP Weights (RGBAFloat)", 2D) = "black" {}
    }

    SubShader
    {
        Tags
        {
            "Queue" = "Geometry"
            "RenderType" = "Opaque"
            "VRCFallback" = "Toon"
        }

        Pass
        {
            Tags { "LightMode" = "ForwardBase" }
            Cull Back
            ZWrite On
            ZTest LEqual

            CGPROGRAM
            #pragma target 3.0
            #pragma vertex vert
            #pragma fragment frag
            #include "UnityCG.cginc"
            #include "Lighting.cginc"

            fixed4 _BaseColor;
            sampler2D _NNLWeights;
            float4 _NNLWeights_TexelSize;

            struct appdata
            {
                float4 vertex : POSITION;
                float3 normal : NORMAL;
            };

            struct v2f
            {
                float4 position : SV_POSITION;
                float3 world_position : TEXCOORD0;
                float3 world_normal : TEXCOORD1;
            };

            v2f vert(appdata input)
            {
                v2f output;
                output.position = UnityObjectToClipPos(input.vertex);
                output.world_position = mul(unity_ObjectToWorld, input.vertex).xyz;
                output.world_normal = UnityObjectToWorldNormal(input.normal);
                return output;
            }

            float4 LoadWeight(int x, int y)
            {
                float2 uv = (float2(x, y) + 0.5) * _NNLWeights_TexelSize.xy;
                return tex2Dlod(_NNLWeights, float4(uv, 0.0, 0.0));
            }

            float EvaluateHidden(int neuron, float4 input_value)
            {
                float4 weights = LoadWeight(0, neuron);
                float bias = LoadWeight(1, neuron).r;
                return max(dot(weights, input_value) + bias, 0.0);
            }

            float EvaluateOutput(int neuron, float4 hidden_a, float4 hidden_b)
            {
                int row = 8 + neuron;
                float4 weights_a = LoadWeight(0, row);
                float4 weights_b = LoadWeight(1, row);
                float bias = LoadWeight(2, row).r;
                return dot(weights_a, hidden_a) + dot(weights_b, hidden_b) + bias;
            }

            fixed4 frag(v2f input) : SV_Target
            {
                float3 normal = normalize(input.world_normal);
                float3 light_direction = normalize(UnityWorldSpaceLightDir(input.world_position));
                float ndotl = dot(normal, light_direction);

                // Network input: world normal (xyz) and signed N.L.
                float4 network_input = float4(normal, ndotl);

                // 4 -> 8 ReLU -> 3 MLP.
                float4 hidden_a = float4(
                    EvaluateHidden(0, network_input),
                    EvaluateHidden(1, network_input),
                    EvaluateHidden(2, network_input),
                    EvaluateHidden(3, network_input));

                float4 hidden_b = float4(
                    EvaluateHidden(4, network_input),
                    EvaluateHidden(5, network_input),
                    EvaluateHidden(6, network_input),
                    EvaluateHidden(7, network_input));

                float3 network_color = saturate(float3(
                    EvaluateOutput(0, hidden_a, hidden_b),
                    EvaluateOutput(1, hidden_a, hidden_b),
                    EvaluateOutput(2, hidden_a, hidden_b)));

                return fixed4(network_color * _BaseColor.rgb, _BaseColor.a);
            }
            ENDCG
        }
    }

    FallBack "Unlit/Color"
}
