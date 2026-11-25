using System;

namespace OrbitalRoom.Features.VoiceRecognition
{
    /// <summary>
    /// Application-facing boundary for starting voice capture and receiving transcriptions.
    /// </summary>
    public interface IVoiceRecognitionService
    {
        event Action ListeningStarted;
        event Action ListeningStopped;
        event Action<string> TranscriptionReceived;
        event Action<string, string> RecognitionFailed;

        bool IsListening { get; }

        void StartListening();
        void StopListening();
    }
}
