using Vortice.Direct3D11;
using Vortice.DXGI;
using FFMpegCore.Pipes;
using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;

namespace CastorCore.Frame
{
    public class DxgiVideoFrame : IVideoFrame, IDisposable
    {
        private readonly ID3D11DeviceContext _context;
        private byte[] _buffer;
        private bool _disposed = false;

        public int Width { get; }
        public int Height { get; }
        public string Format { get; } = "bgra32";

        public DxgiVideoFrame(ID3D11DeviceContext ctx, int width, int height)
        {
            _context = ctx;
            Width = width;
            Height = height;

            // Allocate the buffer immediately
            int size = Width * Height * 4;
            _buffer = new byte[size];
        }

        public void CopyFrom(ID3D11Texture2D sourceTexture)
        {
            if (_disposed)
                throw new ObjectDisposedException(nameof(DxgiVideoFrame));

            // Create a staging texture to read the data
            Texture2DDescription desc = new Texture2DDescription
            {
                Width = (uint)Width,
                Height = (uint)Height,
                ArraySize = 1,
                MipLevels = 1,
                Format = Vortice.DXGI.Format.B8G8R8A8_UNorm,
                SampleDescription = new SampleDescription(1, 0),
                BindFlags = BindFlags.None,
                Usage = ResourceUsage.Staging,
                CPUAccessFlags = CpuAccessFlags.Read,
                MiscFlags = ResourceOptionFlags.None
            };

            using ID3D11Texture2D staging = _context.Device.CreateTexture2D(desc);

            // Copy source texture to staging texture
            _context.CopyResource(staging, sourceTexture);

            // Map the staging texture and copy data to our buffer
            _context.Map(staging, 0, MapMode.Read, Vortice.Direct3D11.MapFlags.None, out var data);
            
            try
            {
                unsafe
                {
                    fixed (byte* dest = _buffer)
                    {
                        // Copy line by line considering the row pitch
                        for (int y = 0; y < Height; y++)
                        {
                            Buffer.MemoryCopy(
                                (byte*)data.DataPointer + y * data.RowPitch,
                                dest + y * Width * 4,
                                Width * 4,
                                Width * 4);
                        }
                    }
                }
            }
            finally
            {
                _context.Unmap(staging, 0);
            }
        }

        public void Serialize(Stream pipe)
        {
            if (_disposed)
                throw new ObjectDisposedException(nameof(DxgiVideoFrame));
            
            if (_buffer == null || _buffer.Length == 0)
                throw new InvalidOperationException("Frame data not initialized. Call CopyFrom() first.");

            pipe.Write(_buffer, 0, _buffer.Length);
        }

        public async Task SerializeAsync(Stream pipe, CancellationToken token)
        {
            if (_disposed)
                throw new ObjectDisposedException(nameof(DxgiVideoFrame));
            
            if (_buffer == null || _buffer.Length == 0)
                throw new InvalidOperationException("Frame data not initialized. Call CopyFrom() first.");

            await pipe.WriteAsync(_buffer, 0, _buffer.Length, token);
        }

        public void Dispose()
        {
            if (_disposed)
                return;

            _disposed = true;
            _buffer = null!;
        }
    }
}
