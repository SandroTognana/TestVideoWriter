// fxc /Zi /T ps_5_0 /E PS /Fo "test_shader.cso" "test_shader.hlsl"
// Parametri passati dalla CPU
cbuffer ResolutionParams : register(b0)
{
    float2 inputResolution; // Risoluzione originale (es. 1920x1080)
    float2 outputResolution; // Risoluzione desiderata (es. 1280x720)
};

Texture2D<float>  YPlane : register(t0);
Texture2D<float2> UVPlane : register(t1);
SamplerState samLinear : register(s0);

float3 YUVtoRGB(float y, float2 uv)
{
    // Formule di conversione YUV → RGB (BT.601)
    y = 1.164 * (y - 0.0625);
    float u = uv.x - 0.5;
    float v = uv.y - 0.5;
    
    float3 rgb;
    rgb.r = y + 1.596 * v;
    rgb.g = y - 0.391 * u - 0.813 * v;
    rgb.b = y + 2.018 * u;
    
    return rgb;
}

float4 PS(float4 pos : SV_Position) : SV_Target
{
    // Calcolo dinamico delle coordinate
    float2 uv = pos.xy / outputResolution;
    float2 srcUV = uv * (outputResolution / inputResolution);
    
    float y = YPlane.Sample(samLinear, srcUV);
    float2 uvChroma = UVPlane.Sample(samLinear, srcUV * 0.5);
    
    return float4(YUVtoRGB(y, uvChroma), 1.0);
}