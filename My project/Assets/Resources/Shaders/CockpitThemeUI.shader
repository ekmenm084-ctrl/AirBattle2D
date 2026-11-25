Shader "UI/CockpitThemeRemap"
{
    Properties
    {
        _MainTex ("Sprite Texture", 2D) = "white" {}
        _Color ("Tint", Color) = (1,1,1,1)
        _ThemeMode ("Theme Mode", Float) = 0

        _StencilComp ("Stencil Comparison", Float) = 8
        _Stencil ("Stencil ID", Float) = 0
        _StencilOp ("Stencil Operation", Float) = 0
        _StencilWriteMask ("Stencil Write Mask", Float) = 255
        _StencilReadMask ("Stencil Read Mask", Float) = 255

        _CullMode ("Cull Mode", Float) = 0
        _ColorMask ("Color Mask", Float) = 15
        _ClipRect ("Clip Rect", vector) = (-32767, -32767, 32767, 32767)

        [Toggle(UNITY_UI_ALPHACLIP)] _UseUIAlphaClip ("Use Alpha Clip", Float) = 0
    }

    SubShader
    {
        Tags
        {
            "Queue"="Transparent"
            "IgnoreProjector"="True"
            "RenderType"="Transparent"
            "PreviewType"="Plane"
            "CanUseSpriteAtlas"="True"
        }

        Stencil
        {
            Ref [_Stencil]
            Comp [_StencilComp]
            Pass [_StencilOp]
            ReadMask [_StencilReadMask]
            WriteMask [_StencilWriteMask]
        }

        Cull [_CullMode]
        Lighting Off
        ZWrite Off
        ZTest [unity_GUIZTestMode]
        Blend SrcAlpha OneMinusSrcAlpha
        ColorMask [_ColorMask]

        Pass
        {
            Name "Default"
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma target 3.0

            #include "UnityCG.cginc"
            #include "UnityUI.cginc"

            #pragma multi_compile __ UNITY_UI_CLIP_RECT
            #pragma multi_compile __ UNITY_UI_ALPHACLIP

            struct appdata_t
            {
                float4 vertex   : POSITION;
                float4 color    : COLOR;
                float2 texcoord : TEXCOORD0;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            struct v2f
            {
                float4 vertex        : SV_POSITION;
                fixed4 color         : COLOR;
                float2 texcoord      : TEXCOORD0;
                float4 worldPosition : TEXCOORD1;
                float4 mask          : TEXCOORD2;
                UNITY_VERTEX_OUTPUT_STEREO
            };

            sampler2D _MainTex;
            fixed4 _Color;
            fixed4 _TextureSampleAdd;
            float4 _ClipRect;
            float4 _MainTex_ST;
            float _UIMaskSoftnessX;
            float _UIMaskSoftnessY;
            int _UIVertexColorAlwaysGammaSpace;
            float _ThemeMode;

            float3 RgbToHsv(float3 c)
            {
                float4 K = float4(0.0, -1.0 / 3.0, 2.0 / 3.0, -1.0);
                float4 p = lerp(float4(c.bg, K.wz), float4(c.gb, K.xy), step(c.b, c.g));
                float4 q = lerp(float4(p.xyw, c.r), float4(c.r, p.yzx), step(p.x, c.r));
                float d = q.x - min(q.w, q.y);
                float e = 1.0e-6;
                return float3(abs(q.z + (q.w - q.y) / (6.0 * d + e)), d / (q.x + e), q.x);
            }

            float3 HsvToRgb(float3 c)
            {
                float4 K = float4(1.0, 2.0 / 3.0, 1.0 / 3.0, 3.0);
                float3 p = abs(frac(c.xxx + K.xyz) * 6.0 - K.www);
                return c.z * lerp(K.xxx, saturate(p - K.xxx), c.y);
            }

            float3 GradeCockpitPaletteSrgb(float3 srgb, float mode)
            {
                if (mode < 0.5)
                    return srgb;

                float3 hsv = RgbToHsv(saturate(srgb));
                float h = hsv.x;
                float s = hsv.y;
                float v = hsv.z;

                // 1. Korunan İşlevsel Renkler:
                // - Kırmızı / Kızıl (ATEŞ tuşu, Zor butonu, Kapat, Düşman Canı, Bildirim Rozeti, P2 Joystick)
                float isRedHue = max(smoothstep(0.915, 0.955, h), 1.0 - smoothstep(0.035, 0.068, h));
                float redProtect = isRedHue * smoothstep(0.32, 0.52, s);

                // - Parlak Saf Altın (Altın para ikonları / sayaçları / Ulti halkası)
                float isGoldHue = smoothstep(0.095, 0.120, h) * (1.0 - smoothstep(0.155, 0.180, h));
                float goldProtect = isGoldHue * smoothstep(0.55, 0.75, s) * smoothstep(0.58, 0.78, v);

                // - Saf Parlak Beyaz Yazılar & Krom Harfler (okunabilirliği %100 korur)
                float whiteProtect = smoothstep(0.84, 0.96, v) * (1.0 - smoothstep(0.04, 0.13, s));

                float protectMask = saturate(max(redProtect, max(goldProtect * 0.85, whiteProtect)));

                // 2. Birincil Siber Mavi / Cyan / Arduvaz-Lacivert Bölgesi (135°..280°) -> %100 Tema Dönüşümü
                float blueBand = smoothstep(0.375, 0.435, h) * (1.0 - smoothstep(0.715, 0.785, h));

                // 3. Arka Plan Hangar Duvarları, Bulutlar ve Atmosferik Gölgeler -> Bütünsel Tema Atmosferi
                float ambientWeight = lerp(0.62, 1.0, blueBand) * (1.0 - protectMask);
                if (ambientWeight <= 0.002)
                    return srgb;

                float brightFactor = smoothstep(0.12, 0.74, v);
                float midTone = sin(saturate(v) * 3.14159);
                float bandOffset = clamp((h - 0.545) * 0.18, -0.020, 0.020);
                float3 targetHsv = hsv;

                if (mode < 1.5)
                {
                    // TEMA 1: RADAR YEŞİLİ (F-16 Gece Görüş & Taktik Zümrüt Kokpit)
                    targetHsv.x = frac(lerp(0.415, 0.390, brightFactor) + bandOffset * 0.45 + 1.0);
                    float ambientSatFloor = lerp(0.24, 0.40, midTone) * smoothstep(0.02, 0.18, v);
                    float boostedSat = max(s * lerp(0.85, 1.05, brightFactor), ambientSatFloor);
                    targetHsv.y = saturate(lerp(boostedSat * 0.78, boostedSat, blueBand));
                    targetHsv.z = saturate(v * lerp(0.95, 1.04, brightFactor));
                }
                else
                {
                    // TEMA 2: ALTIN KARBON (Black Ops Koyu Karbon & Kehribar Altın)
                    targetHsv.x = frac(lerp(0.088, 0.106, brightFactor) + bandOffset * 0.35 + 1.0);
                    float ambientSatFloor = lerp(0.20, 0.44, brightFactor) * smoothstep(0.03, 0.22, v);
                    float darkCarbonSat = s * lerp(0.42, 1.05, brightFactor);
                    float boostedSat = max(darkCarbonSat, ambientSatFloor);
                    targetHsv.y = saturate(lerp(boostedSat * 0.80, boostedSat, blueBand));
                    targetHsv.z = saturate(v * lerp(0.92, 1.06, brightFactor));
                }

                float3 themedRgb = HsvToRgb(targetHsv);
                return lerp(srgb, themedRgb, ambientWeight);
            }

            v2f vert(appdata_t v)
            {
                v2f OUT;
                UNITY_SETUP_INSTANCE_ID(v);
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(OUT);
                float4 vPosition = UnityObjectToClipPos(v.vertex);
                OUT.worldPosition = v.vertex;
                OUT.vertex = vPosition;

                float2 pixelSize = vPosition.w;
                pixelSize /= abs(mul((float2x2)UNITY_MATRIX_P, _ScreenParams.xy));

                float4 clampedRect = clamp(_ClipRect, -2e10, 2e10);
                OUT.texcoord = TRANSFORM_TEX(v.texcoord, _MainTex);
                OUT.mask = half4(v.vertex.xy * 2 - clampedRect.xy - clampedRect.zw, 0.25 / (0.25 * half2(_UIMaskSoftnessX, _UIMaskSoftnessY) + abs(pixelSize.xy)));

                if (_UIVertexColorAlwaysGammaSpace && !IsGammaSpace())
                {
                    v.color.rgb = UIGammaToLinear(v.color.rgb);
                }
                OUT.color = v.color * _Color;
                return OUT;
            }

            fixed4 frag(v2f IN) : SV_Target
            {
                half4 color = (tex2D(_MainTex, IN.texcoord) + _TextureSampleAdd) * IN.color;

                if (_ThemeMode > 0.5 && color.a > 0.001)
                {
                    bool linearSpace = !IsGammaSpace();
                    float3 srgb = linearSpace ? LinearToGammaSpace(max(color.rgb, 0.0)) : color.rgb;
                    float3 graded = GradeCockpitPaletteSrgb(srgb, _ThemeMode);
                    color.rgb = linearSpace ? GammaToLinearSpace(max(graded, 0.0)) : graded;
                }

                #if UNITY_UI_CLIP_RECT
                half2 m = saturate((_ClipRect.zw - _ClipRect.xy - abs(IN.mask.xy)) * IN.mask.zw);
                color *= m.x * m.y;
                #endif

                #ifdef UNITY_UI_ALPHACLIP
                clip(color.a - 0.001);
                #endif

                return color;
            }
            ENDCG
        }

        Pass
        {
            Name "BlitRemap"
            Cull Off
            Lighting Off
            ZWrite Off
            ZTest Always
            Blend Off

            CGPROGRAM
            #pragma vertex vertBlit
            #pragma fragment fragBlit
            #pragma target 3.0

            #include "UnityCG.cginc"

            struct appdata_blit
            {
                float4 vertex   : POSITION;
                float2 texcoord : TEXCOORD0;
            };

            struct v2f_blit
            {
                float4 vertex   : SV_POSITION;
                float2 texcoord : TEXCOORD0;
            };

            sampler2D _MainTex;
            float4 _MainTex_ST;
            float _ThemeMode;

            float3 RgbToHsvB(float3 c)
            {
                float4 K = float4(0.0, -1.0 / 3.0, 2.0 / 3.0, -1.0);
                float4 p = lerp(float4(c.bg, K.wz), float4(c.gb, K.xy), step(c.b, c.g));
                float4 q = lerp(float4(p.xyw, c.r), float4(c.r, p.yzx), step(p.x, c.r));
                float d = q.x - min(q.w, q.y);
                float e = 1.0e-6;
                return float3(abs(q.z + (q.w - q.y) / (6.0 * d + e)), d / (q.x + e), q.x);
            }

            float3 HsvToRgbB(float3 c)
            {
                float4 K = float4(1.0, 2.0 / 3.0, 1.0 / 3.0, 3.0);
                float3 p = abs(frac(c.xxx + K.xyz) * 6.0 - K.www);
                return c.z * lerp(K.xxx, saturate(p - K.xxx), c.y);
            }

            v2f_blit vertBlit(appdata_blit v)
            {
                v2f_blit o;
                o.vertex = UnityObjectToClipPos(v.vertex);
                o.texcoord = TRANSFORM_TEX(v.texcoord, _MainTex);
                return o;
            }

            fixed4 fragBlit(v2f_blit i) : SV_Target
            {
                half4 color = tex2D(_MainTex, i.texcoord);
                if (_ThemeMode > 0.5 && color.a > 0.001)
                {
                    bool linearSpace = !IsGammaSpace();
                    float3 srgb = linearSpace ? LinearToGammaSpace(max(color.rgb, 0.0)) : color.rgb;

                    float3 hsv = RgbToHsvB(saturate(srgb));
                    float h = hsv.x;
                    float s = hsv.y;
                    float v = hsv.z;

                    float isRedHue = max(smoothstep(0.915, 0.955, h), 1.0 - smoothstep(0.035, 0.068, h));
                    float redProtect = isRedHue * smoothstep(0.32, 0.52, s);
                    float isGoldHue = smoothstep(0.095, 0.120, h) * (1.0 - smoothstep(0.155, 0.180, h));
                    float goldProtect = isGoldHue * smoothstep(0.55, 0.75, s) * smoothstep(0.58, 0.78, v);
                    float whiteProtect = smoothstep(0.86, 0.97, v) * (1.0 - smoothstep(0.04, 0.12, s));
                    float protectMask = saturate(max(redProtect, max(goldProtect * 0.85, whiteProtect)));

                    float blueBand = smoothstep(0.375, 0.435, h) * (1.0 - smoothstep(0.715, 0.785, h));
                    float ambientWeight = lerp(0.68, 1.0, blueBand) * (1.0 - protectMask);

                    if (ambientWeight > 0.002)
                    {
                        float brightFactor = smoothstep(0.12, 0.74, v);
                        float midTone = sin(saturate(v) * 3.14159);
                        float bandOffset = clamp((h - 0.545) * 0.18, -0.020, 0.020);
                        float3 targetHsv = hsv;

                        if (_ThemeMode < 1.5)
                        {
                            targetHsv.x = frac(lerp(0.415, 0.390, brightFactor) + bandOffset * 0.45 + 1.0);
                            float ambientSatFloor = lerp(0.26, 0.44, midTone) * smoothstep(0.02, 0.18, v);
                            float boostedSat = max(s * lerp(0.85, 1.05, brightFactor), ambientSatFloor);
                            targetHsv.y = saturate(lerp(boostedSat * 0.80, boostedSat, blueBand));
                            targetHsv.z = saturate(v * lerp(0.95, 1.04, brightFactor));
                        }
                        else
                        {
                            targetHsv.x = frac(lerp(0.088, 0.106, brightFactor) + bandOffset * 0.35 + 1.0);
                            float ambientSatFloor = lerp(0.22, 0.46, brightFactor) * smoothstep(0.03, 0.22, v);
                            float darkCarbonSat = s * lerp(0.45, 1.05, brightFactor);
                            float boostedSat = max(darkCarbonSat, ambientSatFloor);
                            targetHsv.y = saturate(lerp(boostedSat * 0.82, boostedSat, blueBand));
                            targetHsv.z = saturate(v * lerp(0.92, 1.06, brightFactor));
                        }

                        srgb = lerp(srgb, HsvToRgbB(targetHsv), ambientWeight);
                    }

                    color.rgb = linearSpace ? GammaToLinearSpace(max(srgb, 0.0)) : srgb;
                }
                return color;
            }
            ENDCG
        }
    }
}
