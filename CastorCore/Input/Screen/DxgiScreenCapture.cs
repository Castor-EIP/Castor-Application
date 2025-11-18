using CastorCore.Frame;
using FFMpegCore.Pipes;
using SharpGen.Runtime;
using Vortice.Direct3D11;
using Vortice.DXGI;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace CastorCore.Input.Screen
{
    public class DxgiScreenCapture : IVideoInput, IDisposable
    {
        private IDXGIOutputDuplication? _duplication;
        private ID3D11Device? _device;
        private ID3D11DeviceContext? _context;

        public int Width { get; private set; }
        public int Height { get; private set; }
        public string Format => "bgra32";

        public DxgiScreenCapture(int globalIndex = 0)
        {
            List<MonitorInfo> monitors = MonitorManager.GetMonitorInfos();
            
            if (globalIndex < 0 || globalIndex >= monitors.Count)
            {
                throw new ArgumentOutOfRangeException(nameof(globalIndex),
                    $"Index out of range. Valid values: 0-{monitors.Count - 1}. Use GetAvailableMonitors() to see all monitors.");
            }

            MonitorInfo selectedMonitor = monitors[globalIndex];
            InitializeCapture(selectedMonitor.AdapterId, selectedMonitor.OutputId);

            Width = selectedMonitor.Width;
            Height = selectedMonitor.Height;

            Console.WriteLine($"[DxgiScreenCapture] Initialized monitor #{globalIndex}: {selectedMonitor.DeviceName} ({Width}x{Height})");
        }

        public DxgiScreenCapture(uint adapterId, uint outputId)
        {
            InitializeCapture(adapterId, outputId);
            Console.WriteLine($"[DxgiScreenCapture] Initialized adapter {adapterId}, output {outputId}: {Width}x{Height}");
        }

        private void InitializeCapture(uint adapterId, uint outputId)
        {
            using IDXGIFactory1 factory = DXGI.CreateDXGIFactory1<IDXGIFactory1>();

            Result factoryResult = factory.EnumAdapters1(adapterId, out IDXGIAdapter1? adapter);
            if (factoryResult.Failure || adapter == null)
            {
                throw new InvalidOperationException($"Failed to enumerate adapter {adapterId}.");
            }

            using (adapter)
            {
                Result adapterResult = adapter.EnumOutputs(outputId, out IDXGIOutput? output);
                if (adapterResult.Failure || output == null)
                {
                    throw new InvalidOperationException($"Failed to enumerate output {outputId} for adapter {adapterId}.");
                }

                using (output)
                {
                    // Get dimensions before creating device
                    OutputDescription desc = output.Description;
                    Width = desc.DesktopCoordinates.Right - desc.DesktopCoordinates.Left;
                    Height = desc.DesktopCoordinates.Bottom - desc.DesktopCoordinates.Top;

                    // Create D3D11 device
                    Result deviceResult = D3D11.D3D11CreateDevice(
                        adapter,
                        Vortice.Direct3D.DriverType.Unknown,
                        DeviceCreationFlags.BgraSupport,
                        null, 
                        out ID3D11Device? device);
                    
                    deviceResult.CheckError();

                    if (device == null)
                    {
                        throw new InvalidOperationException("Failed to create D3D11 device.");
                    }

                    _device = device;
                    _context = _device.ImmediateContext;

                    // Convert IDXGIOutput to IDXGIOutput1 for duplication
                    using IDXGIOutput1 output1 = output.QueryInterface<IDXGIOutput1>();
                    
                    if (output1 == null)
                    {
                        throw new InvalidOperationException("Failed to get IDXGIOutput1 interface. Desktop duplication not supported.");
                    }

                    // Create duplication
                    try
                    {
                        _duplication = output1.DuplicateOutput(_device);
                        
                        if (_duplication == null)
                        {
                            throw new InvalidOperationException("Failed to create output duplication. This may happen if another application is already using desktop duplication.");
                        }
                    }
                    catch (SharpGenException ex)
                    {
                        throw new InvalidOperationException($"Failed to duplicate output: {ex.Message}. Make sure no other screen capture application is running.", ex);
                    }
                }
            }
        }

        public Task<IVideoFrame?> CaptureFrameAsync(CancellationToken token)
        {
            try
            {
                if (_duplication == null)
                {
                    throw new InvalidOperationException("DXGI Output Duplication is not initialized.");
                }

                Result result = _duplication.AcquireNextFrame(16, out OutduplFrameInfo frameInfo, out IDXGIResource? resource);
                
                if (result.Failure)
                {
                    // Timeout or other error - return null
                    return Task.FromResult<IVideoFrame?>(null);
                }

                if (resource == null)
                {
                    return Task.FromResult<IVideoFrame?>(null);
                }

                using (resource)
                {
                    using ID3D11Texture2D tex = resource.QueryInterface<ID3D11Texture2D>();
                    
                    if (tex == null)
                    {
                        _duplication.ReleaseFrame();
                        return Task.FromResult<IVideoFrame?>(null);
                    }

                    if (_context == null)
                    {
                        _duplication.ReleaseFrame();
                        throw new InvalidOperationException("D3D11 device context is not initialized.");
                    }

                    DxgiVideoFrame frame = new DxgiVideoFrame(_context, Width, Height);
                    frame.CopyFrom(tex);
                    
                    _duplication.ReleaseFrame();
                    return Task.FromResult<IVideoFrame?>(frame);
                }
            }
            catch (SharpGenException ex) when (ex.ResultCode.Code == unchecked((int)0x887A0026)) // DXGI_ERROR_ACCESS_LOST
            {
                // Access lost (resolution change, etc.)
                Console.WriteLine("[DxgiScreenCapture] Access lost - frame dropped");
                return Task.FromResult<IVideoFrame?>(null);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[DxgiScreenCapture] Capture error: {ex.Message}");
                return Task.FromResult<IVideoFrame?>(null);
            }
        }

        public void Dispose()
        {
            _duplication?.Dispose();
            _context?.Dispose();
            _device?.Dispose();
            Console.WriteLine("[DxgiScreenCapture] Disposed");
        }
    }
}
