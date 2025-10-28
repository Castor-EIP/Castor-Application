using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

using CastorCore.Source.Frame;

using SharpDX;
using SharpDX.Direct3D11;
using SharpDX.DXGI;

using Device = SharpDX.Direct3D11.Device;
using MapFlags = SharpDX.Direct3D11.MapFlags;

namespace CastorCore.Input
{
    public class ScreenCaptureInput : IVideoInput
    {
        private Factory1? _factory;
        private Adapter1? _adapter;
        private Device? _device;
        private Output? _output;
        private Output1? _output1;
        private OutputDuplication? _outputDuplication;
        private Texture2D? _stagingTexture;

        private int _width;
        private int _height;

        private int _outputIndex;
        private bool _isInitialized;
        
        public ScreenCaptureInput(int screenIndex = 0)
        {
            _outputIndex = screenIndex;
        }

        public async Task<bool> InitializeAsync(CancellationToken cancellationToken = default)
        {
            try
            {
                // Create DXGI Factory
                _factory = new Factory1();

                // Get adapter (GPU)
                _adapter = _factory.GetAdapter1(0);

                // Create D3D11 Device
                _device = new Device(_adapter, DeviceCreationFlags.None);

                // Get output (monitor)
                _output = _adapter.GetOutput(_outputIndex);
                _output1 = _output.QueryInterface<Output1>();

                // Get output description
                OutputDescription outputDesc = _output.Description;
                _width = outputDesc.DesktopBounds.Right - outputDesc.DesktopBounds.Left;
                _height = outputDesc.DesktopBounds.Bottom - outputDesc.DesktopBounds.Top;

                // Create desktop duplication
                _outputDuplication = _output1.DuplicateOutput(_device);

                // Create staging texture for CPU access
                Texture2DDescription textureDesc = new Texture2DDescription
                {
                    CpuAccessFlags = CpuAccessFlags.Read,
                    BindFlags = BindFlags.None,
                    Format = Format.B8G8R8A8_UNorm,
                    Width = _width,
                    Height = _height,
                    OptionFlags = ResourceOptionFlags.None,
                    MipLevels = 1,
                    ArraySize = 1,
                    SampleDescription = { Count = 1, Quality = 0 },
                    Usage = ResourceUsage.Staging
                };
                _stagingTexture = new Texture2D(_device, textureDesc);

                _isInitialized = true;
                return await Task.FromResult(true);
            }
            catch (SharpDXException ex)
            {
                Console.WriteLine($"Desktop Duplication initialization failed: {ex.Message}");
                Dispose();
                return await Task.FromResult(false);
            }
        }

        public async Task<VideoFrame?> ReadFrameAsync(CancellationToken cancellationToken = default)
        {
            //TODO: Capture frame from Desktop Duplication API
            if (!_isInitialized || _outputDuplication == null || _device == null || _stagingTexture == null)
                return null;

            SharpDX.DXGI.Resource? desktopResource = null;
            Texture2D? desktopTexture = null;

            try
            {
                // Try to acquire next frame (with 1ms timeout)
                Result result = _outputDuplication.TryAcquireNextFrame(
                    1,  // 1ms timeout
                    out OutputDuplicateFrameInformation frameInfo,
                    out desktopResource
                );

                // Handle timeout - no new frame available
                if (result.Code == SharpDX.DXGI.ResultCode.WaitTimeout.Result.Code)
                {
                    await Task.Delay(1, cancellationToken);
                    return null;
                }

                // Handle access lost - need to reinitialize
                if (result.Failure && result.Code == SharpDX.DXGI.ResultCode.AccessLost.Result.Code)
                {
                    Console.WriteLine("Desktop Duplication access lost, reinitializing...");
                    Dispose();
                    await InitializeAsync(cancellationToken);
                    return null;
                }

                result.CheckError();

                // No frame update (AccumulatedFrames == 0)
                if (frameInfo.AccumulatedFrames == 0 || desktopResource == null)
                {
                    ReleaseFrame();
                    return null;
                }

                // Get the desktop texture
                desktopTexture = desktopResource.QueryInterface<Texture2D>();

                // Copy to staging texture for CPU access
                _device.ImmediateContext.CopyResource(desktopTexture, _stagingTexture);

                // Map the staging texture to read data
                DataBox dataBox = _device.ImmediateContext.MapSubresource(
                    _stagingTexture,
                    0,
                    MapMode.Read,
                    MapFlags.None
                );

                try
                {
                    // Allocate buffer for frame data
                    byte[] frameData = new byte[_width * _height * 4]; // BGRA32 = 4 bytes per pixel

                    // Copy data row by row (handle stride)
                    nint sourcePtr = dataBox.DataPointer;
                    int destOffset = 0;

                    for (int y = 0; y < _height; y++)
                    {
                        // Copy one row
                        System.Runtime.InteropServices.Marshal.Copy(
                            sourcePtr,
                            frameData,
                            destOffset,
                            _width * 4
                        );

                        // Move to next row
                        sourcePtr = IntPtr.Add(sourcePtr, dataBox.RowPitch);
                        destOffset += _width * 4;
                    }

                    // Create video frame
                    VideoFrame videoFrame = new VideoFrame
                    {
                        Width = _width,
                        Height = _height,
                        Data = frameData,
                        Format = VideoPixelFormat.BGRA32
                    };

                    return await Task.FromResult(videoFrame);
                }
                finally
                {
                    // Unmap the staging texture
                    _device.ImmediateContext.UnmapSubresource(_stagingTexture, 0);

                    // Release the frame
                    ReleaseFrame();
                }

            }
            catch (SharpDXException ex) when (ex.ResultCode.Code == SharpDX.DXGI.ResultCode.WaitTimeout.Result.Code)
            {
                // Timeout is normal, just return null
                return null;
            }
            catch (SharpDXException ex) when (ex.ResultCode.Code == SharpDX.DXGI.ResultCode.AccessLost.Result.Code)
            {
                // Desktop duplication interface became invalid (mode change, etc.)
                Console.WriteLine("Access lost during frame capture, reinitializing...");
                Dispose();
                await InitializeAsync(cancellationToken);
                return null;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error capturing frame: {ex.Message}");
                ReleaseFrame();
                return null;
            }
            finally
            {
                desktopTexture?.Dispose();
                desktopResource?.Dispose();
            }
        }

        private void ReleaseFrame()
        {
            try
            {
                _outputDuplication?.ReleaseFrame();
            }
            catch (SharpDXException ex)
            {
                // Ignore errors when releasing frame
                Console.WriteLine($"Error releasing frame: {ex.Message}");
            }
        }

        public void Dispose()
        {
            _isInitialized = false;

            _stagingTexture?.Dispose();
            _stagingTexture = null;

            _outputDuplication?.Dispose();
            _outputDuplication = null;

            _output1?.Dispose();
            _output1 = null;

            _output?.Dispose();
            _output = null;

            _device?.Dispose();
            _device = null;

            _adapter?.Dispose();
            _adapter = null;

            _factory?.Dispose();
            _factory = null;
        }
    }
}
