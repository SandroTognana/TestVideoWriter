using System;
using System.Drawing;
using System.Drawing.Imaging;
using System.Runtime.InteropServices;
using System.Windows.Media.Imaging;
using System.Windows.Media;
using MediaFoundation;
using MediaFoundation.Misc;
using MediaFoundation.ReadWrite;
using System.Runtime.Remoting.Messaging;
using System.Windows.Media.Media3D;
using System.Windows;
using MFModels;
using static TestVideoWriter.MainWindow;
using TestVideoWriter;
using System.Threading.Tasks;
using System.Diagnostics;

public class MediaReader : IDisposable
{
    private IMFSourceReader _reader;
    private uint _frameWidth;
    private uint _frameHeight;
    private uint _bufferWidth;
    private uint _bufferHeight;
    private double _framerate;
    private int _bitRate;

    MediaBufferPool _bufferPool;
    // dichiaro l'evento che verrà lanciato quando un frame è pronto e che passa un BitmapSource
    public event EventHandler<FrameEventArgs> FrameReady;
    // dichiaro FrameEventArgs
    public class FrameEventArgs : EventArgs
    {
        public BitmapSource Frame { get; set; }
        public int FrameWidth { get; set; }
        public int FrameHeight { get; set; }
        public int Stride { get; set; }
    }

    public MediaReader(int CamID, int width, int height, int fps)
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
        devices[CamID].ActivateObject(typeof(IMFMediaSource).GUID, out object obj);
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
            int fwidth = (int)(frameSize >> 32);
            int fheight = (int)(frameSize & 0xFFFFFFFF);

            // Leggiamo il frame rate (MF_MT_FRAME_RATE)
            long frameRate;
            hr = mediaType.GetUINT64(MFAttributesClsid.MF_MT_FRAME_RATE, out frameRate);
            int fpsNum = (int)(frameRate >> 32);
            int fpsDen = (int)(frameRate & 0xFFFFFFFF);
            double ffps = (fpsDen != 0) ? (double)fpsNum / fpsDen : 0;

            Console.WriteLine($"Formato {index}: {GetSubtypeName(subtype)} Res:{width}x{height} Fps: {fps}");
            if (width == fwidth && height == fheight && fps == ffps && subtype == MFMediaType.NV12) // Seleziona il formato 640x480
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
        _bufferPool = new MediaBufferPool(width * height * 3, 3);

        sourceReader.SetStreamSelection((int)MF_SOURCE_READER.FirstVideoStream, true);
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

    bool _isAcquiring = false;
    public void Start()
    {
        // Avvia la cattura video
        _reader.ReadSample((int)MF_SOURCE_READER.FirstVideoStream, 0, out _, out _, out _, out _);
        _isAcquiring = true;
        Task.Run(() => CaptureLoop());
        Trace.WriteLine("Cattura video avviata.");
    }

    public void Stop()
    {
        _isAcquiring = false;
        Trace.WriteLine("Arresto cattura video richiesto...");
    }

    private void CaptureLoop()
    {
        while (_isAcquiring)
        {
            // Leggi un frame
            BitmapSource frame = QueryFrameBitmapSource();
            if (frame != null)
            {
                // Notifica il frame pronto
                FrameReady?.Invoke(this, new FrameEventArgs { Frame = frame, FrameHeight=1280, FrameWidth=720, Stride=1280*4 }); 
            }
        }
        Trace.WriteLine("Cattura video terminata.");
    }

