using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

using System.Runtime.InteropServices;

namespace TestVideoWriter
{
    using System;
    using System.Runtime.InteropServices;
    using System.Threading.Tasks;
    using MediaFoundation;
    using MediaFoundation.ReadWrite;
    using MediaFoundation.Transform;
    using MediaFoundation.Misc;
    using MediaFoundation.OPM;
    using MediaFoundation.Net;

    

    class AsyncMediaWriter
    {
        private IMFSinkWriter _sinkWriter;
        private int _streamIndex;
        private long _rtStart;
        private int _frameRate = 30;
        private int _width = 1920;
        private int _height = 1080;
        private int _bitRate = 4000000; // 4 Mbps
        private Guid _videoFormat = MFMediaType.H264; // Cambia in MFVideoFormat_RGB32 per RAW
        Guid HEVC = new FourCC("HEVC").ToMediaSubtype();

        [DllImport("AVXCopy.dll", EntryPoint = "CopyMemoryAVX2")]
        private static extern void CopyMemoryAVX2(IntPtr src, IntPtr dst, int size);

        public async Task InitializeAsync(string outputFile,int W, int H,int FrameRate, int BitRate)
        {
            await Task.Run(() =>
            {
                MediaFoundationManager.Startup();
                _width= W;
                _height = H;
                _frameRate = FrameRate;
                _bitRate = BitRate;
                _streamIndex = 0;
                _rtStart = 0;
                IMFAttributes attr;
                MFExtern.MFCreateAttributes(out attr, 1);
                attr.SetUINT32(MFAttributesClsid.MF_READWRITE_ENABLE_HARDWARE_TRANSFORMS, 1);

                MFExtern.MFCreateSinkWriterFromURL(outputFile, null, attr, out _sinkWriter);

                ConfigureMediaType();
            });
        }

        private void ConfigureMediaType()
        {
            string outputFile = "output_v2.mp4";

            // Crea attributi per il Sink Writer.
            IMFAttributes attributes;
            MFError.ThrowExceptionForHR(MFExtern.MFCreateAttributes(out attributes, 1));
            // Abilita trasformazioni hardware se disponibili.
            attributes.SetUINT32(MFAttributesClsid.MF_READWRITE_ENABLE_HARDWARE_TRANSFORMS, 1);

            // Crea il Sink Writer dal file di output.
            MFError.ThrowExceptionForHR(MFExtern.MFCreateSinkWriterFromURL(outputFile, null, attributes, out _sinkWriter));

            // Configura il formato di output 
            IMFMediaType outputMediaType;
            MFError.ThrowExceptionForHR(MFExtern.MFCreateMediaType(out outputMediaType));
            outputMediaType.SetGUID(MFAttributesClsid.MF_MT_MAJOR_TYPE, MFMediaType.Video);
            outputMediaType.SetGUID(MFAttributesClsid.MF_MT_SUBTYPE, HEVC);
            outputMediaType.SetUINT32(MFAttributesClsid.MF_MT_AVG_BITRATE, _bitRate);
            outputMediaType.SetUINT32(MFAttributesClsid.MF_MT_INTERLACE_MODE, 2); // progressive
            long frameSize = (((long)_width) << 32) | ((uint)_height);
            outputMediaType.SetUINT64(MFAttributesClsid.MF_MT_FRAME_SIZE, frameSize);
            long framerate = (((long)_frameRate) << 32) | ((uint)1);
            outputMediaType.SetUINT64(MFAttributesClsid.MF_MT_FRAME_RATE, framerate);
            long pixelAspectRatio = (((long)1) << 32) | ((uint)1);
            outputMediaType.SetUINT64(MFAttributesClsid.MF_MT_PIXEL_ASPECT_RATIO, pixelAspectRatio);

            MFError.ThrowExceptionForHR(_sinkWriter.AddStream(outputMediaType, out _streamIndex));

            // Configura il formato di input (RGB32)
            IMFMediaType inputMediaType;
            MFError.ThrowExceptionForHR(MFExtern.MFCreateMediaType(out inputMediaType));
            inputMediaType.SetGUID(MFAttributesClsid.MF_MT_MAJOR_TYPE, MFMediaType.Video);
            inputMediaType.SetGUID(MFAttributesClsid.MF_MT_SUBTYPE, MFMediaType.RGB32);
            inputMediaType.SetUINT32(MFAttributesClsid.MF_MT_INTERLACE_MODE, 2);    // progressive

            inputMediaType.SetUINT64(MFAttributesClsid.MF_MT_FRAME_SIZE, frameSize);
            inputMediaType.SetUINT64(MFAttributesClsid.MF_MT_FRAME_RATE, framerate);
            inputMediaType.SetUINT64(MFAttributesClsid.MF_MT_PIXEL_ASPECT_RATIO, pixelAspectRatio);

            MFError.ThrowExceptionForHR(_sinkWriter.SetInputMediaType(_streamIndex, inputMediaType, null));

            // Avvia la scrittura
            MFError.ThrowExceptionForHR(_sinkWriter.BeginWriting());
        }

        public async Task WriteFrameAsync(IntPtr buffer, int bufferSize)
        {
            await Task.Run(() =>
            {
                IMFSample sample;
                MFExtern.MFCreateSample(out sample);

                IMFMediaBuffer mediaBuffer;
                MFExtern.MFCreateMemoryBuffer(bufferSize, out mediaBuffer);

                IntPtr pBuffer;
                int maxLength, currentLength;
                mediaBuffer.Lock(out pBuffer, out maxLength, out currentLength);

                unsafe
                {
                    Buffer.MemoryCopy(buffer.ToPointer(), pBuffer.ToPointer(), bufferSize, bufferSize);
                }

                mediaBuffer.Unlock();
                mediaBuffer.SetCurrentLength(bufferSize);
                sample.AddBuffer(mediaBuffer);

                sample.SetSampleTime(_rtStart);
                sample.SetSampleDuration(10000000 / _frameRate);    // Durata frame

                MFError.ThrowExceptionForHR(_sinkWriter.WriteSample(_streamIndex, sample));

                _rtStart += (10000000 / _frameRate);

                Marshal.ReleaseComObject(sample);
                sample = null;
                Marshal.ReleaseComObject(mediaBuffer);
                mediaBuffer = null;
            });
        }

        public async Task FinalizeAsync()
        {
            await Task.Run(() =>
            {
                MFError.ThrowExceptionForHR(_sinkWriter.Finalize_());
                Marshal.ReleaseComObject(_sinkWriter);
                MediaFoundationManager.Shutdown();
            });
        }

        

    unsafe void CopyWithAVX(void* source, void* destination, int dataSize)
    {
            //fixed (byte* srcPtr = source, dstPtr = destination)
            //{
            //    CopyMemoryAVX2((IntPtr)srcPtr, (IntPtr)dstPtr, size);
            //}
    }
}

}
