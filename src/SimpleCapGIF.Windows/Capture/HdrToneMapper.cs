using Vortice.Direct3D;
using Vortice.Direct3D11;
using Vortice.DXGI;

namespace SimpleCapGIF.Windows.Capture;

internal sealed class HdrToneMapper : IDisposable
{
    private const string ShaderSource = """
        Texture2D<float4> SourceTexture : register(t0);

        struct VertexOutput
        {
            float4 Position : SV_POSITION;
        };

        VertexOutput VSMain(uint vertexId : SV_VertexID)
        {
            float2 coordinates = float2((vertexId << 1) & 2, vertexId & 2);
            VertexOutput output;
            output.Position = float4(coordinates * float2(2.0, -2.0) + float2(-1.0, 1.0), 0.0, 1.0);
            return output;
        }

        float LinearToSrgb(float value)
        {
            return value <= 0.0031308
                ? value * 12.92
                : 1.055 * pow(value, 1.0 / 2.4) - 0.055;
        }

        float4 PSMain(VertexOutput input) : SV_TARGET
        {
            float3 color = SourceTexture.Load(int3(int2(input.Position.xy), 0)).rgb;
            float luminance = max(dot(color, float3(0.2126, 0.7152, 0.0722)), 0.0);
            const float knee = 0.75;
            if (luminance > knee)
            {
                const float remainingRange = 1.0 - knee;
                float mappedLuminance = knee + remainingRange * (1.0 - exp(-(luminance - knee) / remainingRange));
                color *= mappedLuminance / luminance;
            }

            color = saturate(color);
            return float4(
                LinearToSrgb(color.r),
                LinearToSrgb(color.g),
                LinearToSrgb(color.b),
                1.0);
        }
        """;

    private readonly ID3D11Texture2D _linearTexture;
    private readonly ID3D11ShaderResourceView _linearView;
    private readonly ID3D11Texture2D _outputTexture;
    private readonly ID3D11RenderTargetView _outputView;
    private readonly ID3D11VertexShader _vertexShader;
    private readonly ID3D11PixelShader _pixelShader;
    private readonly int _width;
    private readonly int _height;

    internal HdrToneMapper(ID3D11Device device, int width, int height)
    {
        _width = width;
        _height = height;
        var linearDescription = new Texture2DDescription(
            Format.R16G16B16A16_Float,
            (uint)width,
            (uint)height,
            1,
            1,
            BindFlags.RenderTarget | BindFlags.ShaderResource,
            ResourceUsage.Default,
            CpuAccessFlags.None,
            1,
            0,
            ResourceOptionFlags.None);
        _linearTexture = device.CreateTexture2D(in linearDescription);
        _linearView = device.CreateShaderResourceView(_linearTexture, null);

        var outputDescription = new Texture2DDescription(
            Format.B8G8R8A8_UNorm,
            (uint)width,
            (uint)height,
            1,
            1,
            BindFlags.RenderTarget,
            ResourceUsage.Default,
            CpuAccessFlags.None,
            1,
            0,
            ResourceOptionFlags.None);
        _outputTexture = device.CreateTexture2D(in outputDescription);
        _outputView = device.CreateRenderTargetView(_outputTexture, null);

        var (vertexBytecode, pixelBytecode) = CompileShaders();
        _vertexShader = device.CreateVertexShader(vertexBytecode, null);
        _pixelShader = device.CreatePixelShader(pixelBytecode, null);
    }

    internal ID3D11Texture2D LinearTexture => _linearTexture;
    internal ID3D11Texture2D OutputTexture => _outputTexture;

    internal static (byte[] Vertex, byte[] Pixel) CompileShaders() =>
        (D3DShaderCompiler.Compile(ShaderSource, "VSMain", "vs_5_0"), D3DShaderCompiler.Compile(ShaderSource, "PSMain", "ps_5_0"));

    internal void Render(ID3D11DeviceContext context)
    {
        context.OMSetRenderTargets(_outputView, null);
        context.RSSetViewport(0, 0, _width, _height, 0, 1);
        context.IASetPrimitiveTopology(PrimitiveTopology.TriangleList);
        context.VSSetShader(_vertexShader);
        context.PSSetShader(_pixelShader);
        context.PSSetShaderResource(0, _linearView);
        context.Draw(3, 0);
        context.PSSetShaderResource(0, null!);
        context.OMSetRenderTargets((ID3D11RenderTargetView)null!, null);
    }

    public void Dispose()
    {
        _pixelShader.Dispose();
        _vertexShader.Dispose();
        _outputView.Dispose();
        _outputTexture.Dispose();
        _linearView.Dispose();
        _linearTexture.Dispose();
    }
}
