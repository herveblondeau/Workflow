using Main;

var transcriptor = new Transcriptor();
await transcriptor.Process(TranscriptionSource.MicrophoneOnly, sourceLanguage: "fr", cleanUp: true, concise: true, targetLanguage: "anglais", additionalInstructions: [
    "le texte est destiné à une formation en ligne et le ton doit donc être relativement formel",
    "n'hésite pas à reformuler et/ou réordonner les phrases pour qu'elles soient plus claires et concises",
]);
// await transcriptor.Process(TranscriptionSource.MicrophoneOnly, sourceLanguage: "fr", cleanUp: true, concise: false, targetLanguage: "japonais", additionalInstructions: null);
