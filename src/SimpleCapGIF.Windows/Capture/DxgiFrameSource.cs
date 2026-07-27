using System.Runtime.InteropServices;
using SimpleCapGIF.Core.Geometry;
using SimpleCapGIF.Localization;
using SharpGen.Runtime;
using Vortice;
using Vortice.Direct3D;
using Vortice.Direct3D11;
using Vortice.DXGI;
using Vortice.Mathematics;
using static Vortice.Direct3D11.D3D11;
using static Vortice.DXGI.DXGI;

namespace SimpleCapGIF.Windows.Capture;

internal sealed class DxgiFrameSource : ICaptureFrameSource
{
    private const int DxgiErrorWaitTimeout = unchecked((int)0x887A0027);
    private const int DxgiErrorAccessLost = unchecked((int)0x887A0026);
    private readonly PixelRect _region;
    private readonly PixelSize _outputSize;
    private readonly ID3D11Device _device;
    private readonly ID3D11DeviceContext _context;
    private readonly IDXGIOutputDuplication _duplication;
    private readonly ID3D11VideoDevice _videoDevice;
    private readonly ID3D11VideoContext _videoContext;
    private readonly ID3D11Texture2D _scaledTexture;
    private readonly ID3D11Texture2D _stagingTexture;
    private readonly int _localX;
    private readonly int _localY;
    private readonly ModeRotation _rotation;
    private readonly byte[] _desktopPixels;
    private ID3D11VideoProcessorEnumerator? _videoEnumerator;
    private ID3D11VideoProcessor? _videoProcessor;
    private ID3D11VideoProcessorOutputView? _videoOutputView;
    private bool _disposed;

    internal DxgiFrameSource(PixelRect region, PixelSize outputSize)
    {
        _region = region;
        _outputSize = outputSize;
        (_device, _context, _duplication, var outputBounds, _rotation) = CreateDuplication(region);
        _localX = region.X - outputBounds.X;
        _localY = region.Y - outputBounds.Y;
        try
        {
            _videoDevice = _device.QueryInterface<ID3D11VideoDevice>();
            _videoContext = _context.QueryInterface<ID3D11VideoContext>();
        }
        catch
        {
            _duplication.Dispose();
            _context.Dispose();
            _device.Dispose();
            throw new InvalidOperationException(AppStrings.GpuScalingUnavailable);
        }

        var scaledDescription = new Texture2DDescription(
            Format.B8G8R8A8_UNorm,
            (uint)outputSize.Width,
            (uint)outputSize.Height,
            1,
            1,
            BindFlags.RenderTarget,
            ResourceUsage.Default,
            CpuAccessFlags.None,
            1,
            0,
            ResourceOptionFlags.None);
        _scaledTexture = _device.CreateTexture2D(in scaledDescription);
        var stagingDescription = scaledDescription;
        stagingDescription.BindFlags = BindFlags.None;
        stagingDescription.Usage = ResourceUsage.Staging;
        stagingDescription.CPUAccessFlags = CpuAccessFlags.Read;
        _stagingTexture = _device.CreateTexture2D(in stagingDescription);
        _desktopPixels = new byte[checked(outputSize.Width * outputSize.Height * 4)];
    }

    public bool TryAcquireLatest(uint timeoutMilliseconds)
    {
        ThrowIfDisposed();
        IDXGIResource? resource = null;
        var acquired = false;
        try
        {
            var result = _duplication.AcquireNextFrame(timeoutMilliseconds, out _, out resource);
            if (result.Code == DxgiErrorWaitTimeout) return false;
            if (result.Code == DxgiErrorAccessLost)
            {
                throw new InvalidOperationException(AppStrings.DisplayChanged);
            }

            result.CheckError();
            acquired = true;
            using var texture = resource.QueryInterface<ID3D11Texture2D>();
            EnsureVideoProcessor(texture.Description);
            using (var inputView = _videoDevice.CreateVideoProcessorInputView(
                texture,
                _videoEnumerator!,
                new VideoProcessorInputViewDescription
                {
                    ViewDimension = VideoProcessorInputViewDimension.Texture2D,
                    Texture2D = new Texture2DVideoProcessorInputView(),
                }))
            {
                var streams = new[]
                {
                    new VideoProcessorStream
                    {
                        Enable = true,
                        InputSurface = inputView,
                    },
                };
                _videoContext.VideoProcessorBlt(_videoProcessor!, _videoOutputView!, 0, streams).CheckError();
            }

            _context.CopyResource(_stagingTexture, _scaledTexture);
            var mapped = _context.Map(_stagingTexture, 0, MapMode.Read, Vortice.Direct3D11.MapFlags.None);
            try
            {
                var rowBytes = checked(_outputSize.Width * 4);
                for (var row = 0; row < _outputSize.Height; row++)
                {
                    Marshal.Copy(IntPtr.Add(mapped.DataPointer, checked((int)(row * mapped.RowPitch))), _desktopPixels, row * rowBytes, rowBytes);
                }
            }
            finally
            {
                _context.Unmap(_stagingTexture, 0);
            }

            return true;
        }
        finally
        {
            resource?.Dispose();
            if (acquired) _duplication.ReleaseFrame().CheckError();
        }
    }

