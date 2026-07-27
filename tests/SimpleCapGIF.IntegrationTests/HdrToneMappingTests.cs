using System.Numerics;
using System.Runtime.InteropServices;
using SimpleCapGIF.Windows.Capture;
using Vortice.Direct3D;
using Vortice.Direct3D11;
using Vortice.DXGI;
using static Vortice.Direct3D11.D3D11;

namespace SimpleCapGIF.IntegrationTests;

public sealed class HdrToneMappingTests
{
    [Fact]
    public void PrefersLinearHdrFramesWithSdrFallback()
    {
        Assert.Equal(
            [Format.R16G16B16A16_Float, Format.B8G8R8A8_UNorm],
            DxgiFrameSource.GetPreferredDuplicationFormats());
        Assert.True(DxgiFrameSource.RequiresHdrToneMapping(Format.R16G16B16A16_Float));
        Assert.False(DxgiFrameSource.RequiresHdrToneMapping(Format.B8G8R8A8_UNorm));
        Assert.True(DxgiFrameSource.IsHdrColorSpace(ColorSpaceType.RgbFullG2084NoneP2020));
        Assert.True(DxgiFrameSource.IsHdrColorSpace(ColorSpaceType.RgbStudioG2084NoneP2020));
        Assert.False(DxgiFrameSource.IsHdrColorSpace(ColorSpaceType.RgbFullG22NoneP709));
    }

    [Fact]
    public void PreservesSdrMidtonesAndCompressesHdrHighlights()
    {
        var midtone = HdrToneMapCurve.MapToSrgb(new Vector3(0.18f));
        var sdrWhite = HdrToneMapCurve.MapToSrgb(Vector3.One);
        var hdrHighlight = HdrToneMapCurve.MapToSrgb(new Vector3(8f));

        Assert.InRange(midtone.X, 0.460f, 0.463f);
        Assert.InRange(sdrWhite.X, 0.956f, 0.960f);
        Assert.InRange(hdrHighlight.X, 0.999f, 1f);
        Assert.True(midtone.X < sdrWhite.X);
        Assert.True(sdrWhite.X < hdrHighlight.X);
    }

    [Fact]
    public void ToneMappingPreservesNeutralColorAndFiniteOutput()
    {
        var mapped = HdrToneMapCurve.MapToSrgb(new Vector3(3f));

        Assert.True(float.IsFinite(mapped.X));
        Assert.Equal(mapped.X, mapped.Y, 6);
        Assert.Equal(mapped.Y, mapped.Z, 6);
        Assert.InRange(mapped.X, 0f, 1f);
    }

    [Fact]
    public void RuntimeToneMappingShadersCompile()
    {
        var (vertex, pixel) = HdrToneMapper.CompileShaders();

        Assert.NotEmpty(vertex);
        Assert.NotEmpty(pixel);
    }

    [Fact]
    public void ToneMappingShaderRunsOnWarpDevice()
    {
        using var device = D3D11CreateDevice(DriverType.Warp, DeviceCreationFlags.BgraSupport, [FeatureLevel.Level_11_0]);
        using var context = device.ImmediateContext;
        using var toneMapper = new HdrToneMapper(device, 2, 1);
        var input = new Half[]
        {
            (Half)0.18f, (Half)0.18f, (Half)0.18f, (Half)1f,
            (Half)8f, (Half)8f, (Half)8f, (Half)1f,
        };
        context.UpdateSubresource(input, toneMapper.LinearTexture, 0, 16, 16, null);

        toneMapper.Render(context);

        var stagingDescription = new Texture2DDescription(
            Format.B8G8R8A8_UNorm,
            2,
            1,
            1,
            1,
            BindFlags.None,
            ResourceUsage.Staging,
            CpuAccessFlags.Read,
            1,
            0,
            ResourceOptionFlags.None);
        using var staging = device.CreateTexture2D(in stagingDescription);
        context.CopyResource(staging, toneMapper.OutputTexture);
        var mapped = context.Map(staging, 0, MapMode.Read, Vortice.Direct3D11.MapFlags.None);
        try
        {
            var pixels = new byte[8];
            Marshal.Copy(mapped.DataPointer, pixels, 0, pixels.Length);
            Assert.InRange(pixels[0], 116, 119);
            Assert.Equal(pixels[0], pixels[1]);
            Assert.Equal(pixels[1], pixels[2]);
            Assert.InRange(pixels[4], 254, 255);
            Assert.Equal(pixels[4], pixels[5]);
            Assert.Equal(pixels[5], pixels[6]);
        }
        finally
        {
            context.Unmap(staging, 0);
        }
    }
}
