using System;
using System.Threading;
using MediaFoundation;
using MediaFoundation.Misc;

public static class MediaFoundationManager
{
    private static int _initCount = 0;
    private static readonly object _lock = new object();

    /// <summary>
    /// Avvia Media Foundation se non è già stato avviato.
    /// </summary>
    public static void Startup()
    {
        lock (_lock)
        {
            if (_initCount == 0)
            {
                int hr = MFExtern.MFStartup(0x00020070, MFStartup.Full);
                if (hr < 0)
                {
                    throw new Exception($"MFStartup fallito con errore: 0x{hr:X}");
                }
            }
            Interlocked.Increment(ref _initCount);
        }
    }

    /// <summary>
    /// Chiude Media Foundation solo se nessun'altra parte del codice la sta ancora utilizzando.
    /// </summary>
    public static void Shutdown()
    {
        lock (_lock)
        {
            if (_initCount > 0)
            {
                Interlocked.Decrement(ref _initCount);
                if (_initCount == 0)
                {
                    MFExtern.MFShutdown();
                }
            }
        }
    }
}