    private void EnsureVideoProcessor(Texture2DDescription inputDescription)
    {
        if (_videoProcessor is not null) return;
        var contentDescription = new VideoProcessorContentDescription
        {
            InputFrameFormat = VideoFrameFormat.Progressive,
            InputFrameRate = new Rational(60, 1),
            InputWidth = inputDescription.Width,
            InputHeight = inputDescription.Height,
            OutputFrameRate = new Rational(60, 1),
            OutputWidth = (uint)_outputSize.Width,
            OutputHeight = (uint)_outputSize.Height,
            Usage = VideoUsage.OptimalQuality,
        };
        _videoEnumerator = _videoDevice.CreateVideoProcessorEnumerator(contentDescription);
        _videoProcessor = _videoDevice.CreateVideoProcessor(_videoEnumerator, 0);
        _videoOutputView = _videoDevice.CreateVideoProcessorOutputView(
            _scaledTexture,
            _videoEnumerator,
            new VideoProcessorOutputViewDescription
            {
                ViewDimension = VideoProcessorOutputViewDimension.Texture2D,
                Texture2D = new Texture2DVideoProcessorOutputView(),
            });
        _videoContext.VideoProcessorSetStreamFrameFormat(_videoProcessor, 0, VideoFrameFormat.Progressive);
        _videoContext.VideoProcessorSetStreamAutoProcessingMode(_videoProcessor, 0, false);
        _videoContext.VideoProcessorSetStreamSourceRect(
            _videoProcessor,
            0,
            true,
            MapSourceRect(
                new PixelRect(_localX, _localY, _region.Width, _region.Height),
                new PixelSize((int)inputDescription.Width, (int)inputDescription.Height),
                _rotation));
        _videoContext.VideoProcessorSetStreamRotation(
            _videoProcessor,
            0,
            _rotation is ModeRotation.Rotate90 or ModeRotation.Rotate180 or ModeRotation.Rotate270,
            ToVideoRotation(_rotation));
        _videoContext.VideoProcessorSetStreamDestRect(
            _videoProcessor,
            0,
            true,
            new RawRect(0, 0, _outputSize.Width, _outputSize.Height));
        _videoContext.VideoProcessorSetOutputTargetRect(
            _videoProcessor,
            true,
            new RawRect(0, 0, _outputSize.Width, _outputSize.Height));
    }

    internal static RawRect MapSourceRect(PixelRect localRegion, PixelSize surfaceSize, ModeRotation rotation) => rotation switch
    {
        ModeRotation.Rotate90 => new RawRect(
            surfaceSize.Width - localRegion.Bottom,
            localRegion.Left,
            surfaceSize.Width - localRegion.Top,
            localRegion.Right),
        ModeRotation.Rotate180 => new RawRect(
            surfaceSize.Width - localRegion.Right,
            surfaceSize.Height - localRegion.Bottom,
            surfaceSize.Width - localRegion.Left,
            surfaceSize.Height - localRegion.Top),
        ModeRotation.Rotate270 => new RawRect(
            localRegion.Top,
            surfaceSize.Height - localRegion.Right,
            localRegion.Bottom,
            surfaceSize.Height - localRegion.Left),
        _ => new RawRect(localRegion.Left, localRegion.Top, localRegion.Right, localRegion.Bottom),
    };

    private static VideoProcessorRotation ToVideoRotation(ModeRotation rotation) => rotation switch
    {
        ModeRotation.Rotate90 => VideoProcessorRotation.Rotation270,
        ModeRotation.Rotate180 => VideoProcessorRotation.Rotation180,
        ModeRotation.Rotate270 => VideoProcessorRotation.Rotation90,
        _ => VideoProcessorRotation.Identity,
    };

    public void CopyCurrentFrame(Span<byte> destination)
    {
        ThrowIfDisposed();
        _desktopPixels.CopyTo(destination);
    }

    private static (ID3D11Device Device, ID3D11DeviceContext Context, IDXGIOutputDuplication Duplication, PixelRect Bounds, ModeRotation Rotation) CreateDuplication(PixelRect region)
    {
        using var factory = CreateDXGIFactory1<IDXGIFactory1>();
        for (uint adapterIndex = 0; ; adapterIndex++)
        {
            var adapterResult = factory.EnumAdapters1(adapterIndex, out var adapter);
            if (adapterResult.Failure) break;
            using (adapter)
            {
                for (uint outputIndex = 0; ; outputIndex++)
                {
                    var outputResult = adapter.EnumOutputs(outputIndex, out var output);
                    if (outputResult.Failure) break;
                    using (output)
                    {
                        var rawBounds = output.Description.DesktopCoordinates;
                        var bounds = new PixelRect(rawBounds.Left, rawBounds.Top, rawBounds.Right - rawBounds.Left, rawBounds.Bottom - rawBounds.Top);
                        if (!bounds.Contains(region)) continue;

                        var createResult = D3D11CreateDevice(
                            adapter,
                            DriverType.Unknown,
                            DeviceCreationFlags.BgraSupport,
                            [FeatureLevel.Level_11_1, FeatureLevel.Level_11_0],
                            out var device,
                            out _,
                            out var context);
                        createResult.CheckError();
                        try
                        {
                            using var output1 = output.QueryInterface<IDXGIOutput1>();
                            var duplication = output1.DuplicateOutput(device);
                            return (device, context, duplication, bounds, output.Description.Rotation);
                        }
                        catch
                        {
                            context.Dispose();
                            device.Dispose();
                            throw;
                        }
                    }
                }
            }
        }

        throw new InvalidOperationException(AppStrings.NoDisplayOutput);
    }

    private void ThrowIfDisposed() => ObjectDisposedException.ThrowIf(_disposed, this);

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        _videoOutputView?.Dispose();
        _videoProcessor?.Dispose();
        _videoEnumerator?.Dispose();
        _stagingTexture.Dispose();
        _scaledTexture.Dispose();
        _videoContext.Dispose();
        _videoDevice.Dispose();
        _duplication.Dispose();
        _context.Dispose();
        _device.Dispose();
    }
}
