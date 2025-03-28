#define TEST_ONNX

using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using System.Windows.Shapes;
using System.Xml.Serialization;
using System;
using System.Drawing;
using System.Drawing.Imaging;
using System.Runtime.InteropServices;
using MediaFoundation;
using MediaFoundation.Misc;
using MediaFoundation.ReadWrite;
using MediaFoundation.Net;
using System.Runtime.Remoting.Messaging;
using System.Resources;
using MediaFoundation.Transform;
using TurboJpegWrapper;
using System.Diagnostics;
using MFModels;
using Emgu.CV.Reg;
using System.Windows.Interop;
using System.Drawing.Drawing2D;
using Microsoft.ML.OnnxRuntime;
using Microsoft.ML.OnnxRuntime.Tensors;

namespace TestVideoWriter
{

   public static class ext
    {
        public static void SetGDIHighQuality(this Graphics g)
        {
            // Ottimizzazioni per GPU (funziona su schemi NVIDIA/AMD/Intel)
            g.CompositingMode = CompositingMode.SourceCopy;
            g.InterpolationMode = InterpolationMode.Low; // Trade-off qualità/performance
            g.TextRenderingHint = System.Drawing.Text.TextRenderingHint.AntiAliasGridFit;
        }
    }

    /// <summary>
    /// Logica di interazione per MainWindow.xaml
    /// </summary>
    public partial class MainWindow : Window
    {
        MediaBufferPool _bufferPool;
        MediaSamplePool _samplePool;

        D3D11Renderer _renderer;

        public MainWindow()
        {
            InitializeComponent();
            Loaded += OnWindowLoaded;

#if TEST_ONNX
            var sessionOptions = new SessionOptions();
            int quale = 2;
            // CPU è sempre disponibile
            sessionOptions.AppendExecutionProvider_CPU();
            Console.WriteLine("Provider disponibile: CPU");
            MessageBox.Show("Provider disponibile: CPU");
            try
            {
                // Prova ad aggiungere DirectML
                sessionOptions.AppendExecutionProvider_DML();
                Console.WriteLine("Provider disponibile: DirectML");
                MessageBox.Show("Provider disponibile: DirectML");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"DirectML non disponibile: {ex.Message}");
                MessageBox.Show($"DirectML non disponibile: {ex.Message}");
            }

            try
            {
                // Prova ad aggiungere CUDA
                sessionOptions.AppendExecutionProvider_CUDA(1);
                Console.WriteLine("Provider disponibile: CUDA");
                Console.WriteLine("Provider disponibile: CUDA");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"CUDA non disponibile: {ex.Message}");
                MessageBox.Show($"CUDA non disponibile: {ex.Message}");
            }
            
            // l'ultimo trovato resta quello selezionato
            

            // carico il modello onnx dalla cartella dell'applicazione (in questo caso è un modello di riconoscimento immagini)
            var modelPath = System.IO.Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "ONNX\\yolov5l.onnx");
            var session = new InferenceSession(modelPath, sessionOptions);
            // eseguo l'inferenza con un tensore di input (immagine 224x224x3)
            // 2. Prepara i dati come array flat
            float[] inputData = new float[1 * 3 * 640 * 640]; // Esempio per YOLOv5
            int[] dimensions = new[] { 1, 3, 640, 640 };

            // 3. Crea il tensore usando la classe Tensor di ONNX Runtime 1.21
            var tensor = new DenseTensor<float>(inputData, dimensions);

            // 4. Crea l'input
            var inputs = new List<NamedOnnxValue>
            {
                NamedOnnxValue.CreateFromTensor("images", tensor) // "images" deve matchare il modello
            };

            Stopwatch sw = new Stopwatch();
            sw.Start();
            // 5. Esegui l'inferenza
            using (var results = session.Run(inputs))
            {
                var output = results.First().AsTensor<float>();
            }
            sw.Stop();
            MessageBox.Show($"Inferenza in {sw.ElapsedMilliseconds} ms");
