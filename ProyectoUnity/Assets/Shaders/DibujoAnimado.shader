// Estilo caricatura para URP: luz por bandas, brillo en el borde y contorno negro.
Shader "Juego/DibujoAnimado"
{
    Properties
    {
        _BaseColor ("Color", Color) = (1, 0.55, 0.1, 1)
        _BaseMap ("Textura", 2D) = "white" {}
        _ColorSombra ("Color de sombra", Color) = (0.75, 0.3, 0.1, 1)
        _Bandas ("Bandas de luz", Range(2, 5)) = 3
        _ColorBorde ("Brillo de borde", Color) = (1, 0.9, 0.7, 1)
        _FuerzaBorde ("Fuerza del borde", Range(0, 1)) = 0.35
        _ColorContorno ("Color del contorno", Color) = (0.12, 0.05, 0.02, 1)
        _GrosorContorno ("Grosor del contorno", Range(0, 0.02)) = 0.004
    }

    SubShader
    {
        Tags { "RenderType" = "Opaque" "RenderPipeline" = "UniversalPipeline" "Queue" = "Geometry" }

        // ---------- cuerpo con luz por bandas ----------
        Pass
        {
            Name "Cuerpo"
            Tags { "LightMode" = "UniversalForward" }
            Cull Off // algunas piezas del modelo (los pies) tienen las caras invertidas

            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"

            TEXTURE2D(_BaseMap); SAMPLER(sampler_BaseMap);

            CBUFFER_START(UnityPerMaterial)
                float4 _BaseMap_ST;
                half4 _BaseColor;
                half4 _ColorSombra;
                half _Bandas;
                half4 _ColorBorde;
                half _FuerzaBorde;
                half4 _ColorContorno;
                half _GrosorContorno;
            CBUFFER_END

            struct Atributos
            {
                float4 posicion : POSITION;
                float3 normal : NORMAL;
                float2 uv : TEXCOORD0;
            };

            struct Varyings
            {
                float4 posicionCS : SV_POSITION;
                float3 normalWS : TEXCOORD0;
                float3 vistaWS : TEXCOORD1;
                float2 uv : TEXCOORD2;
            };

            Varyings vert(Atributos i)
            {
                Varyings o;
                VertexPositionInputs p = GetVertexPositionInputs(i.posicion.xyz);
                o.posicionCS = p.positionCS;
                o.normalWS = TransformObjectToWorldNormal(i.normal);
                o.vistaWS = GetWorldSpaceViewDir(p.positionWS);
                o.uv = TRANSFORM_TEX(i.uv, _BaseMap);
                return o;
            }

            half4 frag(Varyings i, FRONT_FACE_TYPE cara : FRONT_FACE_SEMANTIC) : SV_Target
            {
                float3 n = normalize(i.normalWS);
                n *= IS_FRONT_VFACE(cara, 1.0, -1.0);
                Light luz = GetMainLight();
                // luz envuelta para que las sombras no queden muy negras
                half d = saturate(dot(n, luz.direction) * 0.5 + 0.5);
                half banda = floor(d * _Bandas) / (_Bandas - 1);
                banda = saturate(banda);

                half4 albedo = SAMPLE_TEXTURE2D(_BaseMap, sampler_BaseMap, i.uv) * _BaseColor;
                half3 color = lerp(albedo.rgb * _ColorSombra.rgb, albedo.rgb, banda);
                color *= lerp(half3(1, 1, 1), luz.color, 0.5);

                half borde = 1.0 - saturate(dot(n, normalize(i.vistaWS)));
                borde = step(0.65, borde) * _FuerzaBorde;
                color = lerp(color, _ColorBorde.rgb, borde);
                return half4(color, 1);
            }
            ENDHLSL
        }

        // ---------- contorno: casco invertido empujado en espacio de pantalla ----------
        Pass
        {
            Name "Contorno"
            Tags { "LightMode" = "SRPDefaultUnlit" }
            Cull Front

            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            CBUFFER_START(UnityPerMaterial)
                float4 _BaseMap_ST;
                half4 _BaseColor;
                half4 _ColorSombra;
                half _Bandas;
                half4 _ColorBorde;
                half _FuerzaBorde;
                half4 _ColorContorno;
                half _GrosorContorno;
            CBUFFER_END

            struct Atributos { float4 posicion : POSITION; float3 normal : NORMAL; };
            struct Varyings { float4 posicionCS : SV_POSITION; };

            Varyings vert(Atributos i)
            {
                Varyings o;
                float4 cs = TransformObjectToHClip(i.posicion.xyz);
                float3 nWS = TransformObjectToWorldNormal(i.normal);
                float3 nCS = mul((float3x3)UNITY_MATRIX_VP, nWS);
                float2 dir = normalize(nCS.xy + 1e-5);
                // se multiplica por w para que el grosor sea igual a cualquier distancia
                cs.xy += dir * _GrosorContorno * cs.w;
                o.posicionCS = cs;
                return o;
            }

            half4 frag(Varyings i) : SV_Target { return _ColorContorno; }
            ENDHLSL
        }

        // sombras proyectadas por el personaje
        Pass
        {
            Name "ShadowCaster"
            Tags { "LightMode" = "ShadowCaster" }
            ColorMask 0

            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Shadows.hlsl"

            float3 _LightDirection;

            struct Atributos { float4 posicion : POSITION; float3 normal : NORMAL; };
            struct Varyings { float4 posicionCS : SV_POSITION; };

            Varyings vert(Atributos i)
            {
                Varyings o;
                float3 pWS = TransformObjectToWorld(i.posicion.xyz);
                float3 nWS = TransformObjectToWorldNormal(i.normal);
                o.posicionCS = TransformWorldToHClip(ApplyShadowBias(pWS, nWS, _LightDirection));
                return o;
            }

            half4 frag(Varyings i) : SV_Target { return 0; }
            ENDHLSL
        }
    }
}
