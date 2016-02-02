﻿// <copyright file="ConversionJob_FFMPEG.cs" company="AAllard">License: http://www.gnu.org/licenses/gpl.html GPL version 3.</copyright>

namespace FileConverter.ConversionJobs
{
    using System;
    using System.Diagnostics;
    using System.Globalization;
    using System.IO;
    using System.Text.RegularExpressions;

    using FileConverter.Controls;

    public partial class ConversionJob_FFMPEG : ConversionJob
    {
        private readonly Regex durationRegex = new Regex(@"Duration:\s*([0-9][0-9]):([0-9][0-9]):([0-9][0-9])\.([0-9][0-9]),.*bitrate:\s*([0-9]+) kb\/s");
        private readonly Regex progressRegex = new Regex(@"size=\s*([0-9]+)kB\s+time=([0-9][0-9]):([0-9][0-9]):([0-9][0-9]).([0-9][0-9])\s+bitrate=\s*([0-9]+.[0-9])kbits\/s");

        private TimeSpan fileDuration;
        private TimeSpan actualConvertedDuration;

        private ProcessStartInfo ffmpegProcessStartInfo;

        public ConversionJob_FFMPEG() : base()
        {
        }

        public ConversionJob_FFMPEG(ConversionPreset conversionPreset) : base(conversionPreset)
        {
        }

        protected virtual string FfmpegPath
        {
            get
            {
                string applicationDirectory = System.IO.Path.GetDirectoryName(System.Reflection.Assembly.GetExecutingAssembly().Location);
                return System.IO.Path.Combine(applicationDirectory, "ffmpeg.exe");
            }
        }

