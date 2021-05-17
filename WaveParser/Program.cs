using System;
using Wave;

namespace WaveParser
{
    class Program
    {
        static void Main(string[] args)
        {
            // This wave is from https://freewavesamples.com/files/Ensoniq-ZR-76-01-Dope-77.wav
            string embeddedPath = "WaveParser.Data.Ensoniq-ZR-76-01-Dope-77.wav";
            string outputPath = "Ensoniq-ZR-76-01-Dope-77.wav";
            Embedded.WriteEmbeddedFile(embeddedPath, outputPath, "WaveParser");

            Wave.Wave w = new Wave.Wave();
            w.DeepParse(outputPath);
            w.OutputBasicInfo();
        }
    }
}
