using System;
using System.Buffers.Binary;
using System.IO;

namespace PCMWAVE
{
    public enum AudioBitDepth
    {
        Bit8,
        Bit16,
        Bit24,
        Bit32,
        Float32,
        Float64
    }

    public static class WavImporter
    {
        /// <summary>
        /// Loads a WAV file and returns the audio data as normalized float samples [-1.0, 1.0].
        /// Stereo is automatically mixed down to mono.
        /// </summary>
        /// <param name="filePath">Path to the .wav file</param>
        /// <returns>Array of float samples at the file's original sample rate</returns>
        /// <exception cref="InvalidDataException">If the file is not a valid PCM WAV</exception>
        public static float[] Load(string filePath)
        {
            if (string.IsNullOrEmpty(filePath))
                throw new ArgumentNullException(nameof(filePath));

            using var stream = File.OpenRead(filePath);
            using var reader = new BinaryReader(stream);

            // Read RIFF header
            if (reader.ReadUInt32() != 0x46464952) // "RIFF"
                throw new InvalidDataException("Not a RIFF file.");

            reader.ReadUInt32(); // file size - 8 (we don't care)

            if (reader.ReadUInt32() != 0x45564157) // "WAVE"
                throw new InvalidDataException("Not a WAVE file.");

            // Find 'fmt ' chunk
            ushort numChannels = 0;
            uint sampleRate = 0;
            AudioBitDepth bitsPerSample = 0;
            uint dataSize = 0;
            ushort audioFormat = 0;

            while (stream.Position < stream.Length)
            {
                uint chunkId = reader.ReadUInt32();
                uint chunkSize = reader.ReadUInt32();

                if (chunkId == 0x20746D66) // "fmt "
                {
                    if (chunkSize < 16)
                        throw new InvalidDataException("Invalid fmt chunk size.");

                    audioFormat = reader.ReadUInt16();
                    if (audioFormat != 1 && audioFormat != 3) // 1 = PCM, 3 = IEEE float
                        throw new InvalidDataException($"Unsupported audio format: {audioFormat} (only PCM and float supported)");

                    numChannels = reader.ReadUInt16();
                    sampleRate = reader.ReadUInt32();
                    reader.ReadUInt32(); // byte rate (skip)
                    reader.ReadUInt16(); // block align (skip)
                    bitsPerSample = (AudioBitDepth)reader.ReadUInt16();

                    // Skip any extra fmt bytes
                    if (chunkSize > 16)
                        reader.ReadBytes((int)(chunkSize - 16));
                }
                else if (chunkId == 0x61746164) // "data"
                {
                    dataSize = chunkSize;
                    break; // We only need the first data chunk for now
                }
                else
                {
                    // Skip unknown chunk
                    reader.ReadBytes((int)chunkSize);
                }
            }

            if (dataSize == 0)
                throw new InvalidDataException("No data chunk found in WAV file.");

            if (numChannels == 0 || bitsPerSample == 0)
                throw new InvalidDataException("Incomplete fmt chunk.");

            // Read raw data
            byte[] rawData = reader.ReadBytes((int)dataSize);

            // Convert to float samples [-1.0, 1.0]
            int sampleCount = (int)dataSize / ((int)bitsPerSample / 8 * numChannels);
            float[] samples = new float[sampleCount];

            int srcIndex = 0;

            for (int i = 0; i < sampleCount; i++)
            {
                float monoSample = 0f;

                for (int ch = 0; ch < numChannels; ch++)
                {
                    float sample = ReadSample(rawData, ref srcIndex, bitsPerSample, audioFormat);
                    monoSample += sample;
                }

                samples[i] = monoSample / numChannels; // average channels → mono
            }

            Console.WriteLine($"Loaded WAV: {sampleCount} samples @ {sampleRate} Hz, {bitsPerSample}-bit, {numChannels} channels");

            return samples;
        }

        private static float ReadSample(byte[] data, ref int index, AudioBitDepth bitsPerSample, ushort audioFormat)
        {
            float value;

            switch (bitsPerSample)
            {
                case AudioBitDepth.Bit8: // unsigned 8-bit
                    value = (data[index++] - 128) / 128f;
                    break;

                case AudioBitDepth.Bit16: // signed 16-bit
                    short s16 = BinaryPrimitives.ReadInt16LittleEndian(data.AsSpan(index));
                    index += 2;
                    value = s16 / 32768f;
                    break;

                case AudioBitDepth.Bit24: // signed 24-bit
                    int s24 = data[index++] | (data[index++] << 8) | (data[index++] << 16);
                    if ((s24 & 0x800000) != 0) s24 |= unchecked((int)0xFF000000); // sign extend
                    value = s24 / 8388608f;
                    break;

                case AudioBitDepth.Bit32: // signed 32-bit integer or IEEE 32-bit float
                    if (audioFormat == 3)
                    {
                        value = BinaryPrimitives.ReadSingleLittleEndian(data.AsSpan(index));
                        index += 4;
                    }
                    else
                    {
                        int s32 = BinaryPrimitives.ReadInt32LittleEndian(data.AsSpan(index));
                        index += 4;
                        value = s32 / 2147483648f;
                    }
                    break;

                default:
                    throw new NotSupportedException($"Unsupported bits per sample: {bitsPerSample}");
            }

            return Math.Clamp(value, -1.0f, 1.0f);
        }
    }
}