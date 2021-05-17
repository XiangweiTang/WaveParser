using System;
using System.Collections.Generic;
using System.IO;
using System.Text;

namespace Wave
{
    public class Wave
    {
        #region Basic wave parameters
        /// <summary>
        /// Type id defines which type of the wave is.
        /// </summary>
        public short TypeId { get; set; } = 0;
        /// <summary>
        /// Type string is the wave type string, or name by human.
        /// </summary>
        public string TypeString { get; set; } = "";
        /// <summary>
        /// How many channels are there. Typically mono or parallel.
        /// Theoratically there are 7.1 Dubai audios...
        /// </summary>
        public short NumChannels { get; set; } = 0;
        /// <summary>
        /// The sample rate, how many samples per seconds.
        /// </summary>
        public int SampleRate { get; set; } = 0;
        /// <summary>
        /// The byte rate, how many bytes per second for this audio.
        /// This number has already have "Sample rate"/"Number of channels"/"Audio bits" all together.
        /// The number can be read in file info of OS.
        /// e.g. you may found something like "256kbps", which is 32,000 KB per seconds(16K sample rate audio*16bits).
        /// </summary>
        public int ByteRate { get; set; } = 0;
        /// <summary>
        /// How many bytes does a single group(all channels) of samples contain.
        /// </summary>
        public short BlockAlign { get; set; } = 0;
        /// <summary>
        /// The precision of the audio. Typically 16bits or 65536, alaw and mulaw(ulaw) are 8K sample rate*8 bits(256).
        /// </summary>
        public short BitsPerSample { get; set; } = 0;
        /// <summary>
        /// The lengthe of the audio in seconds. Can be directly calculated by [Length of data chunk]/[Byte rate].
        /// </summary>
        public double AudioTime { get; set; } = 0;
        /// <summary>
        /// The binary of the data chunk.
        /// </summary>
        public byte[] DataBytes
        {
            get
            {
                // Only in deep parse mod, the data bytes will be availible.
                Sanity.Requires(IsDeep, "Only deep parse will generate the data bytes.");
                return _DataBytes;
            }
        }
        private byte[] _DataBytes = new byte[0];
        /// <summary>
        /// RMS is root of mean square. It is typically used to assess the volumn(energy) of the sound.
        /// RMS = Sqrt(( x_0^2 + x_1^2 + ... + x_(n-1)^2 ) / n )
        /// </summary>
        public double RMS
        {
            get
            {
                // Only supported type can do the following calculations.
                Sanity.Requires(IsSupportedWaveType, $"Current wave type [{TypeId}-{TypeString}] is not supported.");
                // Only in deep parse mod, the data bytes will be read.
                Sanity.Requires(IsDeep, "Only deep parse will generate the RMS.");
                if (_RMS == -1 || IsDataChanged)
                {
                    // Calculate RMS will cost some time.
                    // In order to speed up, once the value is calculated and data is not changed, no calculate is required.
                    // Otherwise, calculate.
                    _RMS = CalculateRMS();
                    IsDataChanged = false;
                }
                return _RMS;
            }
        }
        private double _RMS = -1;
        #endregion

        #region Other Internal/Exteranl wave parameters.

        /// <summary>
        /// The list of the chunks.
        /// </summary>
        public List<WaveChunk> ChunkList { get; set; } = new List<WaveChunk>();
        /// <summary>
        /// The format chunk.
        /// </summary>
        public WaveChunk FormatChunk { get; set; }
        /// <summary>
        /// The data chunk.
        /// </summary>
        public WaveChunk DataChunk { get; set; }
        /// <summary>
        /// Whether the wave type is supported in this code.
        /// </summary>
        public bool IsSupportedWaveType { get; private set; } = true;

        /// <summary>
        /// Whether the audio is deep parsed(read every bytes of the data chunk).
        /// </summary>
        private bool IsDeep = false;
        /// <summary>
        /// Whether the audio's data chunk has been reset or not.
        /// </summary>
        private bool IsDataChanged = false;
        /// <summary>
        /// Most of the time, read the four bytes of text info into this array.
        /// (In RIFF file, every chunk has a 4-bit long header for the chunk name.)
        /// </summary>
        private byte[] ChunkNameBytes = new byte[4];
        /// <summary>
        /// Read the 32 bit integer into this array.
        /// (In RIFF file, every chunk has a 4-bit long header for the chunk size.)
        /// </summary>
        private byte[] Int32Bytes = new byte[4];
        /// <summary>
        /// Read the 16 bits integer into this array.
        /// </summary>
        private byte[] Int16Bytes = new byte[2];
        #endregion

