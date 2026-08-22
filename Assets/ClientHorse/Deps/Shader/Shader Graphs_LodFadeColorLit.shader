Shader "Shader Graphs/LodFadeColorLit" {
	Properties {
		[NoScaleOffset] Texture2D_918f5484808e4af3b2b08b7ddeeb0e5c ("BaseMap", 2D) = "white" {}
		Color_3e70faaec078434fa3a4c6c90065c3aa ("BaseColor", Vector) = (0,0,0,0)
		Vector1_06af79f2fd1e45669d8d60f6cbb2aec6 ("Metallic", Float) = 0
		Vector1_d9caf6fb3e7d48e791ac521eb79cf224 ("Occlusion", Float) = 0
		Vector1_de75dea2c42c447c82bd97144da01d62 ("Smoothness", Float) = 0
		[NoScaleOffset] Texture2D_aaf8447fbaf64bdc8e81fce432f4cad2 ("NormalMap", 2D) = "white" {}
		[NoScaleOffset] Texture2D_635a9d58fa444082aa59037c262f51c3 ("Color", 2D) = "white" {}
		Vector1_d50a5c6381354b6992a29adb0041aeac ("AlphaClipping", Float) = 0
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