        protected override void Initialize()
        {
            base.Initialize();

            if (this.ConversionPreset == null)
            {
                throw new Exception("The conversion preset must be valid.");
            }

            this.ffmpegProcessStartInfo = null;

            string ffmpegPath = this.FfmpegPath;
            if (!System.IO.File.Exists(ffmpegPath))
            {
                this.ConversionFailed("Can't find ffmpeg executable. You should try to reinstall the application.");
                Diagnostics.Debug.Log("Can't find ffmpeg executable ({0}). Try to reinstall the application.", ffmpegPath);
                return;
            }

            this.ffmpegProcessStartInfo = new ProcessStartInfo(ffmpegPath);

            this.ffmpegProcessStartInfo.CreateNoWindow = true;
            this.ffmpegProcessStartInfo.UseShellExecute = false;
            this.ffmpegProcessStartInfo.RedirectStandardOutput = true;
            this.ffmpegProcessStartInfo.RedirectStandardError = true;

            string arguments = string.Empty;
            switch (this.ConversionPreset.OutputType)
            {
                case OutputType.Aac:
                    {
                        // https://trac.ffmpeg.org/wiki/Encode/AAC
                        int audioEncodingBitrate = this.ConversionPreset.GetSettingsValue<int>(ConversionPreset.ConversionSettingKeys.AudioBitrate);
                        string encoderArgs = string.Format("-c:a aac -q:a {0}", this.AACBitrateToQualityIndex(audioEncodingBitrate));
                        arguments = string.Format("-n -stats -i \"{0}\" {2} \"{1}\"", this.InputFilePath, this.OutputFilePath, encoderArgs);
                    }

                    break;

                case OutputType.Avi:
                    {
                        // https://trac.ffmpeg.org/wiki/Encode/MPEG-4
                        int videoEncodingQuality = this.ConversionPreset.GetSettingsValue<int>(ConversionPreset.ConversionSettingKeys.VideoQuality);
                        int audioEncodingBitrate = this.ConversionPreset.GetSettingsValue<int>(ConversionPreset.ConversionSettingKeys.AudioBitrate);

                        float scaleFactor = this.ConversionPreset.GetSettingsValue<float>(ConversionPreset.ConversionSettingKeys.VideoScale);
                        string scaleArgs = string.Empty;
                        if (Math.Abs(scaleFactor - 1f) >= 0.005f)
                        {
                            scaleArgs = string.Format("-vf scale=iw*{0}:ih*{0}", scaleFactor.ToString("#.##", CultureInfo.InvariantCulture));
                        }

                        string encoderArgs = string.Format("-c:v mpeg4 -vtag xvid -qscale:v {0} -c:a libmp3lame -qscale:a {1} {2}", this.MPEG4QualityToQualityIndex(videoEncodingQuality), this.MP3VBRBitrateToQualityIndex(audioEncodingBitrate), scaleArgs);
                        arguments = string.Format("-n -stats -i \"{0}\" {2} \"{1}\"", this.InputFilePath, this.OutputFilePath, encoderArgs);
                    }

                    break;

                case OutputType.Flac:
                    {
                        // http://taer-naguur.blogspot.fr/2013/11/flac-audio-encoding-with-ffmpeg.html
                        string encoderArgs = string.Format("-compression_level 12");
                        arguments = string.Format("-n -stats -i \"{0}\" {2} \"{1}\"", this.InputFilePath, this.OutputFilePath, encoderArgs);
                    }

                    break;

                case OutputType.Ico:
                    {
                        string encoderArgs = string.Empty;
                        arguments = string.Format("-n -stats -i \"{0}\" {2} \"{1}\"", this.InputFilePath, this.OutputFilePath, encoderArgs);
                    }

                    break;

                case OutputType.Jpg:
                    {
                        int encodingQuality = this.ConversionPreset.GetSettingsValue<int>(ConversionPreset.ConversionSettingKeys.ImageQuality);

                        float scaleFactor = this.ConversionPreset.GetSettingsValue<float>(ConversionPreset.ConversionSettingKeys.ImageScale);
                        string scaleArgs = string.Empty;
                        if (Math.Abs(scaleFactor - 1f) >= 0.005f)
                        {
                            scaleArgs = string.Format("-vf scale=iw*{0}:ih*{0}", scaleFactor.ToString("#.##", CultureInfo.InvariantCulture));
                        }

                        string encoderArgs = string.Format("-q:v {0} {1}", this.JPGQualityToQualityIndex(encodingQuality), scaleArgs);

                        arguments = string.Format("-n -stats -i \"{0}\" {2} \"{1}\"", this.InputFilePath, this.OutputFilePath, encoderArgs);
                    }

                    break;

                case OutputType.Mp3:
                    {
                        string encoderArgs = string.Empty;
                        EncodingMode encodingMode = this.ConversionPreset.GetSettingsValue<EncodingMode>(ConversionPreset.ConversionSettingKeys.AudioEncodingMode);
                        int encodingQuality = this.ConversionPreset.GetSettingsValue<int>(ConversionPreset.ConversionSettingKeys.AudioBitrate);
                        switch (encodingMode)
                        {
                            case EncodingMode.Mp3VBR:
                                encoderArgs = string.Format("-codec:a libmp3lame -q:a {0}", this.MP3VBRBitrateToQualityIndex(encodingQuality));
                                break;

                            case EncodingMode.Mp3CBR:
                                encoderArgs = string.Format("-codec:a libmp3lame -b:a {0}k", encodingQuality);
                                break;

                            default:
                                break;
                        }

                        arguments = string.Format("-n -stats -i \"{0}\" {2} \"{1}\"", this.InputFilePath, this.OutputFilePath, encoderArgs);
                    }

                break;

                case OutputType.Mkv:
                    {
                        // https://trac.ffmpeg.org/wiki/Encode/H.264
                        // https://trac.ffmpeg.org/wiki/Encode/AAC
                        int videoEncodingQuality = this.ConversionPreset.GetSettingsValue<int>(ConversionPreset.ConversionSettingKeys.VideoQuality);
                        string videoEncodingSpeed = this.ConversionPreset.GetSettingsValue<string>(ConversionPreset.ConversionSettingKeys.VideoEncodingSpeed);
                        int audioEncodingBitrate = this.ConversionPreset.GetSettingsValue<int>(ConversionPreset.ConversionSettingKeys.AudioBitrate);

                        float scaleFactor = this.ConversionPreset.GetSettingsValue<float>(ConversionPreset.ConversionSettingKeys.VideoScale);
                        string scaleArgs = string.Empty;
                        if (Math.Abs(scaleFactor - 1f) >= 0.005f)
                        {
                            scaleArgs = string.Format("-vf scale=iw*{0}:ih*{0}", scaleFactor.ToString("#.##", CultureInfo.InvariantCulture));
                        }

                        string encoderArgs = string.Format("-c:v libx264 -preset {0} -crf {1} -c:a aac -q:a {2} {3}", this.H264EncodingSpeedToPreset(videoEncodingSpeed), this.H264QualityToCRF(videoEncodingQuality), this.AACBitrateToQualityIndex(audioEncodingBitrate), scaleArgs);
                        arguments = string.Format("-n -stats -i \"{0}\" {2} \"{1}\"", this.InputFilePath, this.OutputFilePath, encoderArgs);
                    }

                    break;

                case OutputType.Ogg:
                    {
                        int encodingQuality = this.ConversionPreset.GetSettingsValue<int>(ConversionPreset.ConversionSettingKeys.AudioBitrate);
                        string encoderArgs = string.Format("-codec:a libvorbis -qscale:a {0}", this.OGGVBRBitrateToQualityIndex(encodingQuality));
                        arguments = string.Format("-n -stats -i \"{0}\" {2} \"{1}\"", this.InputFilePath, this.OutputFilePath, encoderArgs);
                    }

                    break;

                case OutputType.Png:
                    {
                        float scaleFactor = this.ConversionPreset.GetSettingsValue<float>(ConversionPreset.ConversionSettingKeys.ImageScale);
                        string scaleArgs = string.Empty;
                        if (Math.Abs(scaleFactor - 1f) >= 0.005f)
                        {
                            scaleArgs = string.Format("-vf scale=iw*{0}:ih*{0}", scaleFactor.ToString("#.##", CultureInfo.InvariantCulture));
                        }

                        // http://www.howtogeek.com/203979/is-the-png-format-lossless-since-it-has-a-compression-parameter/
                        string encoderArgs = string.Format("-compression_level 100 {0}", scaleArgs);

                        arguments = string.Format("-n -stats -i \"{0}\" {2} \"{1}\"", this.InputFilePath, this.OutputFilePath, encoderArgs);
                    }

                    break;

                case OutputType.Wav:
                    {
                        EncodingMode encodingMode = this.ConversionPreset.GetSettingsValue<EncodingMode>(ConversionPreset.ConversionSettingKeys.AudioEncodingMode);
                        string encoderArgs = string.Format("-acodec {0}", this.WAVEncodingToCodecArgument(encodingMode));
                        arguments = string.Format("-n -stats -i \"{0}\" {2} \"{1}\"", this.InputFilePath, this.OutputFilePath, encoderArgs);
                    }

                    break;

                default:
                    throw new NotImplementedException("Converter not implemented for output file type " + this.ConversionPreset.OutputType);
            }

            if (string.IsNullOrEmpty(arguments))
            {
                throw new Exception("Invalid ffmpeg process arguments.");
            }

            this.ffmpegProcessStartInfo.Arguments = arguments;
        }

