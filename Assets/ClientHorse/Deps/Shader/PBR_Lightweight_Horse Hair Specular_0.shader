Shader "PBR/Lightweight/Horse Hair Specular" {
	Properties {
		_BaseMap ("Albedo", 2D) = "white" {}
		_Cutoff ("Alpha Cutoff", Range(0, 1)) = 0.5
		_Smoothness ("Smoothness", Range(0, 1)) = 0.5
		_SmoothnessTextureChannel ("Smoothness texture channel", Float) = 0
		_SpecColor ("Specular", Vector) = (0.2,0.2,0.2,1)
		_SpecGlossMap ("Specular", 2D) = "white" {}
		[ToggleOff] _SpecularHighlights ("Specular Highlights", Float) = 1
		[ToggleOff] _EnvironmentReflections ("Environment Reflections", Float) = 1
		_BumpScale ("Scale", Float) = 1
		_BumpMap ("Normal Map", 2D) = "bump" {}
		_OcclusionStrength ("Strength", Range(0, 1)) = 1
		_OcclusionMap ("Occlusion", 2D) = "white" {}
		_EmissionColor ("Color", Vector) = (0,0,0,1)
		_EmissionMap ("Emission", 2D) = "white" {}
		[Enum(Hair,0,Tail,1,Mane,2)] _HairGradientIndexOffset ("Horse Hair Gradient", Float) = 0
		[HideInInspector] _PatternTex ("PatternTex", 2D) = "black" {}
		_Anisotropy ("Anisotropy", Range(0, 1)) = 0
		_AnisotropyMap ("Anisotropy", 2D) = "bump" {}
		_DistanceSpecFadeEnd ("Distance Fade End", Float) = 100
		_DistanceSpecFadeRange ("Distance Fade Range", Float) = 5
		_ArtisticSpecular ("Artistic Specular", Range(1, 16)) = 1
		_SssViewAngleSensitivity ("SSS View Angle Sensitivity", Range(1, 5)) = 1
		_SssPow ("SSS Pow", Range(0, 16)) = 1
		_SssScale ("SSS Scale", Range(0, 1)) = 1
		_SssThickness ("SSS Thickness", 2D) = "white" {}
		[HideInInspector] _Surface ("__surface", Float) = 0
		[HideInInspector] _Blend ("__blend", Float) = 0
		[HideInInspector] _AlphaClip ("__clip", Float) = 0
		[HideInInspector] _SrcBlend ("__src", Float) = 1
		[HideInInspector] _DstBlend ("__dst", Float) = 0
		[HideInInspector] _ZWrite ("__zw", Float) = 1
		[HideInInspector] _Cull ("__cull", Float) = 2
		_ReceiveShadows ("Receive Shadows", Float) = 1
		_CastShadow ("Cast Shadow", Float) = 1
		_RimLightPulse ("Rim Light Pulse", Float) = 0
		_RimLightPulseColour ("Rim Colour", Vector) = (1,1,1,1)
		_RimLightPulseScale ("Rim Scale", Float) = 1
		_RimLightPulsePower ("Rim Power", Float) = 3
		_RimLightPulseFreq ("Pulse Frequency", Float) = 2
	}
	//DummyShaderTextExporter
	SubShader{
		Tags { "RenderType" = "Opaque" }
		LOD 200

		Pass
		{
			HLSLPROGRAM
			#pragma vertex vert
			#pragma fragment frag

			float4x4 unity_ObjectToWorld;
			float4x4 unity_MatrixVP;

			struct Vertex_Stage_Input
			{
				float4 pos : POSITION;
			};

			struct Vertex_Stage_Output
			{
				float4 pos : SV_POSITION;
			};

			Vertex_Stage_Output vert(Vertex_Stage_Input input)
			{
				Vertex_Stage_Output output;
				output.pos = mul(unity_MatrixVP, mul(unity_ObjectToWorld, input.pos));
				return output;
			}

			float4 frag(Vertex_Stage_Output input) : SV_TARGET
			{
				return float4(1.0, 1.0, 1.0, 1.0); // RGBA
			}

			ENDHLSL
		}
	}
	Fallback "Hidden/InternalErrorShader"
	//CustomEditor "PBRHorseShaderGUI"
}