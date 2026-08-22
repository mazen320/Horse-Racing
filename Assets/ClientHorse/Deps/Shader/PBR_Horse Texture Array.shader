Shader "PBR/Horse Texture Array" {
	Properties {
		_MainTex ("Albedo", 2D) = "white" {}
		_SpecMap ("SpecMap", 2D) = "white" {}
		_GlossMap ("GlossMap", 2D) = "white" {}
		_SpecGlossMask ("SpecGlossMask", 2D) = "white" {}
		_GradientMap ("GradientMap", 2DArray) = "white" {}
		_HeadMarking ("HeadMarking", 2D) = "red" {}
		_BodyPattern ("BodyPattern", 2D) = "red" {}
		_FrontLeftLegMarking ("FrontLeftLegMarking", 2D) = "red" {}
		_FrontRightLegMarking ("FrontRightLegMarking", 2D) = "red" {}
		_BackLeftLegMarking ("BackLeftLegMarking", 2D) = "red" {}
		_BackRightLegMarking ("BackRightLegMarking", 2D) = "red" {}
		_FaceMaskTex ("FaceMask", 2D) = "red" {}
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
			float4 _MainTex_ST;

			struct Vertex_Stage_Input
			{
				float4 pos : POSITION;
				float2 uv : TEXCOORD0;
			};

			struct Vertex_Stage_Output
			{
				float2 uv : TEXCOORD0;
				float4 pos : SV_POSITION;
			};

			Vertex_Stage_Output vert(Vertex_Stage_Input input)
			{
				Vertex_Stage_Output output;
				output.uv = (input.uv.xy * _MainTex_ST.xy) + _MainTex_ST.zw;
				output.pos = mul(unity_MatrixVP, mul(unity_ObjectToWorld, input.pos));
				return output;
			}

			Texture2D<float4> _MainTex;
			SamplerState sampler_MainTex;

			struct Fragment_Stage_Input
			{
				float2 uv : TEXCOORD0;
			};

			float4 frag(Fragment_Stage_Input input) : SV_TARGET
			{
				return _MainTex.Sample(sampler_MainTex, input.uv.xy);
			}

			ENDHLSL
		}
	}
}