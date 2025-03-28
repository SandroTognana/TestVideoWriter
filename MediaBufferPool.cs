using MediaFoundation;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading.Tasks;

namespace TestVideoWriter
{
    public class MediaBufferPool : IDisposable
    {
        private readonly Queue<IMFMediaBuffer> _availableBuffers = new Queue<IMFMediaBuffer>();
        private readonly int _bufferSize;
        
        public MediaBufferPool(int bufferSize, int initialCapacity)
        {
            _bufferSize = bufferSize;
            for (int i = 0; i < initialCapacity; i++)
            {
                _availableBuffers.Enqueue(CreateBuffer());
            }
        }

        private IMFMediaBuffer CreateBuffer()
        {
            IMFMediaBuffer buffer;
            MFExtern.MFCreateMemoryBuffer(_bufferSize, out buffer);
            return buffer;
        }

        public IMFMediaBuffer Rent()
        {
            lock (_availableBuffers)
            {
                return _availableBuffers.Count > 0 ? _availableBuffers.Dequeue() : CreateBuffer();
            }
        }

        public void Return(IMFMediaBuffer buffer)
        {
            lock (_availableBuffers)
            {
                _availableBuffers.Enqueue(buffer);
            }
        }

        public void Dispose()
        {
            foreach (var buffer in _availableBuffers)
            {
                Marshal.ReleaseComObject(buffer);
            }
            _availableBuffers.Clear();
        }
    }

    public class MediaSamplePool : IDisposable
    {
        private readonly Queue<IMFSample> _availableSamples = new Queue<IMFSample>();
        private readonly int _initialBufferCount;
        private readonly int _bufferSize;
       
        public MediaSamplePool(int bufferSize, int initialSampleCount)
        {
            _bufferSize = bufferSize;
            _initialBufferCount = initialSampleCount;

            for (int i = 0; i < initialSampleCount; i++)
            {
                _availableSamples.Enqueue(CreateSample());
            }
        }

        private IMFSample CreateSample()
        {
            IMFSample sample;
            MFExtern.MFCreateSample(out sample);

            IMFMediaBuffer buffer;
            MFExtern.MFCreateMemoryBuffer(_bufferSize, out buffer);
            sample.AddBuffer(buffer);

            Marshal.ReleaseComObject(buffer); // Il sample ora possiede il buffer
            return sample;
        }

        public IMFSample Rent()
        {
            lock (_availableSamples)
            {
                return _availableSamples.Count > 0 ? _availableSamples.Dequeue() : CreateSample();
            }
        }

        public void Return(IMFSample sample)
        {
            lock (_availableSamples)
            {
                // Rimuovi tutti i buffer associati (opzionale, dipende dal tuo caso d'uso)
                sample.RemoveAllBuffers();
                _availableSamples.Enqueue(sample);
            }
        }

        public void Dispose()
        {
            foreach (var sample in _availableSamples)
            {
                Marshal.ReleaseComObject(sample);
            }
            _availableSamples.Clear();
        }
    }
}