#endif
        }

        private void OnWindowLoaded(object sender, RoutedEventArgs e)
        {
            _renderer = new D3D11Renderer(1280, 720); // Risoluzione telecamera
            imgScan.Source = _renderer.GetImageSource();
        }

        public static readonly Guid MF_SAMPLE_EXTENSION_ZOOM = new Guid("BFF6C2E1-8A10-4E70-BD7C-9B1A94F85F3A");

        private async void button_Click(object sender, RoutedEventArgs e)
        {
            int W = 4000;
            int H = 3000;
            double Quality = 0.08;
            int FrameRate = 18;
            int BitRate = (int)(W * H * FrameRate * Quality);

            var writer = new AsyncMediaWriter();
            await writer.InitializeAsync("output.mp4", W, H, FrameRate, BitRate);

            // Parametri per i frame
            int totalFrames = 180;
            long rtStart = 0;
            long frameDuration = 10000000 / FrameRate; // 100 ns unità, per 30 fps
            
            Bitmap bmp = new Bitmap(W, H, System.Drawing.Imaging.PixelFormat.Format32bppRgb);

            for (int i = 0; i < totalFrames; i++)
            {
                // Crea una bitmap (640x480) e disegna un rettangolo in movimento.
                //using (Bitmap bmp = new Bitmap(W, H, System.Drawing.Imaging.PixelFormat.Format32bppRgb))
                {
                    using (Graphics g = Graphics.FromImage(bmp))
                    {
                        g.Clear(System.Drawing.Color.Black);
                        g.FillRectangle(System.Drawing.Brushes.Red, i % W, 100, 50, 50);
                    }

                    // Blocca i dati della bitmap
                    BitmapData bmpData = bmp.LockBits(new System.Drawing.Rectangle(0, 0, bmp.Width, bmp.Height),
                        ImageLockMode.ReadOnly, bmp.PixelFormat);
                    int dataSize = Math.Abs(bmpData.Stride) * bmp.Height;

                    await writer.WriteFrameAsync(bmpData.Scan0, dataSize);
                    bmp.UnlockBits(bmpData);
                }
                rtStart += frameDuration;
            }
            bmp.Dispose();
            // Finalizza la scrittura e chiude il file.
            await writer.FinalizeAsync();

            //Console.WriteLine("Video creato con successo: " + outputFile);
            Console.Beep(1000, 250);
        }
        //catch (Exception ex)
        //{
        //    Console.WriteLine("Errore: " + ex.Message);
        //}
        //finally
        //{
        //    MFExtern.MFShutdown();
        //}
    

        private void button1_Click(object sender, RoutedEventArgs e)
        {
            // Percorso del file di input
            string filePath = "output_v2.mp4";

            // Crea lo Source Reader dal file
            IMFSourceReader sourceReader;
            MFError.ThrowExceptionForHR(MFExtern.MFCreateSourceReaderFromURL(filePath, null, out sourceReader));

            // Ottieni il media type nativo per il primo stream video
            IMFMediaType nativeType;
            MFError.ThrowExceptionForHR(sourceReader.GetNativeMediaType((int)MF_SOURCE_READER.FirstVideoStream, 0, out nativeType));

            // Estrai le dimensioni del frame (MF_MT_FRAME_SIZE è un QWORD in cui i 32 bit superiori rappresentano la larghezza e i 32 bit inferiori l'altezza)
            long frameSizeValue;
            MFError.ThrowExceptionForHR(nativeType.GetUINT64(MFAttributesClsid.MF_MT_FRAME_SIZE, out frameSizeValue));
            uint width = (uint)(frameSizeValue >> 32);
            uint height = (uint)(frameSizeValue & 0xFFFFFFFF);
            Console.WriteLine("Dimensioni frame: {0} x {1}", width, height);

            // Estrai il frame rate (MF_MT_FRAME_RATE: QWORD con numeratore (32 bit superiori) e denominatore (32 bit inferiori))
            long avgFrameRateValue;
            MFError.ThrowExceptionForHR(nativeType.GetUINT64(MFAttributesClsid.MF_MT_FRAME_RATE, out avgFrameRateValue));
            uint frameRateNumerator = (uint)(avgFrameRateValue >> 32);
            uint frameRateDenom = (uint)(avgFrameRateValue & 0xFFFFFFFF);
            double framerate = frameRateDenom != 0 ? (double)frameRateNumerator / frameRateDenom : 0;
            Console.WriteLine("Frame rate: {0:F2} fps", framerate);

            // Estrai il bitrate medio (MF_MT_AVG_BITRATE) – di solito in bit/s
            int bitrate;
            MFError.ThrowExceptionForHR(nativeType.GetUINT32(MFAttributesClsid.MF_MT_AVG_BITRATE, out bitrate));
            Console.WriteLine("Bitrate: {0} bit/s", bitrate);

            // Ora cicla sui frame del video
            while (true)
            {
                int streamIndex, flags;
                long sampleTime;
                IMFSample sample;
                // Legge il sample dal primo stream video
                MFError.ThrowExceptionForHR(sourceReader.ReadSample(
                    (int)MF_SOURCE_READER.FirstVideoStream,
                    0,
                    out streamIndex,
                    out flags,
                    out sampleTime,
                    out sample));

                // Se sample è null, abbiamo terminato la lettura
                if (sample == null)
                {
                    break;
                }

                // Estrai il metadato "zoom" dal sample (se presente)
                double zoomFactor = 0;
                int hr = sample.GetDouble(MF_SAMPLE_EXTENSION_ZOOM, out zoomFactor);
                if (hr == 0)
                {
                    Console.WriteLine("Timestamp: {0} - Zoom: {1}", sampleTime, zoomFactor);
                }
                else
                {
                    Console.WriteLine("Timestamp: {0} - Zoom non disponibile.", sampleTime);
                }

                // Supponiamo di avere un IMFSample 'sample'
                IMFMediaBuffer mediaBuffer;
                MFError.ThrowExceptionForHR(sample.ConvertToContiguousBuffer(out mediaBuffer));

                IntPtr pBuffer;
                int maxLength, currentLength;
                MFError.ThrowExceptionForHR(mediaBuffer.Lock(out pBuffer, out maxLength, out currentLength));

                // A questo punto pBuffer punta ai dati dei pixel, con currentLength byte validi.
                // Puoi copiare i dati in un array se necessario:
                byte[] pixelData = new byte[currentLength];
                Marshal.Copy(pBuffer, pixelData, 0, currentLength);

                // Quando hai finito, sblocca il buffer e rilascia le risorse:
                MFError.ThrowExceptionForHR(mediaBuffer.Unlock());
                Marshal.ReleaseComObject(mediaBuffer);
                
                // Rilascia il sample COM per evitare memory leak
                Marshal.ReleaseComObject(sample);
            }

            // Pulizia finale
            Marshal.ReleaseComObject(nativeType);
            Marshal.ReleaseComObject(sourceReader);
            
            Console.WriteLine("Fine elaborazione.");
            Console.ReadLine();
        }

        private async void button2_Click(object sender, RoutedEventArgs e)
        {
            MediaReader mr = new MediaReader("output_v2.mp4");
            var framerate = mr.Framerate;
            var width = mr.FrameWidth;
            var height = mr.FrameHeight;

            int duration = (int)(1000 / framerate); // millisecondi

            BitmapSource frame;
            while (true)
            {
                frame = mr.QueryFrameBitmapSource();
                if (frame == null) break;
                
                // Visualizza il frame
                imgScan.Source = frame;
                imgScan.UpdateLayout();
                await Task.Delay(10);
            }
        }

        public static class MFCustomGuids
        {
            public static readonly Guid MF_DEVSOURCE_ATTRIBUTE_SOURCE_TYPE_VIDCAP_GUID = new Guid(0x8ac3587a, 0x4ae7, 0x42d8, 0x99, 0xe0, 0x0a, 0x60, 0x13, 0xee, 0xf9, 0x0f);
        }

        private async void btnWebCam_Click(object sender, RoutedEventArgs e)
        {
            millis = 0;
            nn = 0;
            MediaFoundationManager.Startup();
            try
            {
                // Enumerare tutte le sorgenti video disponibili
                IMFAttributes pConfig;
                var r = MFExtern.MFCreateAttributes(out pConfig, 1);
                r = pConfig.SetGUID(MFAttributesClsid.MF_DEVSOURCE_ATTRIBUTE_SOURCE_TYPE, MFCustomGuids.MF_DEVSOURCE_ATTRIBUTE_SOURCE_TYPE_VIDCAP_GUID);

                IMFActivate[] devices;
                int count;
                r = MFExtern.MFEnumDeviceSources(pConfig, out devices, out count);

                if (count == 0)
                {
                    Console.WriteLine("Nessuna telecamera trovata.");
                    return;
                }

                // Seleziona la prima telecamera
                IMFMediaSource mediaSource;
                devices[0].ActivateObject(typeof(IMFMediaSource).GUID, out object obj);
                mediaSource = obj as IMFMediaSource;

                // Ottieni la Source Reader dalla Media Source
                IMFSourceReader sourceReader;
                MFExtern.MFCreateSourceReaderFromMediaSource(mediaSource, null, out sourceReader);

                var imc = mediaSource as IAMCameraControl;
                int rMin, rMax, rDelta, rDeflt;
                CameraControlFlags cflag;
                var rslt = imc.GetRange(CameraControlProperty.Exposure, out rMin, out rMax, out rDelta, out rDeflt, out cflag);

                // Enumerare i formati disponibili
                Console.WriteLine("Formati video disponibili:");
                int index = 0;
                bool trovato = false;
                while (true)
                {
                    IMFMediaType mediaType;
                    int hr = sourceReader.GetNativeMediaType((int)MF_SOURCE_READER.FirstVideoStream, index, out mediaType);
                    if (hr != 0) break; // Nessun altro formato disponibile

                    // Ottieni il formato colore
                    Guid subtype;
                    mediaType.GetGUID(MFAttributesClsid.MF_MT_SUBTYPE, out subtype);

                    // Leggiamo la risoluzione(MF_MT_FRAME_SIZE)
                    long frameSize;
                    hr = mediaType.GetUINT64(MFAttributesClsid.MF_MT_FRAME_SIZE, out frameSize);
                    int width = (int)(frameSize >> 32);
                    int height = (int)(frameSize & 0xFFFFFFFF);

                    // Leggiamo il frame rate (MF_MT_FRAME_RATE)
                    long frameRate;
                    hr = mediaType.GetUINT64(MFAttributesClsid.MF_MT_FRAME_RATE, out frameRate);
                    int fpsNum = (int)(frameRate >> 32);
                    int fpsDen = (int)(frameRate & 0xFFFFFFFF);
                    double fps = (fpsDen != 0) ? (double)fpsNum / fpsDen : 0;

                    Console.WriteLine($"Formato {index}: {GetSubtypeName(subtype)} Res:{width}x{height} Fps: {fps}");
                    if (width==1280 && height == 720 && fps==30 && subtype== MFMediaType.NV12) // Seleziona il formato 640x480
                    {
                        var re = sourceReader.SetCurrentMediaType((int)MF_SOURCE_READER.FirstVideoStream, IntPtr.Zero, mediaType);
                        trovato = true;
                        break;
                    }
                    Marshal.ReleaseComObject(mediaType);
                    index++;
                }
                if (!trovato)
                {
                    Console.WriteLine("Formato video non trovato.");
                    return;
                }
                // alloco il pool di buffers:
                _bufferPool = new MediaBufferPool(1280 * 720 * 3, 3);
                _samplePool = new MediaSamplePool(1280 * 720 * 4, 3);
                WriteableBitmap wb = new WriteableBitmap(1280, 720, 96, 96, PixelFormats.Bgr24, null);

                sourceReader.SetStreamSelection((int)MF_SOURCE_READER.FirstVideoStream, true);
                long millis1 = 0;
                int nn1 = 0;
                for (int i = 0; i < 30 * 10; i++)
                {
                    Stopwatch sw1 = new Stopwatch();
                    sw1.Restart();
                    var b = GetFrame(sourceReader);
                    //sw1.Stop();
                    //var resized = ResizeBitmapWithGpu(b, 640, 480);
                    Bitmap resized = null;
                    Bitmap big = new Bitmap(4000, 3000, System.Drawing.Imaging.PixelFormat.Format32bppArgb);
                    
                    if (b!=null) resized = ResizeWithGpuDirect(big, 2000 , 1500);
                    
                    Trace.WriteLine($"{i} - Tempo di conversione: {sw1.ElapsedMilliseconds}");
                    imgScan.Dispatcher.Invoke(() =>
                    {
                        imgScan.Source = b;
                        imgScan.UpdateLayout();
                    });
                    await Task.Delay(10);
                    millis1 += sw1.ElapsedMilliseconds;
                    nn1++;
                }
                Console.WriteLine($"Tempo di frame: {(float)millis1/(float)nn1}  fps:{1000*nn1/millis1}");
                Trace.WriteLine($"Tempo medio di conversione: {(float)millis/(float)nn}");

                // Cleanup
                // rilascio il pool di buffers
                _bufferPool.Dispose();
                _samplePool.Dispose();
                // rilascio il reader e la source
                Marshal.ReleaseComObject(sourceReader);
                Marshal.ReleaseComObject(mediaSource);
                // rilascio i devices
                foreach (var device in devices) Marshal.ReleaseComObject(device);
            }
            finally
            {
                MediaFoundationManager.Shutdown();
            }
        }


        public static BitmapSource BitmapToBitmapSourceFast(Bitmap bitmap)
        {
            // Blocca i pixel della Bitmap in memoria
            var bitmapData = bitmap.LockBits(
                new System.Drawing.Rectangle(0, 0, bitmap.Width, bitmap.Height),
                ImageLockMode.ReadOnly,
                System.Drawing.Imaging.PixelFormat.Format32bppArgb // Usa Format32bppArgb per compatibilità
            );

            try
            {
                // Crea una BitmapSource direttamente dai pixel
                var bitmapSource = BitmapSource.Create(
                    bitmapData.Width,
                    bitmapData.Height,
                    bitmap.HorizontalResolution,
                    bitmap.VerticalResolution,
                    PixelFormats.Bgra32, // WPF usa Bgra32 (non Argb32!)
                    null,
                    bitmapData.Scan0,
                    bitmapData.Stride * bitmapData.Height,
                    bitmapData.Stride
                );

                // Congela per migliorare le prestazioni in WPF
                bitmapSource.Freeze();
                return bitmapSource;
            }
            finally
            {
                // Sblocca la Bitmap
                bitmap.UnlockBits(bitmapData);
            }
        }

        public static Bitmap ResizeWithGpuDirect(Bitmap source, int newWidth, int newHeight)
        {
            var dest = new Bitmap(newWidth, newHeight, source.PixelFormat);

            using (var g = Graphics.FromImage(dest))
            {
                g.InterpolationMode = InterpolationMode.HighQualityBicubic;
                g.CompositingQuality = CompositingQuality.HighSpeed;
                g.SmoothingMode = SmoothingMode.HighSpeed;
                g.PixelOffsetMode = PixelOffsetMode.HighSpeed;

                // Imposta il rendering a hardware (se supportato)
                g.SetGDIHighQuality(); // Estensione custom (vedi sotto)

                g.DrawImage(source, 0, 0, newWidth, newHeight);
            }

            return dest;
        }

        
        public static BitmapSource ResizeWithGpu(BitmapSource source, int newWidth, int newHeight)
        {
            // 1. Calcola il fattore di scaling
            double scaleX = (double)newWidth / source.PixelWidth;
            double scaleY = (double)newHeight / source.PixelHeight;

            // 2. Usa TransformedBitmap per scaling GPU
            var transform = new ScaleTransform(scaleX, scaleY);
            var resized = new TransformedBitmap(source, transform);

            // 3. Ritorna il risultato (senza copia CPU)
            return resized;
        }

        public static BitmapSource ResizeBitmapWithGpu(BitmapSource source, int newWidth, int newHeight)
    {
        // 1. Crea una WriteableBitmap con le nuove dimensioni
        var resized = new WriteableBitmap(
            newWidth,
            newHeight,
            96, 96, // DPI (usare 96 per compatibilità)
            PixelFormats.Pbgra32, // Formato ottimizzato per GPU
            null);

        // 2. Crea un DrawingVisual per il ridimensionamento GPU
        var drawingVisual = new DrawingVisual();
        using (var drawingContext = drawingVisual.RenderOpen())
        {
            drawingContext.DrawImage(
                source,
                new System.Windows.Rect(0, 0, newWidth, newHeight));
        }

        // 3. Renderizza su RenderTargetBitmap (GPU-accelerato)
        var renderTarget = new RenderTargetBitmap(
            newWidth, newHeight,
            96, 96,
            PixelFormats.Pbgra32);

        renderTarget.Render(drawingVisual);

        // 4. Copia i pixel nella WriteableBitmap finale
        renderTarget.CopyPixels(
            new Int32Rect(0, 0, newWidth, newHeight),
            resized.BackBuffer,
            resized.BackBufferStride * newHeight,
            resized.BackBufferStride);

        resized.Lock();
        resized.AddDirtyRect(new Int32Rect(0, 0, newWidth, newHeight));
        resized.Unlock();

        return resized;
    }
    private unsafe void GetFrameToWriteableBitmap(IMFSourceReader sourceReader, WriteableBitmap wb)
        {
            IMFSample sample = _samplePool.Rent();
            IMFMediaBuffer buffer = _bufferPool.Rent();

            try
            {
                int flags;
                long timestamp;

                var hr = sourceReader.ReadSample((int)MF_SOURCE_READER.FirstVideoStream, 0, out _, out flags, out timestamp, out sample);
                if (hr != 0 || sample == null) return;

                sample.ConvertToContiguousBuffer(out buffer);

                IntPtr dataPtr;
                int maxLength, currentLength;
                buffer.Lock(out dataPtr, out maxLength, out currentLength);

                // Scrivi direttamente sulla WriteableBitmap
                wb.Lock();
                wb.WritePixels(
                    new Int32Rect(0, 0, wb.PixelWidth, wb.PixelHeight),
                    dataPtr, currentLength, wb.BackBufferStride
                );
                wb.Unlock();
            }
            finally
            {
                buffer.Unlock();
                _bufferPool.Return(buffer);
                _samplePool.Return(sample);
            }
        }

        public BitmapSource GetFrame(IMFSourceReader sourceReader)
        {
            IMFSample sample=_samplePool.Rent();
            int flags;
            long timestamp;
           
            int hr=-1;
            
            hr = sourceReader.ReadSample((int)MF_SOURCE_READER.FirstVideoStream, 0, out _, out flags, out timestamp, out sample);
            if (hr != 0 || sample == null) return null;

            // Ottieni il buffer
            //IMFMediaBuffer buffer;
            IMFMediaBuffer buffer = _bufferPool.Rent();
            sample.ConvertToContiguousBuffer(out buffer);
            _samplePool.Return(sample);

            // Blocca e ottieni i dati
            IntPtr pBuffer;
            int maxLength, currentLength;
            buffer.Lock(out pBuffer, out maxLength, out currentLength);
            Stopwatch sw = new Stopwatch();
            sw.Restart();
            //var bmp = DecodeMjpegFrame(pBuffer, currentLength);
            var bmp = NV12ToRGB24Converter.ConvertNV12ToRGB24_fast(pBuffer, 1280, 720);
            //bmp.Freeze();
            sw.Stop();
            millis +=sw.ElapsedMilliseconds;
            nn++;
            buffer.Unlock();
            // restituisco il buffer al pool:
            _bufferPool.Return(buffer);

            //Marshal.ReleaseComObject(buffer);
            //Marshal.ReleaseComObject(sample);

            return bmp;
        }

        long millis = 0;
        long nn = 0;

        public static BitmapSource DecodeMjpegFrame(IntPtr mjpegFrame, long bufferSize)
        {
            // Crea un'istanza del decompressore
            using (var decompressor = new TJDecompressor())
            {
                // Decodifica il frame MJPEG in formato RGB
                byte[] rgbData = decompressor.Decompress(mjpegFrame, (ulong)bufferSize, TJPixelFormat.RGB, TJFlags.None,out int width, out int height, out int stride);

                // Crea una BitmapSource WPF
                return BitmapSource.Create(
                    width,                              // Larghezza
                    height,                             // Altezza
                    96,                                 // DPI orizzontale
                    96,                                 // DPI verticale
                    System.Windows.Media.PixelFormats.Rgb24, // Formato pixel (RGB24)
                    null,                               // Palette (null per RGB)
                    rgbData,                           // Dati dell'immagine
                    width * 3                           // Stride (3 byte per pixel)
                );
            }
        }

        // Mappa i GUID dei formati colore a nomi leggibili
        static string GetSubtypeName(Guid subtype)
        {
            if (subtype == MFMediaType.RGB32) return "RGB32";
            if (subtype == MFMediaType.RGB24) return "RGB24";
            if (subtype == MFMediaType.YUY2) return "YUY2";
            if (subtype == MFMediaType.NV12) return "NV12";
            if (subtype == MFMediaType.UYVY) return "UYVY";
            if (subtype == MFMediaType.I420) return "I420";
            if (subtype == MFMediaType.YV12) return "YV12";
            if (subtype == MFMediaType.YVYU) return "YVYU";
            if (subtype == MFMediaType.MJPG) return "MJPEG";
            return $"Sconosciuto ({subtype})";
        }
    }
}
