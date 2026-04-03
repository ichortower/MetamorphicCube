using Microsoft.Xna.Framework.Audio;
using NVorbis;
using StardewValley;
using System.IO;
using System.Linq;
using System.Text;
using ichortower.TowerCore;

namespace ichortower.MetamorphicCube;

internal class Ogg
{

    internal static CueDefinition CueFromStream(Stream stream,
            string cueId, string category, bool looped)
    {
        return CueFromStreams(new Stream[]{stream}, cueId, category, looped);
    }

    internal static CueDefinition CueFromStreams(Stream[] streams,
            string cueId, string category, bool looped)
    {
        SoundEffect[] sounds = streams.Select(stream => StreamToXact(stream)).ToArray();
        return new CueDefinition(cueId, sounds,
                Game1.audioEngine.GetCategoryIndex(category), looped);
    }

    internal static SoundEffect StreamToXact(Stream stream)
    {
        NVorbis.VorbisReader decoder = new(stream, true);
        int channels = decoder.Channels;
        int sampleRate = decoder.SampleRate;
        float[] buffer = new float[channels * sampleRate * 1]; // 1s per read

        MemoryStream waveStream = new();
        BinaryWriter writer = new(waveStream);
        WriteHeader(writer, channels, sampleRate);
        int count = 0;
        // convert float samples to short when writing. this cuts memory use in half and
        // is probably about a wash on load time. i haven't profiled it.
        while ((count = decoder.ReadSamples(buffer, 0, buffer.Length)) > 0) {
            for (int i = 0; i < count; ++i) {
                writer.Write((short)(buffer[i] * 32767));
            }
        }
        WriteSizes(writer, waveStream.Length);
        stream.Position = 0;
        return SoundEffect.FromStream(waveStream);
    }

    const string BLANK_HEADER = "RIFF\0\0\0\0WAVEfmt ";
    const string BLANK_DATA = "data\0\0\0\0";

    internal static void WriteHeader(BinaryWriter writer, int channels, int sampleRate)
    {
        // total header length: 44 bytes
        writer.Write(Encoding.UTF8.GetBytes(BLANK_HEADER));
        writer.Write((int)16);
        writer.Write((short)1);
        writer.Write((short)channels);
        writer.Write((int)sampleRate);
        var blockSize = channels * sizeof(short);
        writer.Write((int)(sampleRate * blockSize));
        writer.Write((short)blockSize);
        writer.Write((short)(sizeof(short) * 8));
        writer.Write(Encoding.UTF8.GetBytes(BLANK_DATA));
    }

    internal static void WriteSizes(BinaryWriter writer, long length)
    {
        writer.Seek(4, SeekOrigin.Begin);
        writer.Write((uint)(length - 8));
        writer.Seek(40, SeekOrigin.Begin);
        writer.Write((uint)(length - 44));
    }
}
