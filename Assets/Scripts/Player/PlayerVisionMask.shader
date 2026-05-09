Shader "Hidden/PlayerVisionMask"
{
    Properties
    {
        [HideInInspector] _MainTex ("Dummy", 2D) = "white" {}
    }

    SubShader
    {
        Tags
        {
            "Queue" = "Transparent"
            "RenderType" = "Transparent"
            "IgnoreProjector" = "True"
        }

        Cull Off
        ZWrite Off
        ZTest Always
        Blend SrcAlpha OneMinusSrcAlpha

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
                float4 vertex : SV_POSITION;
                float2 uv : TEXCOORD0;
            };

            fixed4 _DarknessColor;
            float _DarknessAlpha;
            float4 _PlayerViewportPos;
            float4 _FacingDir;
            float _InnerRadius;
            float _ConeRange;
            float _ConeHalfAngleCos;
            float _EdgeSoftness;
            float _ConeAngleSoftness;
            float _Aspect;

            v2f vert(appdata v)
            {
                v2f o;
                o.vertex = UnityObjectToClipPos(v.vertex);
                o.uv = v.uv;
                return o;
            }

            fixed4 frag(v2f i) : SV_Target
            {
                float2 delta = i.uv - _PlayerViewportPos.xy;
                delta.x *= _Aspect;
                float dist = length(delta);

                // 玩家身边小圈常亮
                float circleVisible = 1.0 - smoothstep(_InnerRadius, _InnerRadius + _EdgeSoftness, dist);

                // 手电筒锥形可视
                float2 dir = _FacingDir.xy;
                dir.x *= _Aspect;
                dir /= max(length(dir), 1e-6);

                float2 deltaDir = delta / max(dist, 1e-6);
                float angleDot = dot(deltaDir, dir);
                float angleMask = smoothstep(_ConeHalfAngleCos, _ConeHalfAngleCos + _ConeAngleSoftness, angleDot);
                float distMask = 1.0 - smoothstep(_ConeRange, _ConeRange + _EdgeSoftness, dist);
                float coneVisible = angleMask * distMask;

                float visible = saturate(max(circleVisible, coneVisible));
                float alpha = _DarknessAlpha * (1.0 - visible);

                return fixed4(_DarknessColor.rgb, alpha);
            }
            ENDCG
        }
    }
}
