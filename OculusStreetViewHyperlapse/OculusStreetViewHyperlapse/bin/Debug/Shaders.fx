Texture2D ShaderTexture : register(t0);
SamplerState Sampler : register(s0);

float4x4 WorldViewProjection;

struct VertexShaderInput
{
	float4 Position : SV_Position;
	float4 Color : COLOR;
	float2 TextureUV : TEXCOORD0;
};

struct VertexShaderOutput
{
	float4 Position : SV_Position;
	float4 Color : COLOR;
	float2 TextureUV : TEXCOORD0;
};

VertexShaderOutput VertexShaderMain(VertexShaderInput input)
{
	VertexShaderOutput output = (VertexShaderOutput)0;

	output.Position = mul(input.Position, WorldViewProjection);
	output.TextureUV = input.TextureUV;

	return output;
}

float4 PixelShaderMain(VertexShaderOutput input) : SV_Target
{
	return ShaderTexture.Sample(Sampler, input.TextureUV);
}