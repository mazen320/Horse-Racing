Shader "Shader Graphs/Desert River" {
	Properties {
		_Foam_depth ("Foam depth", Range(0.1, 10)) = 0.1
		[NoScaleOffset] _Foam_texture ("Foam texture", 2D) = "white" {}
		[NoScaleOffset] _Waterfall_texture ("Waterfall texture", 2D) = "white" {}
		_Color ("Color", Vector) = (0,0,0,0)
		[NoScaleOffset] _NormalFlowing ("NormalFlowing", 2D) = "white" {}
		[NoScaleOffset] _NormalStatic ("NormalStatic", 2D) = "white" {}
		_normal_intensity ("normal intensity", Range(0, 1)) = 1
		_WaterTransparency ("WaterTransparency", Range(0, 1)) = 0.9
		_edges_color ("edges color", Vector) = (0.4235294,0.4313726,0.4470588,0)
		_Smoothness ("Smoothness", Range(0, 1)) = 0
		[HideInInspector] _QueueOffset ("_QueueOffset", Float) = 0
		[HideInInspector] _QueueControl ("_QueueControl", Float) = -1
		[HideInInspector] [NoScaleOffset] unity_Lightmaps ("unity_Lightmaps", 2DArray) = "" {}
		[HideInInspector] [NoScaleOffset] unity_LightmapsInd ("unity_LightmapsInd", 2DArray) = "" {}
		[HideInInspector] [NoScaleOffset] unity_ShadowMasks ("unity_ShadowMasks", 2DArray) = "" {}
	}
	//DummyShaderTextExporter
	SubShader{
		Tags { "RenderType"="Opaque" }
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

			float4 _Color;

			float4 frag(Vertex_Stage_Output input) : SV_TARGET
			{
				return _Color; // RGBA
			}

			ENDHLSL
		}
	}
	Fallback "Hidden/Shader Graph/FallbackError"
	//CustomEditor "UnityEditor.ShaderGraph.GenericShaderGraphMaterialGUI"
}