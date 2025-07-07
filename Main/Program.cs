using Main;

var transcriptor = new Transcriptor();
await transcriptor.Process(TranscriptionSource.MicrophoneAndAudio, sourceLanguage: "fr", cleanUp: true, concise: true, targetLanguage: "fr");
