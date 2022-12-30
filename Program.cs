using OpenTK.Audio;
using OpenTK.Audio.OpenAL;
using NLayer;
using NVorbis;
using System.Runtime.InteropServices;

namespace OpenTKTest
{
    public class Program
    {
        private ALDevice device;
        private ALContext context;


        private static void Main(string[] args)
        {
            int channels;
            int sampleRate;

            Program p = new Program();

            string filename = args[0];
            string ext = Path.GetExtension(filename);

            float[] stream;
            if (ext == ".mp3" || ext == ".MP3")
            {
                stream = p.LoadMP3(filename, out channels, out sampleRate);
            }
            else if (ext == ".ogg" || ext == ".OGG")
            {
                stream = p.LoadOgg("test.ogg", out channels, out sampleRate);
            }
            else
            {
                Console.WriteLine("please select MP3 or Ogg file.");
                return;
            }

            Console.WriteLine($"File Name = {filename}");
            Console.WriteLine($"channels = {channels}");
            Console.WriteLine($"sample rate = {sampleRate}");

            p.InitAudio();
            int src = p.Play(stream, sampleRate);
            p.Wait(src);
        }

        public Program()
        {
        }

        private void InitAudio()
        {
            int[] attr = { 0 };
            string defname = ALC.GetString(ALDevice.Null, AlcGetString.DefaultDeviceSpecifier);
            device = ALC.OpenDevice(defname);
            context = ALC.CreateContext(device, attr);
            ALC.MakeContextCurrent(context);
            ALC.ProcessContext(context);
            AL.Listener(ALListenerf.Gain, 0.5f);
        }

        private float[] LoadOgg(string filename, out int _channels, out int _sampleRate)
        {
            using(VorbisReader vorbis = new VorbisReader(filename))
            {
                int channel = vorbis.Channels;
                int sampleRate = vorbis.SampleRate;
                long totalSamples = vorbis.TotalSamples;

                _channels = channel;
                _sampleRate = sampleRate;

                int buffer_size = (int)totalSamples * channel;
                float[] buffer = new float[buffer_size];
                vorbis.ReadSamples(buffer, 0, buffer_size);

                return buffer;
            }
        }

        private float[] LoadMP3(string filename, out int _channels, out int _sampleRate)
        {
            MpegFile _stream = new MpegFile(filename);
            if (_stream == null)
            {
                _channels = 0;
                _sampleRate = 0;
                return null!;
            }
            _sampleRate = _stream.SampleRate;
            _channels = _stream.Channels;

            int size = (int)_stream.Length;
            //byte[] data = new byte[size];
            float[] data = new float[size/sizeof(float)];

            _stream.ReadSamples(data, 0, size/sizeof(float));

            return data;
        }
        
        private int Play(float[] stream, int sampleRate)
        {
            int buffer = AL.GenBuffer();
            int source = AL.GenSource();

            AL.BufferData(buffer, ALFormat.StereoFloat32Ext, stream, sampleRate);
            //AL.BufferData(buffer, ALFormat.VorbisExt, stream, 44100);
            AL.Source(source, ALSourcei.Buffer, buffer);
            AL.SourcePlay(source);

            return source;
        }

        private void Wait(int source)
        {
            int state;
            do
            {
                Thread.Sleep(1000);
                Console.Write(".");
                AL.GetSource(source, ALGetSourcei.SourceState, out state);
            } while ((ALSourceState)state == ALSourceState.Playing);

            Console.WriteLine("finished playback");
        }
    }
}
