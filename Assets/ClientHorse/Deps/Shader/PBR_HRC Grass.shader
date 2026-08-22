Shader "PBR/HRC Grass" {
	Properties {
		_CullMode ("CullMode", Float) = 0
		_EdgeLength ("Density Falloff", Range(0.5, 50)) = 8
		_MaxTessellation ("Max Density", Range(1, 6)) = 6
		_LODStart ("LOD Start", Float) = 20
		_LODEnd ("LOD End", Float) = 100
		_LODMax ("LOD Max", Range(1, 6)) = 6
		_GrassFadeStart ("Grass Fade Start", Float) = 50
		_GrassFadeEnd ("Grass Fade End", Float) = 100
		_Disorder ("Disorder", Float) = 0.3
		_GrassBottomColor ("Grass Bottom Color", Vector) = (0.35,0.35,0.35,1)
		_BurnColor ("Burn Color", Vector) = (1,1,1,1)
		_TextureCutoff ("Texture Cutoff", Range(0, 1)) = 0.1
		_WindParams ("Wind WaveStrength(X), WaveSpeed(Y), RippleStrength(Z), RippleSpeed(W)", Vector) = (0.3,1.2,0.15,1.3)
		_WindRotation ("Wind Rotation", Range(0, 6.2831855)) = 0
		_ColorMap ("Color Texture (RGB), Height(A)", 2D) = "white" {}
		_Normal ("Normal Texture (RG)", 2D) = "bump" {}
		_BurnMap ("Burn Map (R only - Represents bad going)", 2D) = "white" {}
		_Density ("Grass Density 1(R) 2(G) 3(B) 4(A)", 2D) = "red" {}
		_DensityValues ("Grass Density Values", Vector) = (1,1,1,1)
		_GrassTex00 ("Grass Texture", 2D) = "white" {}
		_Color00 ("Color", Vector) = (0.5,0.7,0.3,1)
		_SecColor00 ("Secondary Color", Vector) = (0.2,0.5,0.15,1)
		_WetDry00 ("Wet & Dry (XY - Dry (Metallic Smooth) ZW - Wet (Mettalic Smooth))", Vector) = (0,0,0,0)
		_Softness00 ("Softness", Range(0, 1)) = 0.5
		_Width00 ("Width", Float) = 0.1
		_MinHeight00 ("Min Height", Float) = 0.2
		_MaxHeight00 ("Max Height", Float) = 1.5
		_TextureAtlasWidth00 ("Texture Atlas Width", Float) = 1
		_TextureAtlasHeight00 ("Texture Atlas Height", Float) = 1
		_GrassTex01 ("Grass Texture", 2D) = "white" {}
		_Color01 ("Color", Vector) = (0.5,0.7,0.3,1)
		_SecColor01 ("Secondary Color", Vector) = (0.2,0.5,0.15,1)
		_WetDry01 ("Wet & Dry (XY - Dry (Metallic Smooth) ZW - Wet (Mettalic Smooth))", Vector) = (0,0,0,0)
		_Softness01 ("Softness", Range(0, 1)) = 0.5
		_Width01 ("Width", Float) = 0.1
		_MinHeight01 ("Min Height", Float) = 0.2
		_MaxHeight01 ("Max Height", Float) = 1.5
		_TextureAtlasWidth01 ("Texture Atlas Width", Float) = 1
		_TextureAtlasHeight01 ("Texture Atlas Height", Float) = 1
		_GrassTex02 ("Grass Texture", 2D) = "white" {}
		_Color02 ("Color", Vector) = (0.5,0.7,0.3,1)
		_SecColor02 ("Secondary Color", Vector) = (0.2,0.5,0.15,1)
		_WetDry02 ("Wet & Dry (XY - Dry (Metallic Smooth) ZW - Wet (Mettalic Smooth))", Vector) = (0,0,0,0)
		_Softness02 ("Softness", Range(0, 1)) = 0.5
		_Width02 ("Width", Float) = 0.1
		_MinHeight02 ("Min Height", Float) = 0.2
		_MaxHeight02 ("Max Height", Float) = 1.5
		_TextureAtlasWidth02 ("Texture Atlas Width", Float) = 1
		_TextureAtlasHeight02 ("Texture Atlas Height", Float) = 1
		_GrassTex03 ("Grass Texture", 2D) = "white" {}
		_Color03 ("Color", Vector) = (0.5,0.7,0.3,1)
		_SecColor03 ("Secondary Color", Vector) = (0.2,0.5,0.15,1)
		_WetDry03 ("Wet & Dry (XY - Dry (Metallic Smooth) ZW - Wet (Mettalic Smooth))", Vector) = (0,0,0,0)
		_Softness03 ("Softness", Range(0, 1)) = 0.5
		_Width03 ("Width", Float) = 0.1
		_MinHeight03 ("Min Height", Float) = 0.2
		_MaxHeight03 ("Max Height", Float) = 1.5
		_TextureAtlasWidth03 ("Texture Atlas Width", Float) = 1
		_TextureAtlasHeight03 ("Texture Atlas Height", Float) = 1
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
	//CustomEditor "HRCGrassEditor"
}