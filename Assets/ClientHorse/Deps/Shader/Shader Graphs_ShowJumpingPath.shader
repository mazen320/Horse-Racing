Shader "Shader Graphs/ShowJumpingPath" {
	Properties {
		_Wetness ("Wetness", Float) = 0.6
		_Depth ("Depth", Float) = 1
		_Tint ("Tint", Vector) = (0.735849,0.6795951,0.5727127,0)
		_Smoothing ("Smoothing", Float) = 0.35
		_XDissolvePow ("XDissolvePow", Float) = 1.7
		_Noise_Scale ("Noise Scale", Float) = 75
		[NoScaleOffset] _Path_Texture ("Path Texture", 2D) = "white" {}
		[ToggleUI] _EnablePathHelper ("EnablePathHelper", Float) = 1
		[HDR] _ArrowColor ("ArrowColor", Vector) = (0.3726415,1,0.8895233,1)
		_HelperTiling ("HelperTiling", Vector) = (0,0,0,0)
		_Speed ("Speed", Float) = 0
		[ToggleUI] _ClipArrows ("ClipArrows", Float) = 0
		_ArrowsStart ("ArrowsStart", Float) = 0
		_ArrowsStop ("ArrowsStop", Float) = 0
		[NoScaleOffset] _SampleTexture2D_23e7a306bed04bc09c812f867f8fc291_Texture_1_Texture2D ("Texture2D", 2D) = "white" {}
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