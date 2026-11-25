using System;
using Oculus.Voice;
using UnityEngine;

namespace Features.VoiceRecognition
{
    /// <summary>
    /// Adapter between the Meta Voice SDK/AppVoiceExperience component and the voice recognition boundary.
    /// </summary>
    public sealed class MetaWitVoiceRecognitionService : MonoBehaviour, IVoiceRecognitionService
    {
        [SerializeField] private AppVoiceExperience voiceExperience;

        public event Action ListeningStarted;
        public event Action ListeningStopped;
        public event Action<string> TranscriptionReceived;
        public event Action<string, string> RecognitionFailed;

        public bool IsListening => voiceExperience != null && voiceExperience.MicActive;

        private void OnEnable()
        {
            if (voiceExperience == null)
            {
                Debug.LogError("[Voice] AppVoiceExperience reference is missing.");
                return;
            }

            var events = voiceExperience.VoiceEvents;
            events.OnStartListening.AddListener(HandleListeningStarted);
            events.OnStoppedListening.AddListener(HandleListeningStopped);
            events.OnFullTranscription.AddListener(HandleFullTranscription);
            events.OnError.AddListener(HandleRecognitionError);
        }

        private void OnDisable()
        {
            if (voiceExperience == null)
            {
                return;
            }

            var events = voiceExperience.VoiceEvents;
            events.OnStartListening.RemoveListener(HandleListeningStarted);
            events.OnStoppedListening.RemoveListener(HandleListeningStopped);
            events.OnFullTranscription.RemoveListener(HandleFullTranscription);
            events.OnError.RemoveListener(HandleRecognitionError);
        }

        /// <summary>
        /// Starts microphone capture and a Wit.ai voice request.
        /// </summary>
        public void StartListening()
        {
            if (voiceExperience == null)
            {
                Debug.LogError("[Voice] Cannot start listening because AppVoiceExperience is missing.");
                return;
            }

            voiceExperience.Activate();
        }

        /// <summary>
        /// Stops the current microphone capture without aborting the response lifecycle.
        /// </summary>
        public void StopListening()
        {
            if (voiceExperience == null)
            {
                Debug.LogError("[Voice] Cannot stop listening because AppVoiceExperience is missing.");
                return;
            }

            voiceExperience.Deactivate();
        }

        private void HandleListeningStarted()
        {
            ListeningStarted?.Invoke();
        }

        private void HandleListeningStopped()
        {
            ListeningStopped?.Invoke();
        }

        private void HandleFullTranscription(string transcription)
        {
            if (string.IsNullOrWhiteSpace(transcription))
            {
                Debug.LogWarning("[Voice] No speech recognized.");
                return;
            }

            TranscriptionReceived?.Invoke(transcription);
        }

        private void HandleRecognitionError(string errorCode, string errorMessage)
        {
            RecognitionFailed?.Invoke(errorCode, errorMessage);
        }
    }
}