        #region Entrance
        /// <summary>
        /// Deep parse the audio from a certain path.
        /// </summary>
        /// <param name="filePath">The path of the audio file.</param>
        public void DeepParse(string filePath)
        {
            Sanity.Requires(File.Exists(filePath), $"File not exist: {filePath}.");
            using(FileStream st=new FileStream(filePath, FileMode.Open, FileAccess.Read))
            {
                // Call the deep parse stream to keep the core algorithm identical.
                DeepParse(st);
            }
        }
        /// <summary>
        /// Deep parse the audio from a certain stream.
        /// </summary>
        /// <param name="st">The stream of the audio file.</param>
        public void DeepParse(Stream st)
        {            
            // Call the shallow parse stream to keep the core algorithm identical.
            ShallowParse(st);

            // Then set the following to make it deep parse.
            _DataBytes = new byte[DataChunk.ChunkLength];
            st.Seek(DataChunk.ChunkOffset + 8, SeekOrigin.Begin);
            st.Read(_DataBytes, 0, DataChunk.ChunkLength);
            IsDeep = true;
        }
        /// <summary>
        /// Shallow parse the audio from a certain path.
        /// </summary>
        /// <param name="filePath">The path of the audio file.</param>
        public void ShallowParse(string filePath)
        {
            Sanity.Requires(File.Exists(filePath), $"File not exist: {filePath}.");
            using(FileStream st=new FileStream(filePath, FileMode.Open, FileAccess.Read))
            {
                // Call the shallow parse stream to keep the core algorithm identical.
                ShallowParse(st);
            }
        }
        /// <summary>
        /// Shallow parse the audio from a certain stream.
        /// </summary>
        /// <param name="st">The stream of the audio file.</param>
        public void ShallowParse(Stream st)
        {
            IsDeep = false;
            IsDataChanged = true;
            // Everything starts with parse RIFF.
            ParseRiff(st);
        }
        #endregion

