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

namespace TestVideoWriter
{
    /// <summary>
    /// Logica di interazione per MainWindow.xaml
    /// </summary>
    public partial class MainWindow : Window
    {
        

        public MainWindow()
        {
            InitializeComponent();
        }

        private void button_Click(object sender, RoutedEventArgs e)
        {
            // Inizializza Media Foundation.
            // Inizializza Media Foundation (versione specificata: 0x00020070)
            MFError.ThrowExceptionForHR(MFExtern.MFStartup(0x00020070, MFStartup.Full));

            Guid HEVC = new FourCC("HEVC").ToMediaSubtype();
            Guid H265 = new FourCC("H265").ToMediaSubtype();
            try
            {
                string outputFile = "output_v2.mp4";

                // Crea attributi per il Sink Writer.
                IMFAttributes attributes;
                MFError.ThrowExceptionForHR(MFExtern.MFCreateAttributes(out attributes, 1));
                // Abilita trasformazioni hardware se disponibili.
                attributes.SetUINT32(MFAttributesClsid.MF_READWRITE_ENABLE_HARDWARE_TRANSFORMS, 1);

                // Crea il Sink Writer dal file di output.
                IMFSinkWriter sinkWriter;
                MFError.ThrowExceptionForHR(MFExtern.MFCreateSinkWriterFromURL(outputFile, null, attributes, out sinkWriter));

                int streamIndex;
                

        // Configura il formato di output (codifica H.264)
                IMFMediaType outputMediaType;
                MFError.ThrowExceptionForHR(MFExtern.MFCreateMediaType(out outputMediaType));
                outputMediaType.SetGUID(MFAttributesClsid.MF_MT_MAJOR_TYPE, MFMediaType.Video);
                outputMediaType.SetGUID(MFAttributesClsid.MF_MT_SUBTYPE, HEVC);
                outputMediaType.SetUINT32(MFAttributesClsid.MF_MT_AVG_BITRATE, 800000);
                outputMediaType.SetUINT32(MFAttributesClsid.MF_MT_INTERLACE_MODE, 2); // progressive
                long frameSize = (((long)640) << 32) | ((uint)480);
                outputMediaType.SetUINT64(MFAttributesClsid.MF_MT_FRAME_SIZE, frameSize);
                long framerate = (((long)30) << 32) | ((uint)1);
                outputMediaType.SetUINT64(MFAttributesClsid.MF_MT_FRAME_RATE, framerate);
                long pixelAspectRatio = (((long)1) << 32) | ((uint)1);
                outputMediaType.SetUINT64(MFAttributesClsid.MF_MT_PIXEL_ASPECT_RATIO, pixelAspectRatio);
                

                MFError.ThrowExceptionForHR(sinkWriter.AddStream(outputMediaType, out streamIndex));

                // Configura il formato di input (RGB32)
                IMFMediaType inputMediaType;
                MFError.ThrowExceptionForHR(MFExtern.MFCreateMediaType(out inputMediaType));
                inputMediaType.SetGUID(MFAttributesClsid.MF_MT_MAJOR_TYPE, MFMediaType.Video);
                inputMediaType.SetGUID(MFAttributesClsid.MF_MT_SUBTYPE, MFMediaType.RGB32);
                inputMediaType.SetUINT32(MFAttributesClsid.MF_MT_INTERLACE_MODE, 2); // progressive

                inputMediaType.SetUINT64(MFAttributesClsid.MF_MT_FRAME_SIZE, frameSize);
                inputMediaType.SetUINT64(MFAttributesClsid.MF_MT_FRAME_RATE, framerate);
                inputMediaType.SetUINT64(MFAttributesClsid.MF_MT_PIXEL_ASPECT_RATIO, pixelAspectRatio);

                MFError.ThrowExceptionForHR(sinkWriter.SetInputMediaType(streamIndex, inputMediaType, null));

                // Avvia la scrittura
                MFError.ThrowExceptionForHR(sinkWriter.BeginWriting());

                // Parametri per i frame
                int totalFrames = 300;
                long rtStart = 0;
                long frameDuration = 10000000 / 30; // 100 ns unità, per 30 fps

                for (int i = 0; i < totalFrames; i++)
                {
                    // Crea una bitmap (640x480) e disegna un rettangolo in movimento.
                    using (Bitmap bmp = new Bitmap(640, 480, System.Drawing.Imaging.PixelFormat.Format32bppRgb))
                    {
                        using (Graphics g = Graphics.FromImage(bmp))
                        {
                            g.Clear(System.Drawing.Color.Black);
                            g.FillRectangle(System.Drawing.Brushes.Red, i % 640, 100, 50, 50);
                        }

                        // Blocca i dati della bitmap
                        BitmapData bmpData = bmp.LockBits(new System.Drawing.Rectangle(0, 0, bmp.Width, bmp.Height),
                            ImageLockMode.ReadOnly, bmp.PixelFormat);
                        int dataSize = Math.Abs(bmpData.Stride) * bmp.Height;

                        // Crea un campione Media Foundation
                        IMFSample sample;
                        MFError.ThrowExceptionForHR(MFExtern.MFCreateSample(out sample));

                        // Crea un buffer di memoria per il campione.
                        IMFMediaBuffer buffer;
                        MFError.ThrowExceptionForHR(MFExtern.MFCreateMemoryBuffer(dataSize, out buffer));

                        // Copia i dati della bitmap nel buffer.
                        IntPtr pBuffer;
                        int maxLen, currentLen;
                        MFError.ThrowExceptionForHR(buffer.Lock(out pBuffer, out maxLen, out currentLen));

                        byte[] pixelData = new byte[dataSize];
                        Marshal.Copy(bmpData.Scan0, pixelData, 0, dataSize);
                        Marshal.Copy(pixelData, 0, pBuffer, dataSize);

                        MFError.ThrowExceptionForHR(buffer.Unlock());
                        MFError.ThrowExceptionForHR(buffer.SetCurrentLength(dataSize));

                        MFError.ThrowExceptionForHR(sample.AddBuffer(buffer));
                        MFError.ThrowExceptionForHR(sample.SetSampleTime(rtStart));
                        MFError.ThrowExceptionForHR(sample.SetSampleDuration(frameDuration));

                        // Scrive il campione
                        MFError.ThrowExceptionForHR(sinkWriter.WriteSample(streamIndex, sample));

                        Marshal.ReleaseComObject(buffer);
                        Marshal.ReleaseComObject(sample);
                        bmp.UnlockBits(bmpData);
                    }
                    rtStart += frameDuration;
                }

                // Finalizza la scrittura e chiude il file.
                MFError.ThrowExceptionForHR(sinkWriter.Finalize_());
                Marshal.ReleaseComObject(sinkWriter);

                Console.WriteLine("Video creato con successo: " + outputFile);
                Console.Beep(1000, 250);
            }
            catch (Exception ex)
            {
                Console.WriteLine("Errore: " + ex.Message);
            }
            finally
            {
                MFExtern.MFShutdown();
            }
        }
    }
}
