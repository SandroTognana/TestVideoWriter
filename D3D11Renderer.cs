using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using SharpDX;
using SharpDX.Direct3D11;
using SharpDX.DXGI;
using System.Windows;
using System.Windows.Interop;
using Microsoft.Wpf.Interop.DirectX;


namespace TestVideoWriter
{
    public class D3D11Renderer : IDisposable
    {
        // Direct3D11
        private SharpDX.Direct3D11.Device _d3d11Device;
        private Texture2D _renderTarget;
        private RenderTargetView _renderTargetView;
        private D3D11Image _d3d11Image; // Per l'interop WPF

        // Telecamera
        private MediaReader _camera; // Sostituisci con la tua libreria di cattura (es: AForge, OpenCV)

        public D3D11Renderer(int width, int height)
        {
            // 1. Inizializza Direct3D11
            _d3d11Device = new SharpDX.Direct3D11.Device(SharpDX.Direct3D.DriverType.Hardware, DeviceCreationFlags.BgraSupport);

            // 2. Crea una texture di rendering
            var textureDesc = new Texture2DDescription
            {
                Width = width,
                Height = height,
                MipLevels = 1,
                ArraySize = 1,
                Format = Format.B8G8R8A8_UNorm,
                SampleDescription = new SampleDescription(1, 0),
                Usage = ResourceUsage.Dynamic,
                BindFlags = BindFlags.RenderTarget | BindFlags.ShaderResource,
                CpuAccessFlags = CpuAccessFlags.Write,
                OptionFlags = ResourceOptionFlags.None
            };
            _renderTarget = new Texture2D(_d3d11Device, textureDesc);
            _renderTargetView = new RenderTargetView(_d3d11Device, _renderTarget);

            // 3. Inizializza l'interop WPF
            _d3d11Image = new D3D11Image();
            _d3d11Image.SetRenderTargetDX10(_renderTarget);

            // 4. Inizializza la telecamera
            _camera = new MediaReader(0, width, height, 30); // Adatta alla tua libreria
            _camera.FrameReady += OnCameraFrameReady;
            _camera.Start();
        }

        private void OnCameraFrameReady(object sender, MediaReader.FrameEventArgs e)
        {
            // 1. Copia il frame della telecamera nella texture
            var context = _d3d11Device.ImmediateContext;
            DataBox dataBox = context.MapSubresource(_renderTarget, 0, MapMode.WriteDiscard, SharpDX.Direct3D11.MapFlags.None);

            // Copia i dati del frame da byte[] a dataBox.DataPointer:
            // - dataBox.RowPitch è la dimensione di una riga in byte
            // - dataBox.SlicePitch è la dimensione di un'immagine in byte
            // - dataBox.DataPointer è il puntatore alla memoria
            // - e.Buffer è il frame della telecamera
            // - e.Width e e.Height sono le dimensioni del frame

            for (int i = 0; i < e.FrameHeight; i++)
            {
                // Calcola l'offset della riga
                int rowOffset = i * dataBox.RowPitch;

                // Copia la riga
                //System.Buffer.BlockCopy(e.Frame, i * e.Stride, dataBox.DataPointer + rowOffset, e.FrameWidth * 4);
            }
            

            context.UnmapSubresource(_renderTarget, 0);

            // 2. Notifica a WPF di aggiornare l'immagine
            _d3d11Image.Invalidate();
        }

        public D3D11Image GetImageSource() => _d3d11Image;

        public void Dispose()
        {
            _renderTargetView?.Dispose();
            _renderTarget?.Dispose();
            _d3d11Device?.Dispose();
            _camera?.Dispose();
        }
    }
}
