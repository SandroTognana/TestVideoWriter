using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace TestVideoWriter
{
    using MediaFoundation.OPM;
    using System;
    using System.Numerics;
    using System.Runtime.InteropServices;
    using System.Windows.Media;
    using System.Windows.Media.Imaging;

    public static class YUY2ToRGB24Converter
    {
        public static BitmapSource ConvertYUY2ToRGB24(IntPtr yuy2Buffer, int width, int height)
        {
            // Calcola la dimensione del buffer YUY2
            int yuy2Stride = width * 2; // 2 byte per pixel in YUY2
            int yuy2BufferSize = yuy2Stride * height;

            // Crea un buffer per l'immagine RGB24
            int rgbStride = width * 3; // 3 byte per pixel in RGB24
            byte[] rgbBuffer = new byte[rgbStride * height];

            // Copia i dati dal buffer YUY2 a un array gestito
            byte[] yuy2Data = new byte[yuy2BufferSize];
            Marshal.Copy(yuy2Buffer, yuy2Data, 0, yuy2BufferSize);

            // Converti YUY2 in RGB24 utilizzando Parallel.For
            Parallel.For(0, height, y =>
            {
                for (int x = 0; x < width; x += 2)
                {
                    int index = y * yuy2Stride + x * 2;

                    // Estrai i valori YUY2
                    byte y0 = yuy2Data[index];
                    byte u = yuy2Data[index + 1];
                    byte y1 = yuy2Data[index + 2];
                    byte v = yuy2Data[index + 3];

                    // Converti YUY2 in RGB per i due pixel
                    ConvertYUY2ToRGB(y0, u, v, out byte r0, out byte g0, out byte b0);
                    ConvertYUY2ToRGB(y1, u, v, out byte r1, out byte g1, out byte b1);

                    // Scrivi i valori RGB nel buffer
                    int rgbIndex = y * rgbStride + x * 3;
                    rgbBuffer[rgbIndex] = r0;
                    rgbBuffer[rgbIndex + 1] = g0;
                    rgbBuffer[rgbIndex + 2] = b0;

                    rgbBuffer[rgbIndex + 3] = r1;
                    rgbBuffer[rgbIndex + 4] = g1;
                    rgbBuffer[rgbIndex + 5] = b1;
                }
            });

            // Crea una BitmapSource dal buffer RGB24
            BitmapSource bitmapSource = BitmapSource.Create(
                width, height, 96, 96, PixelFormats.Rgb24, null, rgbBuffer, rgbStride);

            return bitmapSource;
        }

        static private void ConvertYUY2ToRGB(byte y, byte u, byte v, out byte r, out byte g, out byte b)
        {
            int c = y - 16;
            int d = u - 128;
            int e = v - 128;

            r = (byte)((298 * c + 409 * e + 128) >> 8);
            r = (r < 0) ? (byte)0 : (r > 255) ? (byte)255 : r;

            g = (byte)((298 * c - 100 * d - 208 * e + 128) >> 8);
            g = (g < 0) ? (byte)0 : (g > 255) ? (byte)255 : g;

            b = (byte)((298 * c + 516 * d + 128) >> 8);
            b = (b < 0) ? (byte)0 : (b > 255) ? (byte)255 : b;
        }
    }
}
