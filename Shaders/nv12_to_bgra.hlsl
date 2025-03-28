Texture2D<float>  YPlane : register(t0);      // Texture Luma (Y)
Texture2D<float2> UVPlane : register(t1);     // Texture Chroma (UV)

SamplerState samLinear : register(s0);

float3 YUVtoRGB(float y, float2 uv)
{
    // Formule di conversione YUV → RGB (standard BT.601)
    float u = uv.x - 0.5;
    float v = uv.y - 0.5;
    
    float r = y + 1.402 * v;
    float g = y - 0.344 * u - 0.714 * v;
    float b = y + 1.772 * u;
    
    return float3(r, g, b);
}

float4 PS(float4 pos : SV_Position) : SV_Target
{
    // Coordinate UV normalizzate
    float2 uv = pos.xy / float2(1920, 1080); // Adatta alla risoluzione
    
    // Campiona Y e UV (UV è a risoluzione dimezzata)
    float  y = YPlane.SampleLevel(samLinear, uv, 0);
    float2 uvChroma = UVPlane.SampleLevel(samLinear, uv * 0.5, 0);
    
    // Converti in RGB
    float3 rgb = YUVtoRGB(y, uvChroma);
    
    // Ritorna BGRA (con alpha=1)
    return float4(rgb.r, rgb.g, rgb.b, 1.0);
}
