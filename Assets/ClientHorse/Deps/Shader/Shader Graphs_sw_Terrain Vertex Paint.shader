Shader "Shader Graphs/sw_Terrain Vertex Paint" {
	Properties {
		Vector1_4E874754 ("Black Tile", Float) = 1
		[NoScaleOffset] Texture2D_E32F36A1 ("Black Diffuse", 2D) = "white" {}
		Vector1_E818814 ("Red Tile", Float) = 1
		[NoScaleOffset] Texture2D_310613A4 ("Red Diffuse", 2D) = "white" {}
		Vector1_7A0AFF9E ("Green Tile", Float) = 1
		[NoScaleOffset] Texture2D_24AD832C ("Green Diffuse", 2D) = "white" {}
		Vector1_2B40E94A ("Blue Tile", Float) = 1
		[NoScaleOffset] Texture2D_AF9D8CDC ("Blue Diffuse", 2D) = "white" {}
		[NoScaleOffset] Texture2D_A1548BAC ("Splat Map", 2D) = "white" {}
		Color_87E6B5F9 ("Tint", Vector) = (1,1,1,0)
		Vector1_44857F64 ("Brightness", Float) = 3.2
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