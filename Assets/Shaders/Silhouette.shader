Shader "Custom/Silhouette" {
    Properties {
        _MainTex ("Texture", 2D) = "white" {}
        _Color ("Color", Color) = (1,1,1,1)
        _OutlineColor ("Silhouette Color", Color) = (0,0,0,1)
        _OutlineThickness ("Silhouette Thickness", Range(0.0, 0.1)) = 0.03
    }
    SubShader {
        Tags { "RenderType"="Opaque" }
        LOD 200

        // ----------------------------
        // Passaggio BASE (rendering normale del player)
        // ----------------------------
        Pass {
            Name "BASE"
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "UnityCG.cginc"

            sampler2D _MainTex;
            float4 _Color;

            struct appdata {
                float4 vertex : POSITION;
                float2 uv : TEXCOORD0;
            };

            struct v2f {
                float4 pos : SV_POSITION;
                float2 uv : TEXCOORD0;
            };

            v2f vert (appdata v) {
                v2f o;
                o.pos = UnityObjectToClipPos(v.vertex);
                o.uv = v.uv;
                return o;
            }

            fixed4 frag (v2f i) : SV_Target {
                fixed4 col = tex2D(_MainTex, i.uv) * _Color;
                return col;
            }
            ENDCG
        }

        // ----------------------------
        // Passaggio SILHOUETTE
        // - Usa ZTest Greater: disegna solo se il valore di profondità del pixel dell'outline
        //   è maggiore di quello già scritto (cioè, se il player è dietro un muro).
        // - Usa Cull Front per disegnare il modello "espanso" solo nelle aree occluse.
        // NOTA: È stato rimosso l'Offset.
        // ----------------------------
        Pass {
            Name "SILHOUETTE"
            Tags { "LightMode" = "Always" }
            Cull Front
            ZTest Greater
            ZWrite Off
            Blend SrcAlpha OneMinusSrcAlpha
            // Rimosso: Offset 1, 1

            CGPROGRAM
            #pragma vertex vertOutline
            #pragma fragment fragOutline
            #include "UnityCG.cginc"

            float _OutlineThickness;
            fixed4 _OutlineColor;

            struct appdata {
                float4 vertex : POSITION;
                float3 normal : NORMAL;
            };

            struct v2f {
                float4 pos : SV_POSITION;
            };

            v2f vertOutline (appdata v) {
                v2f o;
                float3 norm = normalize(v.normal);
                o.pos = UnityObjectToClipPos(v.vertex + float4(norm * _OutlineThickness, 0));
                return o;
            }

            fixed4 fragOutline (v2f i) : SV_Target {
                return _OutlineColor;
            }
            ENDCG
        }
    }
    FallBack "Diffuse"
}
