using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace TestVideoWriter
{
    using System;
    using System.Buffers;
    using System.Drawing;
    using System.Runtime.InteropServices;
    using System.Windows.Media.Imaging;
    using Emgu.CV;
    using Emgu.CV.CvEnum;
    using Emgu.CV.Reg;
    using Emgu.CV.Structure;
    using Lennox.LibYuvSharp;

    public class NV12ToRGB24Converter
    {
        public static BitmapSource ConvertNV12ToRGB24(IntPtr nv12Buffer, int width, int height)
        {
            // Calcola le dimensioni dei piani Y e UV
            int ySize = width * height;
            int uvSize = width * (height / 2);

            // Crea un buffer per i dati RGB24
            int rgbStride = width * 3; // 3 byte per pixel in RGB24
            byte[] rgbBuffer = new byte[rgbStride * height];

            // Copia i dati NV12 in array gestiti
            byte[] yPlane = new byte[ySize];
            byte[] uvPlane = new byte[uvSize];
            Marshal.Copy(nv12Buffer, yPlane, 0, ySize);
            Marshal.Copy(nv12Buffer + ySize, uvPlane, 0, uvSize);

            // Usa Parallel.For per parallelizzare la conversione
            Parallel.For(0, height, y =>
            {
                for (int x = 0; x < width; x++)
                {
                    // Indici per i piani Y e UV
                    int yIndex = y * width + x;
                    int uvIndex = (y / 2) * width + (x & ~1); // UV è a risoluzione dimezzata

                    // Estrai i valori Y, U, V
                    byte yValue = yPlane[yIndex];
                    byte uValue = uvPlane[uvIndex];
                    byte vValue = uvPlane[uvIndex + 1];

                    // Converti YUV in RGB
                    ConvertYUVToRGB(yValue, uValue, vValue, out byte r, out byte g, out byte b);

                    // Scrivi i valori RGB nel buffer
                    int rgbIndex = y * rgbStride + x * 3;
                    rgbBuffer[rgbIndex] = r;
                    rgbBuffer[rgbIndex + 1] = g;
                    rgbBuffer[rgbIndex + 2] = b;
                }
            });

            // Crea una BitmapSource dal buffer RGB24
            BitmapSource bitmapSource = BitmapSource.Create(
                width, height, 96, 96, System.Windows.Media.PixelFormats.Rgb24, null, rgbBuffer, rgbStride);

            return bitmapSource;
        }

        private static void ConvertYUVToRGB(byte y, byte u, byte v, out byte r, out byte g, out byte b)
        {
            // Converti i valori YUV in interi
            int yScaled = y - 16;
            int uScaled = u - 128;
            int vScaled = v - 128;

            // Applica le formule di conversione da YUV a RGB utilizzando calcoli interi
            int rTemp = (298 * yScaled + 409 * vScaled + 128) >> 8;
            int gTemp = (298 * yScaled - 100 * uScaled - 208 * vScaled + 128) >> 8;
            int bTemp = (298 * yScaled + 516 * uScaled + 128) >> 8;

            // Clamp dei valori RGB nel range 0-255
            r = (byte)(rTemp < 0 ? 0 : rTemp > 255 ? 255 : rTemp);
            g = (byte)(gTemp < 0 ? 0 : gTemp > 255 ? 255 : gTemp);
            b = (byte)(bTemp < 0 ? 0 : bTemp > 255 ? 255 : bTemp);
        }

        public static BitmapSource ConvertNV12ToRGB24_fast(IntPtr nv12Buffer, int width, int height)
        {
            // Calcola le dimensioni dei piani Y e UV
            int ySize = width * height;
            int uvSize = width * (height / 2);

            // Crea un buffer per i dati RGB24
            int rgbStride = width * 3; // 3 byte per pixel in RGB24
            //byte[] rgbBuffer = ArrayPool<byte>.Shared.Rent(rgbStride * height);
            byte[] rgbBuffer = new byte[rgbStride * height];
            //try
            //{
                unsafe
                {
                    byte* YplanePtr = (byte*)nv12Buffer.ToPointer();
                    byte* UVplanePtr = YplanePtr + ySize - 1;

                    // Usa LibYuvSharp per convertire NV12 in RGB24
                    fixed (byte* rgbPtr = rgbBuffer)
                    {
                        LibYuv.NV12ToRGB24(YplanePtr, width, UVplanePtr, width, rgbPtr, rgbStride, width, height);
                        // Crea una BitmapSource dal buffer RGB24
                        BitmapSource bitmapSource = BitmapSource.Create(width, height, 96, 96, System.Windows.Media.PixelFormats.Rgb24, null, rgbBuffer, rgbStride);
                        return bitmapSource;
                    }
                }
            //}
            //finally
            //{
            //    ArrayPool<byte>.Shared.Return(rgbBuffer);
            //}
            
        }
    }
}