        #region Parse the file.
        private void ParseRiff(Stream st)
        {          
            // The first three blocks do not follow the rest.
            // (WAVE is not followed by size, and there is no hard definition of "WAVEfmt " are always together)

            // The smallest wave is 44 bytes.
            Sanity.Requires(st.Length >= 44, $"The stream length {st.Length} is too short, should be at least 44.");
            // For safety, 2G file will not be supported.
            Sanity.Requires(st.Length <= int.MaxValue, $"The stream length {st.Length} is too long, at most 2G.");

            // 0-3: RIFF.
            st.Read(ChunkNameBytes, 0, 4);
            Sanity.Requires(GetName() == "RIFF", "Invalid header, should be RIFF.");
            
            // 4-7: int 32 for RIFF size.
            st.Read(Int32Bytes, 0, 4);
            Sanity.Requires(GetInt32() + 8 == st.Length, "Stream length mismatch, in RIFF chunk.");

            // 8-11: WAVE.
            st.Read(ChunkNameBytes, 0, 4);
            Sanity.Requires(GetName() == "WAVE", "Invalid header, should be WAVE");

            // Clear the format chunk and data chunk.
            FormatChunk = InitEmptyChunk();
            DataChunk = InitEmptyChunk();            

            // Recursively parse the remaining chunks.
            ParseChunks(st);

            // Validate the rest values.
            PostCheck(st);
        }
        private void ParseChunks(Stream st)
        {
            // When reaches the end, stop.
            if (st.Position == st.Length)
                return;

            // If it is a valid chunk, it has to be at least 8
            Sanity.Requires(st.Position + 8 <= st.Length, $"Stream length mismatch, at position {st.Position}.");

            // Offset is the current position.
            int offset = (int)st.Position;

            // The first four bytes(of the 8 bytes) is the name.
            st.Read(ChunkNameBytes, 0, 4);
            string name = GetName();

            // The next four bytes(of the 8 bytes) is the chunk siz/length.
            st.Read(Int32Bytes, 0, 4);
            int length = GetInt32();

            // The chunk length should not exceed the end of the file.
            Sanity.Requires(st.Position + length <= st.Length, $"Stream length mismatch, at position {st.Position}, in chunk {name}.");
            // Move forward.
            st.Seek(length, SeekOrigin.Current);

            // Set the chunk.
            WaveChunk chunk = new WaveChunk
            {
                ChunkName = name,
                ChunkLength = length,
                ChunkOffset = offset
            };

            // Format chunk is special.
            if (chunk.ChunkName == "fmt ")
            {
                // There should be only 1 format chunk.
                Sanity.Requires(FormatChunk.ChunkName == null, $"Format error, at most 1 format chunk.");
                // The smallest format chunk for PCM is 16.
                Sanity.Requires(chunk.ChunkLength >= 16, $"Format error, format chunk length {FormatChunk.ChunkLength}, should be at least 16.");
                FormatChunk = chunk;
            }

            // Data chunk is special.
            if (chunk.ChunkName == "data")
            {
                // There should be only 1 data chunk.
                // NOTE: I'M NOT SURE ABOUT THIS, THERE IS NOWHERE SAY ONLY 1 DATA CHUNK IS ALLOWED.
                // JUST FOR SIMPLE HERE.
                Sanity.Requires(DataChunk.ChunkName == null, $"Format error, at most 1 data chunk.");
                DataChunk = chunk;
            }

            // Add this chunk into the list.
            ChunkList.Add(chunk);


            // Parse the next chunk recursively.
            ParseChunks(st);
        }
        private void PostCheck(Stream st)
        {
            // The format chunk and data chunk have to exist.
            Sanity.Requires(FormatChunk.ChunkName != null, "Format error, missing format chunk.");
            Sanity.Requires(DataChunk.ChunkName != null, "Format error, missing data chunk.");

            // Go to the format chunk.
            st.Seek(FormatChunk.ChunkOffset + 8, SeekOrigin.Begin);

            // First two bytes for wave type.
            st.Read(Int16Bytes, 0, 2);
            TypeId = GetInt16();

            // Following two bytes for the number of channels.
            st.Read(Int16Bytes, 0, 2);
            NumChannels = GetInt16();
            Sanity.Requires(NumChannels != 0, "Channel cannot be zero.");

            // Following four bytes for the sample rate.
            st.Read(Int32Bytes, 0, 4);
            SampleRate = GetInt32();
            Sanity.Requires(SampleRate != 0, "Sample rate cannot be zeror.");

            // Following four bytes for the byte rate(bytes per second).
            // Audio length(seconds) can be calculated by this value and the length of data bytes directly.
            st.Read(Int32Bytes, 0, 4);
            ByteRate = GetInt32();
            Sanity.Requires(ByteRate != 0, "Byte rate cannot be zero.");
            AudioTime = (double)DataChunk.ChunkLength / ByteRate;

            // Block align, how many bytes for every "block", e.g. a single sample from all channels.
            st.Read(Int16Bytes, 0, 2);
            BlockAlign = GetInt16();
            Sanity.Requires(BlockAlign != 0, "Block align cannot be zero.");

            // Bits per sample, the audio precision, e.g. 8 bit or 16 bit.
            st.Read(Int16Bytes, 0, 2);
            BitsPerSample = GetInt16();
            Sanity.Requires(BitsPerSample != 0, "Bits per sample cannot be zero.");

            // The values of all the wave format are redundant, two extra equations should be followed.
            Sanity.Requires(ByteRate == BitsPerSample / 8 * SampleRate * NumChannels, "Error in byte rate equation.");
            Sanity.Requires(BlockAlign == BitsPerSample / 8 * NumChannels, "Error in block align equation.");

            // In practical, invalid format is seldom seen.
            // So just let it be.

            // Set the wave type string.
            SetWaveType();
        }
        private void SetWaveType()
        {
            // Pure PCM, alaw, mulaw(or ulaw) are definitely supported.
            // For the others, maybe later.
            switch (TypeId)
            {
                case 1:
                    // Pure PCM.
                    TypeString = "PCM";
                    return;
                case 2:
                    // ADPCM has some special ways of encoding the wave.
                    TypeString = "ADPCM";
                    IsSupportedWaveType = false;
                    return;
                case 3:
                    // Never saw this in practical.
                    TypeString = "IEEE";
                    IsSupportedWaveType = false;
                    return;
                case 6:
                    // ALaw and muLaw(uLaw) are generally the same with pure PCM.                    
                    TypeString = "ALAW";
                    return;
                case 7:
                    TypeString = "MULAW";
                    return;
                default:
                    // There are other types. One of them is called Siren Wave.
                    // Very few documents can be found on internet, Sox and Ffmpeg cannot deal with this.
                    // A special decoder from MS can process it.
                    TypeString = "NA";
                    IsSupportedWaveType = false;
                    return;
            }
        }
        private short GetInt16()
        {
            return BitConverter.ToInt16(Int16Bytes, 0);
        }
        private int GetInt32()
        {
            return BitConverter.ToInt32(Int32Bytes, 0);
        }
        private string GetName()
        {
            return Encoding.ASCII.GetString(ChunkNameBytes);
        }
        private WaveChunk InitEmptyChunk()
        {
            return new WaveChunk
            {
                ChunkName = null,
                ChunkOffset = -1,
                ChunkLength = -1,
            };
        }
        #endregion

