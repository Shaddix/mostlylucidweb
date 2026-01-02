// Voice Form Audio Recorder
// Uses Web Audio API to capture microphone input and convert to WAV

window.voiceFormAudio = (function () {
    let mediaRecorder = null;
    let audioChunks = [];
    let audioContext = null;
    let dotNetRef = null;

    async function initialize(dotNetReference) {
        dotNetRef = dotNetReference;
        console.log('VoiceForm audio recorder initialized');
    }

    async function startRecording() {
        try {
            const stream = await navigator.mediaDevices.getUserMedia({ audio: true });

            // Create audio context for WAV conversion
            audioContext = new (window.AudioContext || window.webkitAudioContext)();

            audioChunks = [];

            // Use MediaRecorder with best available format
            const options = { mimeType: 'audio/webm;codecs=opus' };
            if (!MediaRecorder.isTypeSupported(options.mimeType)) {
                options.mimeType = 'audio/webm';
            }
            if (!MediaRecorder.isTypeSupported(options.mimeType)) {
                options.mimeType = '';
            }

            mediaRecorder = new MediaRecorder(stream, options);

            mediaRecorder.ondataavailable = (event) => {
                if (event.data.size > 0) {
                    audioChunks.push(event.data);
                }
            };

            mediaRecorder.onstop = async () => {
                // Stop all tracks
                stream.getTracks().forEach(track => track.stop());

                // Convert to WAV
                const audioBlob = new Blob(audioChunks, { type: 'audio/webm' });
                const wavBlob = await convertToWav(audioBlob);
                const wavBytes = await wavBlob.arrayBuffer();

                // Send to Blazor
                if (dotNetRef) {
                    const uint8Array = new Uint8Array(wavBytes);
                    await dotNetRef.invokeMethodAsync('OnRecordingComplete', Array.from(uint8Array));
                }
            };

            mediaRecorder.start(100); // Collect data every 100ms
            console.log('Recording started');

        } catch (error) {
            console.error('Error starting recording:', error);
            throw error;
        }
    }

    function stopRecording() {
        if (mediaRecorder && mediaRecorder.state !== 'inactive') {
            mediaRecorder.stop();
            console.log('Recording stopped');
        }
    }

    async function convertToWav(audioBlob) {
        // Decode the audio
        const arrayBuffer = await audioBlob.arrayBuffer();
        const audioBuffer = await audioContext.decodeAudioData(arrayBuffer);

        // Convert to mono 16kHz WAV (optimal for Whisper)
        const targetSampleRate = 16000;
        const numberOfChannels = 1;

        // Create offline context for resampling
        const offlineContext = new OfflineAudioContext(
            numberOfChannels,
            audioBuffer.duration * targetSampleRate,
            targetSampleRate
        );

        const source = offlineContext.createBufferSource();
        source.buffer = audioBuffer;
        source.connect(offlineContext.destination);
        source.start(0);

        const resampledBuffer = await offlineContext.startRendering();

        // Convert to WAV format
        return encodeWav(resampledBuffer);
    }

    function encodeWav(audioBuffer) {
        const numChannels = audioBuffer.numberOfChannels;
        const sampleRate = audioBuffer.sampleRate;
        const format = 1; // PCM
        const bitDepth = 16;

        const bytesPerSample = bitDepth / 8;
        const blockAlign = numChannels * bytesPerSample;

        const data = audioBuffer.getChannelData(0);
        const samples = data.length;
        const dataSize = samples * bytesPerSample;

        const buffer = new ArrayBuffer(44 + dataSize);
        const view = new DataView(buffer);

        // WAV header
        writeString(view, 0, 'RIFF');
        view.setUint32(4, 36 + dataSize, true);
        writeString(view, 8, 'WAVE');
        writeString(view, 12, 'fmt ');
        view.setUint32(16, 16, true); // fmt chunk size
        view.setUint16(20, format, true);
        view.setUint16(22, numChannels, true);
        view.setUint32(24, sampleRate, true);
        view.setUint32(28, sampleRate * blockAlign, true);
        view.setUint16(32, blockAlign, true);
        view.setUint16(34, bitDepth, true);
        writeString(view, 36, 'data');
        view.setUint32(40, dataSize, true);

        // Convert float samples to 16-bit PCM
        let offset = 44;
        for (let i = 0; i < samples; i++) {
            const sample = Math.max(-1, Math.min(1, data[i]));
            const intSample = sample < 0 ? sample * 0x8000 : sample * 0x7FFF;
            view.setInt16(offset, intSample, true);
            offset += 2;
        }

        return new Blob([buffer], { type: 'audio/wav' });
    }

    function writeString(view, offset, string) {
        for (let i = 0; i < string.length; i++) {
            view.setUint8(offset + i, string.charCodeAt(i));
        }
    }

    return {
        initialize,
        startRecording,
        stopRecording
    };
})();
