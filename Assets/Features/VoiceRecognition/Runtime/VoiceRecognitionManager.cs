using UnityEngine;

namespace Features.VoiceRecognition
{
    /// <summary>
    /// Logs the lifecycle of the configured voice recognition service.
    /// It does not execute gameplay or furniture actions.
    /// </summary>
    public sealed class VoiceRecognitionManager : MonoBehaviour
    {
        [SerializeField] private MonoBehaviour voiceServiceBehaviour;

        [SerializeField] private bool startListeningOnEnable;

        private IVoiceRecognitionService voiceService;

        private void OnEnable()
        {
            voiceService = voiceServiceBehaviour as IVoiceRecognitionService;
            if (voiceService == null)
            {
                Debug.LogError("[Voice] VoiceRecognitionManager requires a MonoBehaviour implementing IVoiceRecognitionService.");
                return;
            }

            voiceService.ListeningStarted += HandleListeningStarted;
            voiceService.ListeningStopped += HandleListeningStopped;
            voiceService.TranscriptionReceived += HandleTranscriptionReceived;
            voiceService.RecognitionFailed += HandleRecognitionFailed;

            if (startListeningOnEnable)
            {
                StartListening();
            }
        }

        private void OnDisable()
        {
            if (voiceService == null)
            {
                return;
            }

            voiceService.ListeningStarted -= HandleListeningStarted;
            voiceService.ListeningStopped -= HandleListeningStopped;
            voiceService.TranscriptionReceived -= HandleTranscriptionReceived;
            voiceService.RecognitionFailed -= HandleRecognitionFailed;
        }

        /// <summary>Starts voice listening through the configured adapter.</summary>
        public void StartListening()
        {
            if (voiceService == null)
            {
                Debug.LogError("[Voice] Voice recognition service is not configured.");
                return;
            }

            voiceService.StartListening();
        }

        /// <summary>Stops voice listening through the configured adapter.</summary>
        public void StopListening()
        {
            if (voiceService == null)
            {
                Debug.LogError("[Voice] Voice recognition service is not configured.");
                return;
            }

            voiceService.StopListening();
        }

        private void HandleListeningStarted()
        {
            Debug.Log("[Voice] Listening started.");
        }

        private void HandleListeningStopped()
        {
            Debug.Log("[Voice] Listening stopped.");
        }

        private void HandleTranscriptionReceived(string transcription)
        {
            Debug.Log("[Voice] Recognized speech: " + transcription);
        }

        private void HandleRecognitionFailed(string errorCode, string errorMessage)
        {
            Debug.LogError("[Voice] Recognition error: " +
                (string.IsNullOrEmpty(errorCode) ? "unknown" : errorCode) +
                " - " +
                errorMessage);
        }
    }
}