        #region Data chunk calculation.
        private double CalculateRMS()
        {
            long sqrSum = 0;
            int n = 0;
            Func<int, int> readToInt = ReadDataToIntFunction();
            foreach(var buffer in ReadDataBytesToBuffer())
            {
                for (int i = buffer.offset; i < buffer.length; i += BitsPerSample)
                {
                    int v = readToInt(i);
                    sqrSum += v * v;
                    n++;
                }
            }
            return Math.Sqrt((double)sqrSum / n) / GetDivisor();
        }

        private Func<int,int> ReadDataToIntFunction()
        {
            switch (BitsPerSample)
            {
                case 1:
                    return x => _DataBytes[x];
                case 2:
                    return x => BitConverter.ToInt16(_DataBytes, x);
                case 4:
                    return x => BitConverter.ToInt32(_DataBytes, x);
                default:
                    throw new WaveException("Unsupported audio bits.");
            }
        }

        private int GetDivisor()
        {
            switch (BitsPerSample)
            {
                case 1:
                    return 255;
                case 2:
                    return 65535;
                case 4:
                    return int.MaxValue;
                default:
                    throw new WaveException("Unsupported audio bits.");
            }
        }

        private IEnumerable<(int offset, int length)> ReadDataBytesToBuffer()
        {
            int bufferSize = 1_024 * BitsPerSample / 8;
            for(int offset = 0; offset < _DataBytes.Length; offset += bufferSize)
            {
                int length = offset + bufferSize <= _DataBytes.Length
                    ? bufferSize
                    : _DataBytes.Length - offset;
                yield return (offset, length);
            }
        }
        #endregion

        #region Other functions
        public void OutputBasicInfo()
        {
            Console.WriteLine($"This wave type is:\t{TypeString}.");
            Console.WriteLine($"Channel number:\t{NumChannels}");
            Console.WriteLine($"Sample rate:\t{SampleRate}(samples/second)");
            Console.WriteLine($"Byte rate:\t{ByteRate}(bytes/second)");
            Console.WriteLine($"Audio precision:\t{BitsPerSample}(bits)");
            Console.WriteLine($"Audio time:\t{AudioTime:0.000}(seconds)");
        }
        #endregion
    }
    /// <summary>
    /// Definition of wave chunk.
    /// </summary>
    public struct WaveChunk
    {
        /// <summary>
        /// The name of the chunk.
        /// </summary>
        public string ChunkName { get; set; }
        /// <summary>
        /// The offset of the chunk in the wave file.
        /// </summary>
        public int ChunkOffset { get; set; }
        /// <summary>
        /// The length of the chunk.
        /// </summary>
        public int ChunkLength { get; set; }
    }
}
