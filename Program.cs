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

        private int buffer;
        private int source;

        private static void Main(string[] args)
        {
            int channels = 0;
            int sampleRate = 0;
            int bitsPerSample = 0;

            Program p = new Program();

            string filename = args[0];
            string ext = Path.GetExtension(filename);

            float[] stream = null!;
            if (ext == ".mp3" || ext == ".MP3")
            {
                stream = p.LoadMP3(filename, out channels, out sampleRate);
            }
            else if (ext == ".ogg" || ext == ".OGG")
            {
                stream = p.LoadOgg(filename, out channels, out sampleRate);
            }
            else if (ext == ".wav" || ext == ".WAV")
            {
                stream = p.LoadWav(filename, out channels, out bitsPerSample, out sampleRate);
            }
            else
            {
                Console.WriteLine("please select WAV, MP3 or Ogg file.");
                return;
            }

            Console.WriteLine($"File Name = {filename}");
            Console.WriteLine($"channels = {channels}");
            Console.WriteLine($"sample rate = {sampleRate}");
            Console.WriteLine($"bite per sample = {bitsPerSample}");

            p.InitAudio();
            p.Play(stream, sampleRate, channels);
            p.Wait();
            p.Clean();
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

        private void Clean()
        {
            AL.SourceStop(source);
            AL.DeleteSource(source);
            AL.DeleteBuffer(buffer);
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

        public float[] LoadWav(string filename, out int _channels, out int _bitsPerSample, out int _sampleRate)
        {
            using (BinaryReader reader = new BinaryReader(File.Open(filename, FileMode.Open)))
            {
                Stream baseStream = reader.BaseStream;

                string signature = new string(reader.ReadChars(4));
                Console.WriteLine(signature);
                if (signature != "RIFF")
                    throw new NotSupportedException("Not wav format.");

                int riff_chunck_size = reader.ReadInt32();

                string format = new string(reader.ReadChars(4));
                if (format != "WAVE")
                    throw new NotSupportedException("Not wav format.");

                string format_signature = "";
                byte[] data = null!;
                _channels = 0;
                _bitsPerSample = 0;
                _sampleRate = 0;

                while (baseStream.Position != baseStream.Length)
                {
                    format_signature = new string(reader.ReadChars(4));
                    if (format_signature == "fmt ")
                    {
                        reader.ReadInt32(); //chunk size
                        int fmt = reader.ReadInt16(); //audio format
                        int num_channels = reader.ReadInt16(); //number of channels
                        int sample_rate = reader.ReadInt32(); //sample rate
                        reader.ReadInt32(); //byte rate
                        reader.ReadInt16(); //block algin
                        int bits_per_sample = reader.ReadInt16(); //bits per sample
                        if (fmt != 0x01) {
                             throw new NotSupportedException("unsupport format. PCM format only");
                        }
                        _channels = num_channels;
                        _bitsPerSample = bits_per_sample;
                        _sampleRate = sample_rate;
                    }
                    else if (format_signature == "data")
                    {
                        int data_chunk_size = reader.ReadInt32();
                        data = reader.ReadBytes(data_chunk_size);
                    }
                    else
                    {
                        int chunk_size = reader.ReadInt32();
                        reader.ReadBytes(chunk_size);
                    }
                }
                if (_bitsPerSample == 8)
                {
                    return Convert8ToFloat32(data);
                }
                else if (_bitsPerSample == 16)
                {
                    return Convert16ToFloat32(data);
                }
                else
                {
                    return ConvertFloat32(data);
                }
            }
        }

        private float[] ConvertFloat32(byte[] data)
        {
            int data_index = 0;
            int result_size = 0;
            int result_index = 0;

            result_size = (int)((data.Length / 3));
            float[] result_data = new float[result_size];

            while (data_index < data.Length)
            {
                byte c1 = data[data_index++];
                byte c2 = data[data_index++];
                byte c3 = data[data_index++];

                int value = (int)(((c3 & 0x000000ff) << 16) | ((c2 & 0x000000ff) << 8) | (c1 & 0x000000ff));
                value = (int)((value & 0x00ffffff) | (((value & 0x800000) != 0) ? 0xff000000 : 0x00000000));

                result_data[result_index++] =  (float)value / 8388607.0f;
            }
            return result_data;
        }

        private float[] Convert16ToFloat32(byte[] data)
        {
            int data_index = 0;
            int result_size = 0;
            int result_index = 0;

            result_size = (int)((data.Length / 2));
            float[] result_data = new float[result_size];

            while (data_index < data.Length)
            {
                byte c1 = data[data_index++];
                byte c2 = data[data_index++];

                int value = (int)(((c2 & 0x000000ff) << 8) | (c1 & 0x000000ff));
                value = (int)((value & 0x0000ffff) | (((value & 0x8000) != 0) ? 0xffff0000 : 0x00000000));

                result_data[result_index++] =  (float)value / 65535.0f;
            }
            return result_data;
        }

        private float[] Convert8ToFloat32(byte[] data)
        {
            int data_index = 0;
            int result_size = 0;
            int result_index = 0;

            result_size = data.Length;
            float[] result_data = new float[result_size];

            while (data_index < data.Length)
            {
                byte c1 = data[data_index++];

                int value = (int)c1 - 128; 
                result_data[result_index++] =  (float)value / 128;
            }
            return result_data;
        }
        
        private void Play(float[] stream, int sampleRate, int channels)
        {
            buffer = AL.GenBuffer();
            source = AL.GenSource();

            ALFormat fmt = ALFormat.StereoFloat32Ext;
            if (channels == 1) {
                fmt = ALFormat.MonoFloat32Ext;
            }

            AL.BufferData(buffer, fmt, stream, sampleRate);
            AL.Source(source, ALSourcei.Buffer, buffer);
            AL.SourcePlay(source);
        }

        private void Wait()
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
