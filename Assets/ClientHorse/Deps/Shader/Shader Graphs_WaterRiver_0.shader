Shader "Shader Graphs/WaterRiver" {
	Properties {
		_DepthFadeDistance ("DepthFadeDistance", Float) = 0.45
		_DeepColor ("DeepColor", Vector) = (0,0,0,0)
		_ShallowColor ("ShallowColor", Vector) = (0,0,0,0)
		_RefractionScale ("RefractionScale", Float) = 0.51
		_RefractionSpeed ("RefractionSpeed", Float) = 0.18
		[NoScaleOffset] _NormalMap ("NormalMap", 2D) = "white" {}
		_RefractionStrength ("RefractionStrength", Float) = 0.5
		_FoamScale ("FoamScale", Float) = 0.36
		_FoamSpeed ("FoamSpeed", Float) = -0.02
		[NoScaleOffset] _FoamTexture ("FoamTexture", 2D) = "white" {}
		_FoamAmount ("FoamAmount", Float) = 1
		_FoamColor ("FoamColor", Vector) = (1,1,1,0.4470588)
		_FoamCutoff ("FoamCutoff", Float) = 1.75
		_Metallic ("Metallic", Float) = 0
		_Smoothness ("Smoothness", Range(0, 1)) = 0
		[HideInInspector] _QueueOffset ("_QueueOffset", Float) = 0
		[HideInInspector] _QueueControl ("_QueueControl", Float) = -1
		[HideInInspector] [NoScaleOffset] unity_Lightmaps ("unity_Lightmaps", 2DArray) = "" {}
		[HideInInspector] [NoScaleOffset] unity_LightmapsInd ("unity_LightmapsInd", 2DArray) = "" {}
		[HideInInspector] [NoScaleOffset] unity_ShadowMasks ("unity_ShadowMasks", 2DArray) = "" {}
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
	Fallback "Hidden/Shader Graph/FallbackError"
	//CustomEditor "UnityEditor.ShaderGraph.GenericShaderGraphMaterialGUI"
}