    public MediaReader(string filePath)
    {
        // Inizializza Media Foundation
        MediaFoundationManager.Startup();
       
        // Crea attributi per usare la GPU
        IMFAttributes attributes;
        MFExtern.MFCreateAttributes(out attributes, 1);
        attributes.SetUINT32(MFAttributesClsid.MF_SOURCE_READER_ENABLE_VIDEO_PROCESSING, 1);
        attributes.SetUINT32(MFAttributesClsid.MF_READWRITE_ENABLE_HARDWARE_TRANSFORMS, 1);

        // Crea il SourceReader
        var hf = MFExtern.MFCreateSourceReaderFromURL(filePath, attributes, out _reader);
        if (hf < 0)
            throw new Exception($"MFCreateSourceReaderFromURL fallito con errore: 0x{hf:X}");

        IMFMediaType mediaType;
        _reader.GetNativeMediaType((int)MF_SOURCE_READER.FirstVideoStream, 0, out mediaType);
        MFError.ThrowExceptionForHR(mediaType.SetUINT32(MFAttributesClsid.MF_READWRITE_ENABLE_HARDWARE_TRANSFORMS, 1));
        // d7388766-18fe-48c6-a177-ee894867c8c4
        // 00,00,00,00,00,00,00,00,a0,0f,00,00,b8,0b,00,00
        mediaType.GetUINT64(MFAttributesClsid.MF_MT_FRAME_SIZE, out long frameSize);
        _bufferWidth = (uint)(frameSize >> 32);
        _bufferHeight = (uint)(frameSize & 0xFFFFFFFF);

        byte[] geomAperture = new byte[16];
        if (mediaType.GetBlob(new Guid("d7388766-18fe-48c6-a177-ee894867c8c4"), geomAperture, 16, out int blobSize) == 0)
        {
            _frameWidth = (uint)BitConverter.ToInt32(geomAperture, 8);
            _frameHeight = (uint)BitConverter.ToInt32(geomAperture, 12);
        }

        mediaType.GetUINT64(MFAttributesClsid.MF_MT_FRAME_RATE, out long frameRate);
        _framerate = (double)(frameRate >> 32) / (frameRate & 0xFFFFFFFF);

        mediaType.GetUINT32(MFAttributesClsid.MF_MT_AVG_BITRATE, out int bitRate);
        _bitRate = bitRate;


        int count;
        int hr = mediaType.GetCount(out count);
        if (hr == 0)
        {
            for (int i = 0; i < count; i++)
            {
                Guid key;
                PropVariant value = new PropVariant();
                if (mediaType.GetItemByIndex(i, out key, value) == 0)
                {
                    string keyName = key.ToString(); // Nome dell'attributo (GUID)
                    string valueStr = value.ToString(); // Valore dell'attributo
                    Console.WriteLine($"{keyName}: {valueStr}");
                }
            }
        }

        // Configura il formato di output come RGB24
        SetOutputFormat();
    }

    private void SetOutputFormat()
    {

        IMFMediaType mediaType;
        MFExtern.MFCreateMediaType(out mediaType);

        mediaType.SetGUID(MFAttributesClsid.MF_MT_MAJOR_TYPE, MFMediaType.Video);
        mediaType.SetGUID(MFAttributesClsid.MF_MT_SUBTYPE, MFMediaType.RGB32);

        var e = _reader.SetCurrentMediaType((int)MF_SOURCE_READER.FirstVideoStream, IntPtr.Zero, mediaType);

        Marshal.ReleaseComObject(mediaType);
    }

    public double Framerate
    {
        get { return _framerate; }
    }
    public int FrameWidth
    {
        get { return (int)_frameWidth; }
    }
    public int FrameHeight
    {
        get { return (int)_frameHeight; }
    }

    public BitmapSource QueryFrameBitmapSource()
    {
        IMFSample sample;
        int hr = _reader.ReadSample((int)MF_SOURCE_READER.FirstVideoStream, 0, out _, out _, out long timeStamp, out sample);

        if (hr < 0 || sample == null)
            return null; // Fine del video

        // Ottieni il buffer dei dati del frame
        IMFMediaBuffer buffer;
        sample.ConvertToContiguousBuffer(out buffer);

        IntPtr dataPtr;
        int maxLength, currentLength;
        buffer.Lock(out dataPtr, out maxLength, out currentLength);

        // Converte il frame in BitmapSource (WPF)
        BitmapSource bitmap = BitmapSource.Create(
            (int)_bufferWidth, (int)_bufferHeight, // Modifica con la risoluzione effettiva
            96, 96,
            PixelFormats.Bgra32, // Assumiamo che i dati siano in RGB24
            null,
            dataPtr,
            currentLength,
            (int)_bufferWidth * 4 // Stride: larghezza × byte per pixel (BGR24 → 3 byte per pixel)
        );

        buffer.Unlock();
        Marshal.ReleaseComObject(buffer);
        Marshal.ReleaseComObject(sample);

        return bitmap;
    }

    public void Dispose()
    {
        if (_reader != null)
        {
            Marshal.ReleaseComObject(_reader);
            _reader = null;
        }
        MediaFoundationManager.Shutdown();
    }
}
