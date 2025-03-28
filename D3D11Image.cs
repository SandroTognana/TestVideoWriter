using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace TestVideoWriter
{
    using System.Windows;
    using System.Windows.Interop;
    using System.Windows.Media;
    using SharpDX.Direct3D9;

    public class D3D11Image : D3DImage, IDisposable
    {
        private static Direct3DEx _d3DContext;
        private static DeviceEx _d3DDevice;
        private Texture _backBuffer;

        static D3D11Image()
        {
            _d3DContext = new Direct3DEx();
            var presentParams = new PresentParameters
            {
                Windowed = true,
                SwapEffect = SwapEffect.Discard,
                DeviceWindowHandle = NativeMethods.GetDesktopWindow(),
                PresentationInterval = PresentInterval.Default
            };
            _d3DDevice = new DeviceEx(_d3DContext, 0, DeviceType.Hardware, IntPtr.Zero,
                CreateFlags.HardwareVertexProcessing | CreateFlags.Multithreaded, presentParams);
        }

        public void SetRenderTargetDX10(SharpDX.Direct3D11.Texture2D renderTarget)
        {
            using (var resource = renderTarget.QueryInterface<SharpDX.DXGI.Resource>())
            {
                var handle = resource.SharedHandle;
                _backBuffer = new Texture(_d3DDevice, renderTarget.Description.Width,
                    renderTarget.Description.Height, 1, Usage.RenderTarget,
                    Format.A8R8G8B8, Pool.Default, ref handle);
            }

            var surface = _backBuffer.GetSurfaceLevel(0);
            Lock();
            SetBackBuffer(D3DResourceType.IDirect3DSurface9, surface.NativePointer);
            Unlock();
        }

        public void Invalidate()
        {
            if (IsFrontBufferAvailable)
            {
                Lock();
                AddDirtyRect(new Int32Rect(0, 0, PixelWidth, PixelHeight));
                Unlock();
            }
        }

        public void Dispose()
        {
            _backBuffer?.Dispose();
        }
    }

    internal static class NativeMethods
    {
        [System.Runtime.InteropServices.DllImport("user32.dll")]
        public static extern IntPtr GetDesktopWindow();
    }
    //using System;
    //using System.Windows;
    //using System.Windows.Interop;
    //using SharpDX;
    //using SharpDX.Direct3D9;
    //using SharpDX.Direct3D11;
    ////using SharpDX.DXGI;

    //public class D3D11Image : D3DImage, IDisposable
    //{
    //    // Direct3D9 per l'interop WPF
    //    private static Direct3DEx _d3DContext;
    //    private static DeviceEx _d3DDevice;
    //    private Texture _d3D9Texture;

    //    // Direct3D11
    //    private Texture2D _d3D11Texture;

    //    static D3D11Image()
    //    {
    //        _d3DContext = new Direct3DEx();
    //        var presentParams = new PresentParameters
    //        {
    //            Windowed = true,
    //            SwapEffect = SwapEffect.Discard,
    //            DeviceWindowHandle = GetDesktopWindow(),
    //            PresentationInterval = PresentInterval.Default,
    //            BackBufferFormat = Format.A8R8G8B8
    //        };
    //        _d3DDevice = new DeviceEx(_d3DContext, 0, DeviceType.Hardware, IntPtr.Zero,
    //            CreateFlags.HardwareVertexProcessing | CreateFlags.Multithreaded, presentParams);
    //    }

    //    public void SetRenderTarget(Texture2D d3D11Texture)
    //    {
    //        if (d3D11Texture == null) throw new ArgumentNullException(nameof(d3D11Texture));

    //        DisposeD3D9Texture();

    //        _d3D11Texture = d3D11Texture;
    //        var desc = d3D11Texture.Description;

    //        // Crea una texture D3D9 condivisa con D3D11
    //        using (var resource = d3D11Texture.QueryInterface<SharpDX.DXGI.Resource>())
    //        {
    //            IntPtr sharedHandle = resource.SharedHandle;
    //            _d3D9Texture = new Texture(_d3DDevice, desc.Width, desc.Height, 1,
    //                Usage.RenderTarget, Format.A8R8G8B8, Pool.Default, ref sharedHandle);
    //        }

    //        // Imposta il backbuffer per WPF
    //        using (var surface = _d3D9Texture.GetSurfaceLevel(0))
    //        {
    //            Lock();
    //            SetBackBuffer(D3DResourceType.IDirect3DSurface9, surface.NativePointer);
    //            Unlock();
    //        }
    //    }

    //    public void Invalidate()
    //    {
    //        if (IsFrontBufferAvailable)
    //        {
    //            Lock();
    //            AddDirtyRect(new Int32Rect(0, 0, PixelWidth, PixelHeight));
    //            Unlock();
    //        }
    //    }

    //    private void DisposeD3D9Texture()
    //    {
    //        if (_d3D9Texture != null)
    //        {
    //            _d3D9Texture.Dispose();
    //            _d3D9Texture = null;
    //        }
    //    }

    //    public void Dispose()
    //    {
    //        DisposeD3D9Texture();
    //        _d3D11Texture = null;
    //    }

    //    [System.Runtime.InteropServices.DllImport("user32.dll")]
    //    private static extern IntPtr GetDesktopWindow();
    //}
}
