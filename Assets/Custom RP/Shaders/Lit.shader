Shader "Custom RP/Lit" {
    Properties {
        _BaseMap ("Texture", 2D) = "white" { }
        _BaseColor ("Color", color) = (0.5, 0.5, 0.5, 1.0)
        _Cutoff ("Alpha Cutoff", range(0.0, 1.0)) = 0.5
        [KeywordEnum(On, Clip, Dither, Off)] _Shadows ("Shadows", Float) = 0
        [Toggle(_PREMULTIPLY_ALPHA)] _PremulAlpha ("Premultiply Alpha", Float) = 0
        [Enum(UnityEngine.Rendering.BlendMode)] _SrcBlend ("Src Blend", Float) = 1
        [Enum(UnityEngine.Rendering.BlendMode)] _DstBlend ("Dst Blend", Float) = 0
        [Enum(Off, 0, On, 1)] _ZWrite ("Z Write", Float) = 1
        [Toggle(_MASK_MAP)] _MaskMapToggle ("Mask Map", Float) = 0
        [NoScaleOffset] _MaskMap("Mask (MODS)", 2D) = "white" {} //MODS memtallic occlusion,detail,smoothness
        _Metallic ("Metallic", range(0, 1)) = 0
        _Occlusion ("Occlusion", Range(0, 1)) = 1
        _Smoothness ("Smoothness", Range(0, 1)) = 0.5
        [Toggle(_RECEIVE_SHADOWS)] _ReceiveShadows ("Receive Shadows", Float) = 1
        [NoScaleOffset] _EmissionMap("Emission", 2D) = "white" {}
        [HDR] _EmissionColor("Emission", Color) = (0.0, 0.0, 0.0, 0.0)
        [HideInInspector] _MainTex("Texture for Lightmap", 2D) = "white" {}
        [HideInInspector] _Color("Color for Lightmap", Color) = (0.5, 0.5, 0.5, 1.0)
         _Fresnel ("Fresnel", Range(0, 1)) = 1 //控制菲涅尔反射强度
        
        [Toggle(_DETAIL_MAP)] _DetailMapToggle ("Detail Maps", Float) = 0
        _DetailMap("Details",2D) = "linearGrey"{} //细节部分贴图。细节贴图更加精细，多了其他部分。不加[NoScaleOffset]代表需要缩放偏移，r分量存储反照率系数，g分量存储平滑度系数
        _DetailAlbedo("Detail Albedo", Range(0, 1)) = 1
        _DetailSmoothness("Detail Smoothness", Range(0, 1)) = 1
        
        [Toggle(_NORMAL_MAP)] _NormalMapToggle ("Normal Map", Float) = 0//是否使用normal map
        [NoScaleOffset] _NormalMap("Normals", 2D) = "bump" {} //法线贴图
        _NormalScale("Normal Scale", Range(0, 1)) = 1 //控制法线强度
        [NoScaleOffset] _DetailNormalMap("Detail Normals", 2D) = "bump" {} //细节部分的法线贴图
        _DetailNormalScale("Detail Normal Scale", Range(0, 1)) = 1 //控制细节法线强度
    }
    SubShader {

        HLSLINCLUDE
        #include "../ShaderLibrary/Common.hlsl"
        #include "LitInput.hlsl"
        ENDHLSL

        Pass {
            Tags { "LightMode" = "CustomLit" }
            Blend [_SrcBlend] [_DstBlend], One OneMinusSrcAlpha 
            //颜色混合使用材质中的 _SrcBlend 和 _DstBlend 控制，而 Alpha 通道使用固定的 One 和 OneMinusSrcAlpha 混合因子，这使得颜色和 Alpha 的混合规则可以分开。
            ZWrite [_ZWrite]
            HLSLPROGRAM
            //声明关键字
            #pragma target 3.5
            #pragma multi_compile _ _OTHER_PCF3 _OTHER_PCF5 _OTHER_PCF7
            #pragma multi_compile _ _LIGHTS_PER_OBJECT
            #pragma shader_feature _DETAIL_MAP
            #pragma shader_feature _MASK_MAP
            #pragma shader_feature _NORMAL_MAP
            #pragma multi_compile _ LOD_FADE_CROSSFADE
            #pragma multi_compile _ _SHADOW_MASK_DISTANCE  _SHADOW_MASK_ALWAYS
            #pragma multi_compile _ LIGHTMAP_ON
            #pragma multi_compile _ _CASCADE_BLEND_SOFT _CASCADE_BLEND_DITHER
            #pragma multi_compile _ _DIRECTIONAL_PCF3 _DIRECTIONAL_PCF5 _DIRECTIONAL_PCF7
            #pragma shader_feature _CLIPPING
            #pragma shader_feature _PREMULTIPLY_ALPHA
            #pragma multi_compile_instancing
            #pragma vertex LitPassVertex
            #pragma fragment LitPassFragment
            #pragma shader_feature _RECEIVE_SHADOWS
            #include "LitPass.hlsl"
            ENDHLSL
        }

        Pass {
            //只需要写入深度数据，所以添加ColorMask 0不写入任何颜色数据，但会进行深度测试，并将深度值写到深度缓冲区中
            Tags { "LightMode" = "ShadowCaster" }
            ColorMask 0
            HLSLPROGRAM
            #pragma target 3.5
            #pragma multi_compile _ LOD_FADE_CROSSFADE
            #pragma shader_feature _ _SHADOWS_CLIP _SHADOWS_DITHER
            #pragma multi_compile_instancing
            #pragma vertex ShadowCasterPassVertex
            #pragma fragment ShadowCasterPassFragment
            #include "ShadowCasterPass.hlsl"
            ENDHLSL
        }
        Pass{
            Tags{"LightMode" = "Meta"}
            Cull Off

            HLSLPROGRAM
            #pragma target 3.5
            #pragma vertex MetaPassVertex
            #pragma fragment MetaPassFragment
            #include "MetaPass.hlsl"
            ENDHLSL
        }
    }
    CustomEditor "CustomShaderGUI"
}