        protected override void Convert()
        {
            if (this.ConversionPreset == null)
            {
                throw new Exception("The conversion preset must be valid.");
            }

            this.UserState = "Conversion";

            Diagnostics.Debug.Log("Execute command: {0} {1}.", this.ffmpegProcessStartInfo.FileName, this.ffmpegProcessStartInfo.Arguments);
            Diagnostics.Debug.Log(string.Empty);

            try
            {
                using (Process exeProcess = Process.Start(this.ffmpegProcessStartInfo))
                {
                    using (StreamReader reader = exeProcess.StandardError)
                    {
                        while (!reader.EndOfStream)
                        {
                            string result = reader.ReadLine();

                            this.ParseFFMPEGOutput(result);

                            Diagnostics.Debug.Log("ffmpeg output: {0}", result);
                        }
                    }

                    exeProcess.WaitForExit();
                }
            }
            catch
            {
                this.ConversionFailed("Failed to launch FFMPEG process.");
                throw;
            }

            Diagnostics.Debug.Log(string.Empty);
        }

        private void ParseFFMPEGOutput(string input)
        {
            Match match = this.durationRegex.Match(input);
            if (match.Success && match.Groups.Count >= 6)
            {
                int hours = int.Parse(match.Groups[1].Value);
                int minutes = int.Parse(match.Groups[2].Value);
                int seconds = int.Parse(match.Groups[3].Value);
                int milliseconds = int.Parse(match.Groups[4].Value);
                float bitrate = float.Parse(match.Groups[5].Value);
                this.fileDuration = new TimeSpan(0, hours, minutes, seconds, milliseconds);
                return;
            }

            match = this.progressRegex.Match(input);
            if (match.Success && match.Groups.Count >= 7)
            {
                int size = int.Parse(match.Groups[1].Value);
                int hours = int.Parse(match.Groups[2].Value);
                int minutes = int.Parse(match.Groups[3].Value);
                int seconds = int.Parse(match.Groups[4].Value);
                int milliseconds = int.Parse(match.Groups[5].Value);
                float bitrate = 0f;
                float.TryParse(match.Groups[6].Value, out bitrate);

                this.actualConvertedDuration = new TimeSpan(0, hours, minutes, seconds, milliseconds);

                this.Progress = this.actualConvertedDuration.Ticks / (float)this.fileDuration.Ticks;
                return;
            }

            if (input.Contains("Exiting.") || input.Contains("Error") || input.Contains("Unsupported dimensions") || input.Contains("No such file or directory"))
            {
                this.ConversionFailed(input);
            }
        }
    }
}
