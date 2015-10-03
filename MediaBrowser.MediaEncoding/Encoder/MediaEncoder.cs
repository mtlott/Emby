using MediaBrowser.Common.IO;
using MediaBrowser.Controller.Channels;
using MediaBrowser.Controller.Configuration;
using MediaBrowser.Controller.Library;
using MediaBrowser.Controller.LiveTv;
using MediaBrowser.Controller.MediaEncoding;
using MediaBrowser.Controller.Session;
using MediaBrowser.MediaEncoding.Probing;
using MediaBrowser.Model.Dlna;
using MediaBrowser.Model.Dto;
using MediaBrowser.Model.Entities;
using MediaBrowser.Model.Extensions;
using MediaBrowser.Model.IO;
using MediaBrowser.Model.Logging;
using MediaBrowser.Model.MediaInfo;
using MediaBrowser.Model.Serialization;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace MediaBrowser.MediaEncoding.Encoder
{
    /// <summary>
    /// Class MediaEncoder
    /// </summary>
    public class MediaEncoder : IMediaEncoder, IDisposable
    {
        /// <summary>
        /// The _logger
        /// </summary>
        private readonly ILogger _logger;

        /// <summary>
        /// Gets the json serializer.
        /// </summary>
        /// <value>The json serializer.</value>
        private readonly IJsonSerializer _jsonSerializer;

        /// <summary>
        /// The _thumbnail resource pool
        /// </summary>
        private readonly SemaphoreSlim _thumbnailResourcePool = new SemaphoreSlim(1, 1);

        /// <summary>
        /// The video image resource pool
        /// </summary>
        private readonly SemaphoreSlim _videoImageResourcePool = new SemaphoreSlim(1, 1);

        /// <summary>
        /// The audio image resource pool
        /// </summary>
        private readonly SemaphoreSlim _audioImageResourcePool = new SemaphoreSlim(2, 2);

        /// <summary>
        /// The FF probe resource pool
        /// </summary>
        private readonly SemaphoreSlim _ffProbeResourcePool = new SemaphoreSlim(2, 2);

        public string FFMpegPath { get; private set; }

        public string FFProbePath { get; private set; }

        public string Version { get; private set; }

        protected readonly IServerConfigurationManager ConfigurationManager;
        protected readonly IFileSystem FileSystem;
        protected readonly ILiveTvManager LiveTvManager;
        protected readonly IIsoManager IsoManager;
        protected readonly ILibraryManager LibraryManager;
        protected readonly IChannelManager ChannelManager;
        protected readonly ISessionManager SessionManager;
        protected readonly Func<ISubtitleEncoder> SubtitleEncoder;
        protected readonly Func<IMediaSourceManager> MediaSourceManager;

        private readonly List<ProcessWrapper> _runningProcesses = new List<ProcessWrapper>();

        public MediaEncoder(ILogger logger, IJsonSerializer jsonSerializer, string ffMpegPath, string ffProbePath, string version, IServerConfigurationManager configurationManager, IFileSystem fileSystem, ILiveTvManager liveTvManager, IIsoManager isoManager, ILibraryManager libraryManager, IChannelManager channelManager, ISessionManager sessionManager, Func<ISubtitleEncoder> subtitleEncoder, Func<IMediaSourceManager> mediaSourceManager)
        {
            _logger = logger;
            _jsonSerializer = jsonSerializer;
            Version = version;
            ConfigurationManager = configurationManager;
            FileSystem = fileSystem;
            LiveTvManager = liveTvManager;
            IsoManager = isoManager;
            LibraryManager = libraryManager;
            ChannelManager = channelManager;
            SessionManager = sessionManager;
            SubtitleEncoder = subtitleEncoder;
            MediaSourceManager = mediaSourceManager;
            FFProbePath = ffProbePath;
            FFMpegPath = ffMpegPath;
        }

        /// <summary>
        /// Gets the encoder path.
        /// </summary>
        /// <value>The encoder path.</value>
        public string EncoderPath
        {
            get { return FFMpegPath; }
        }

        /// <summary>
        /// Gets the media info.
        /// </summary>
        /// <param name="request">The request.</param>
        /// <param name="cancellationToken">The cancellation token.</param>
        /// <returns>Task.</returns>
        public Task<Model.MediaInfo.MediaInfo> GetMediaInfo(MediaInfoRequest request, CancellationToken cancellationToken)
        {
            var extractChapters = request.MediaType == DlnaProfileType.Video && request.ExtractChapters;

            var inputFiles = MediaEncoderHelpers.GetInputArgument(request.InputPath, request.Protocol, request.MountedIso, request.PlayableStreamFileNames);

            var extractKeyFrameInterval = request.ExtractKeyFrameInterval && request.Protocol == MediaProtocol.File && request.VideoType == VideoType.VideoFile;

            return GetMediaInfoInternal(GetInputArgument(inputFiles, request.Protocol), request.InputPath, request.Protocol, extractChapters, extractKeyFrameInterval,
                GetProbeSizeArgument(inputFiles, request.Protocol), request.MediaType == DlnaProfileType.Audio, request.VideoType, cancellationToken);
        }

        /// <summary>
        /// Gets the input argument.
        /// </summary>
        /// <param name="inputFiles">The input files.</param>
        /// <param name="protocol">The protocol.</param>
        /// <returns>System.String.</returns>
        /// <exception cref="System.ArgumentException">Unrecognized InputType</exception>
        public string GetInputArgument(string[] inputFiles, MediaProtocol protocol)
        {
            return EncodingUtils.GetInputArgument(inputFiles.ToList(), protocol);
        }

        /// <summary>
        /// Gets the probe size argument.
        /// </summary>
        /// <param name="inputFiles">The input files.</param>
        /// <param name="protocol">The protocol.</param>
        /// <returns>System.String.</returns>
        public string GetProbeSizeArgument(string[] inputFiles, MediaProtocol protocol)
        {
            return EncodingUtils.GetProbeSizeArgument(inputFiles.Length > 1);
        }

        /// <summary>
        /// Gets the media info internal.
        /// </summary>
        /// <param name="inputPath">The input path.</param>
        /// <param name="primaryPath">The primary path.</param>
        /// <param name="protocol">The protocol.</param>
        /// <param name="extractChapters">if set to <c>true</c> [extract chapters].</param>
        /// <param name="extractKeyFrameInterval">if set to <c>true</c> [extract key frame interval].</param>
        /// <param name="probeSizeArgument">The probe size argument.</param>
        /// <param name="isAudio">if set to <c>true</c> [is audio].</param>
        /// <param name="videoType">Type of the video.</param>
        /// <param name="cancellationToken">The cancellation token.</param>
        /// <returns>Task{MediaInfoResult}.</returns>
        /// <exception cref="System.ApplicationException"></exception>
        private async Task<Model.MediaInfo.MediaInfo> GetMediaInfoInternal(string inputPath,
            string primaryPath,
            MediaProtocol protocol,
            bool extractChapters,
            bool extractKeyFrameInterval,
            string probeSizeArgument,
            bool isAudio,
            VideoType videoType,
            CancellationToken cancellationToken)
        {
            var args = extractChapters
                ? "{0} -i {1} -threads 0 -v info -print_format json -show_streams -show_chapters -show_format"
                : "{0} -i {1} -threads 0 -v info -print_format json -show_streams -show_format";

            var process = new Process
            {
                StartInfo = new ProcessStartInfo
                {
                    CreateNoWindow = true,
                    UseShellExecute = false,

                    // Must consume both or ffmpeg may hang due to deadlocks. See comments below.   
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    RedirectStandardInput = true,
                    FileName = FFProbePath,
                    Arguments = string.Format(args,
                    probeSizeArgument, inputPath).Trim(),

                    WindowStyle = ProcessWindowStyle.Hidden,
                    ErrorDialog = false
                },

                EnableRaisingEvents = true
            };

            _logger.Debug("{0} {1}", process.StartInfo.FileName, process.StartInfo.Arguments);

            await _ffProbeResourcePool.WaitAsync(cancellationToken).ConfigureAwait(false);

            using (var processWrapper = new ProcessWrapper(process, this, _logger))
            {
                try
                {
                    StartProcess(processWrapper);
                }
                catch (Exception ex)
                {
                    _ffProbeResourcePool.Release();

                    _logger.ErrorException("Error starting ffprobe", ex);

                    throw;
                }

                try
                {
                    process.BeginErrorReadLine();

                    var result = _jsonSerializer.DeserializeFromStream<InternalMediaInfoResult>(process.StandardOutput.BaseStream);

                    if (result != null)
                    {
                        if (result.streams != null)
                        {
                            // Normalize aspect ratio if invalid
                            foreach (var stream in result.streams)
                            {
                                if (string.Equals(stream.display_aspect_ratio, "0:1", StringComparison.OrdinalIgnoreCase))
                                {
                                    stream.display_aspect_ratio = string.Empty;
                                }
                                if (string.Equals(stream.sample_aspect_ratio, "0:1", StringComparison.OrdinalIgnoreCase))
                                {
                                    stream.sample_aspect_ratio = string.Empty;
                                }
                            }
                        }

                        var mediaInfo = new ProbeResultNormalizer(_logger, FileSystem).GetMediaInfo(result, videoType, isAudio, primaryPath, protocol);

                        if (extractKeyFrameInterval && mediaInfo.RunTimeTicks.HasValue)
                        {
                            if (ConfigurationManager.Configuration.EnableVideoFrameByFrameAnalysis && mediaInfo.Size.HasValue)
                            {
                                foreach (var stream in mediaInfo.MediaStreams)
                                {
                                    if (EnableKeyframeExtraction(mediaInfo, stream))
                                    {
                                        try
                                        {
                                            stream.KeyFrames = await GetKeyFrames(inputPath, stream.Index, cancellationToken).ConfigureAwait(false);
                                        }
                                        catch (OperationCanceledException)
                                        {

                                        }
                                        catch (Exception ex)
                                        {
                                            _logger.ErrorException("Error getting key frame interval", ex);
                                        }
                                    }
                                }
                            }
                        }

                        return mediaInfo;
                    }
                }
                catch
                {
                    StopProcess(processWrapper, 100, true);

                    throw;
                }
                finally
                {
                    _ffProbeResourcePool.Release();
                }
            }

            throw new ApplicationException(string.Format("FFProbe failed for {0}", inputPath));
        }

        private bool EnableKeyframeExtraction(MediaSourceInfo mediaSource, MediaStream videoStream)
        {
            if (videoStream.Type == MediaStreamType.Video && string.Equals(videoStream.Codec, "h264", StringComparison.OrdinalIgnoreCase) &&
                !videoStream.IsInterlaced &&
                !(videoStream.IsAnamorphic ?? false))
            {
                var audioStreams = mediaSource.MediaStreams.Where(i => i.Type == MediaStreamType.Audio).ToList();

                // If it has aac audio then it will probably direct stream anyway, so don't bother with this
                if (audioStreams.Count == 1 && string.Equals(audioStreams[0].Codec, "aac", StringComparison.OrdinalIgnoreCase))
                {
                    return false;
                }

                return true;
            }
            return false;
        }

        private async Task<List<int>> GetKeyFrames(string inputPath, int videoStreamIndex, CancellationToken cancellationToken)
        {
            inputPath = inputPath.Split(new[] { ':' }, 2).Last().Trim('"');

            const string args = "-show_packets -print_format compact -select_streams v:{1} -show_entries packet=flags -show_entries packet=pts_time \"{0}\"";

            var process = new Process
            {
                StartInfo = new ProcessStartInfo
                {
                    CreateNoWindow = true,
                    UseShellExecute = false,

                    // Must consume both or ffmpeg may hang due to deadlocks. See comments below.   
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    FileName = FFProbePath,
                    Arguments = string.Format(args, inputPath, videoStreamIndex.ToString(CultureInfo.InvariantCulture)).Trim(),

                    WindowStyle = ProcessWindowStyle.Hidden,
                    ErrorDialog = false
                },

                EnableRaisingEvents = true
            };

            _logger.Debug("{0} {1}", process.StartInfo.FileName, process.StartInfo.Arguments);

            using (process)
            {
                var start = DateTime.UtcNow;

                process.Start();

                var lines = new List<int>();

                try
                {
                    process.BeginErrorReadLine();

                    await StartReadingOutput(process.StandardOutput.BaseStream, lines, cancellationToken).ConfigureAwait(false);
                }
                catch (OperationCanceledException)
                {
                    if (cancellationToken.IsCancellationRequested)
                    {
                        throw;
                    }
                }

                process.WaitForExit();

                _logger.Debug("Keyframe extraction took {0} seconds", (DateTime.UtcNow - start).TotalSeconds);
                //_logger.Debug("Found keyframes {0}", string.Join(",", lines.ToArray()));
                return lines;
            }
        }

        private async Task StartReadingOutput(Stream source, List<int> keyframes, CancellationToken cancellationToken)
        {
            try
            {
                using (var reader = new StreamReader(source))
                {
                    var text = await reader.ReadToEndAsync().ConfigureAwait(false);

                    var lines = StringHelper.RegexSplit(text, "[\r\n]+");
                    foreach (var line in lines)
                    {
                        if (string.IsNullOrWhiteSpace(line))
                        {
                            continue;
                        }

                        var values = line.Split('|')
                            .Where(i => !string.IsNullOrWhiteSpace(i))
                            .Select(i => i.Split('='))
                            .Where(i => i.Length == 2)
                            .ToDictionary(i => i[0], i => i[1]);

                        string flags;
                        if (values.TryGetValue("flags", out flags) && string.Equals(flags, "k", StringComparison.OrdinalIgnoreCase))
                        {
                            string pts_time;
                            double frameSeconds;
                            if (values.TryGetValue("pts_time", out pts_time) && double.TryParse(pts_time, NumberStyles.Any, CultureInfo.InvariantCulture, out frameSeconds))
                            {
                                var ms = frameSeconds * 1000;
                                keyframes.Add(Convert.ToInt32(ms));
                            }
                        }
                    }
                }
            }
            catch (OperationCanceledException)
            {
                throw;
            }
            catch (Exception ex)
            {
                _logger.ErrorException("Error reading ffprobe output", ex);
            }
        }
        /// <summary>
        /// The us culture
        /// </summary>
        protected readonly CultureInfo UsCulture = new CultureInfo("en-US");

        public Task<Stream> ExtractAudioImage(string path, CancellationToken cancellationToken)
        {
            return ExtractImage(new[] { path }, MediaProtocol.File, true, null, null, cancellationToken);
        }

        public Task<Stream> ExtractVideoImage(string[] inputFiles, MediaProtocol protocol, Video3DFormat? threedFormat,
            TimeSpan? offset, CancellationToken cancellationToken)
        {
            return ExtractImage(inputFiles, protocol, false, threedFormat, offset, cancellationToken);
        }

        private async Task<Stream> ExtractImage(string[] inputFiles, MediaProtocol protocol, bool isAudio,
            Video3DFormat? threedFormat, TimeSpan? offset, CancellationToken cancellationToken)
        {
            var resourcePool = isAudio ? _audioImageResourcePool : _videoImageResourcePool;

            var inputArgument = GetInputArgument(inputFiles, protocol);

            if (!isAudio)
            {
                try
                {
                    return await ExtractImageInternal(inputArgument, protocol, threedFormat, offset, true, resourcePool, cancellationToken).ConfigureAwait(false);
                }
                catch (ArgumentException)
                {
                    throw;
                }
                catch
                {
                    _logger.Error("I-frame image extraction failed, will attempt standard way. Input: {0}", inputArgument);
                }
            }

            return await ExtractImageInternal(inputArgument, protocol, threedFormat, offset, false, resourcePool, cancellationToken).ConfigureAwait(false);
        }

        private async Task<Stream> ExtractImageInternal(string inputPath, MediaProtocol protocol, Video3DFormat? threedFormat, TimeSpan? offset, bool useIFrame, SemaphoreSlim resourcePool, CancellationToken cancellationToken)
        {
            if (string.IsNullOrEmpty(inputPath))
            {
                throw new ArgumentNullException("inputPath");
            }

            // apply some filters to thumbnail extracted below (below) crop any black lines that we made and get the correct ar then scale to width 600. 
            // This filter chain may have adverse effects on recorded tv thumbnails if ar changes during presentation ex. commercials @ diff ar
            var vf = "scale=600:trunc(600/dar/2)*2";

            if (threedFormat.HasValue)
            {
                switch (threedFormat.Value)
                {
                    case Video3DFormat.HalfSideBySide:
                        vf = "crop=iw/2:ih:0:0,scale=(iw*2):ih,setdar=dar=a,crop=min(iw\\,ih*dar):min(ih\\,iw/dar):(iw-min(iw\\,iw*sar))/2:(ih - min (ih\\,ih/sar))/2,setsar=sar=1,scale=600:trunc(600/dar/2)*2";
                        // hsbs crop width in half,scale to correct size, set the display aspect,crop out any black bars we may have made the scale width to 600. Work out the correct height based on the display aspect it will maintain the aspect where -1 in this case (3d) may not.
                        break;
                    case Video3DFormat.FullSideBySide:
                        vf = "crop=iw/2:ih:0:0,setdar=dar=a,crop=min(iw\\,ih*dar):min(ih\\,iw/dar):(iw-min(iw\\,iw*sar))/2:(ih - min (ih\\,ih/sar))/2,setsar=sar=1,scale=600:trunc(600/dar/2)*2";
                        //fsbs crop width in half,set the display aspect,crop out any black bars we may have made the scale width to 600.
                        break;
                    case Video3DFormat.HalfTopAndBottom:
                        vf = "crop=iw:ih/2:0:0,scale=(iw*2):ih),setdar=dar=a,crop=min(iw\\,ih*dar):min(ih\\,iw/dar):(iw-min(iw\\,iw*sar))/2:(ih - min (ih\\,ih/sar))/2,setsar=sar=1,scale=600:trunc(600/dar/2)*2";
                        //htab crop heigh in half,scale to correct size, set the display aspect,crop out any black bars we may have made the scale width to 600
                        break;
                    case Video3DFormat.FullTopAndBottom:
                        vf = "crop=iw:ih/2:0:0,setdar=dar=a,crop=min(iw\\,ih*dar):min(ih\\,iw/dar):(iw-min(iw\\,iw*sar))/2:(ih - min (ih\\,ih/sar))/2,setsar=sar=1,scale=600:trunc(600/dar/2)*2";
                        // ftab crop heigt in half, set the display aspect,crop out any black bars we may have made the scale width to 600
                        break;
                }
            }

            // TODO: Output in webp for smaller sizes
            // -f image2 -f webp

            // Use ffmpeg to sample 100 (we can drop this if required using thumbnail=50 for 50 frames) frames and pick the best thumbnail. Have a fall back just in case.
            var args = useIFrame ? string.Format("-i {0} -threads 1 -v quiet -vframes 1 -vf \"{2},thumbnail=30\" -f image2 \"{1}\"", inputPath, "-", vf) :
                string.Format("-i {0} -threads 1 -v quiet -vframes 1 -vf \"{2}\" -f image2 \"{1}\"", inputPath, "-", vf);

            var probeSize = GetProbeSizeArgument(new[] { inputPath }, protocol);

            if (!string.IsNullOrEmpty(probeSize))
            {
                args = probeSize + " " + args;
            }

            if (offset.HasValue)
            {
                args = string.Format("-ss {0} ", GetTimeParameter(offset.Value)) + args;
            }

            var process = new Process
            {
                StartInfo = new ProcessStartInfo
                {
                    CreateNoWindow = true,
                    UseShellExecute = false,
                    FileName = FFMpegPath,
                    Arguments = args,
                    WindowStyle = ProcessWindowStyle.Hidden,
                    ErrorDialog = false,
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    RedirectStandardInput = true
                }
            };

            _logger.Debug("{0} {1}", process.StartInfo.FileName, process.StartInfo.Arguments);

            await resourcePool.WaitAsync(cancellationToken).ConfigureAwait(false);

            using (var processWrapper = new ProcessWrapper(process, this, _logger))
            {
                bool ranToCompletion;

                var memoryStream = new MemoryStream();

                try
                {
                    StartProcess(processWrapper);

#pragma warning disable 4014
                    // Important - don't await the log task or we won't be able to kill ffmpeg when the user stops playback
                    process.StandardOutput.BaseStream.CopyToAsync(memoryStream);
#pragma warning restore 4014

                    // MUST read both stdout and stderr asynchronously or a deadlock may occurr
                    process.BeginErrorReadLine();

                    ranToCompletion = process.WaitForExit(10000);

                    if (!ranToCompletion)
                    {
                        StopProcess(processWrapper, 1000, false);
                    }

                }
                finally
                {
                    resourcePool.Release();
                }

                var exitCode = ranToCompletion ? processWrapper.ExitCode ?? 0 : -1;

                if (exitCode == -1 || memoryStream.Length == 0)
                {
                    memoryStream.Dispose();

                    var msg = string.Format("ffmpeg image extraction failed for {0}", inputPath);

                    _logger.Error(msg);

                    throw new ApplicationException(msg);
                }

                memoryStream.Position = 0;
                return memoryStream;
            }
        }

        public string GetTimeParameter(long ticks)
        {
            var time = TimeSpan.FromTicks(ticks);

            return GetTimeParameter(time);
        }

        public string GetTimeParameter(TimeSpan time)
        {
            return time.ToString(@"hh\:mm\:ss\.fff", UsCulture);
        }

        public async Task ExtractVideoImagesOnInterval(string[] inputFiles,
            MediaProtocol protocol,
            Video3DFormat? threedFormat,
            TimeSpan interval,
            string targetDirectory,
            string filenamePrefix,
            int? maxWidth,
            CancellationToken cancellationToken)
        {
            var resourcePool = _thumbnailResourcePool;

            var inputArgument = GetInputArgument(inputFiles, protocol);

            var vf = "fps=fps=1/" + interval.TotalSeconds.ToString(UsCulture);

            if (maxWidth.HasValue)
            {
                var maxWidthParam = maxWidth.Value.ToString(UsCulture);

                vf += string.Format(",scale=min(iw\\,{0}):trunc(ow/dar/2)*2", maxWidthParam);
            }

            Directory.CreateDirectory(targetDirectory);
            var outputPath = Path.Combine(targetDirectory, filenamePrefix + "%05d.jpg");

            var args = string.Format("-i {0} -threads 1 -v quiet -vf \"{2}\" -f image2 \"{1}\"", inputArgument, outputPath, vf);

            var probeSize = GetProbeSizeArgument(new[] { inputArgument }, protocol);

            if (!string.IsNullOrEmpty(probeSize))
            {
                args = probeSize + " " + args;
            }

            var process = new Process
            {
                StartInfo = new ProcessStartInfo
                {
                    CreateNoWindow = true,
                    UseShellExecute = false,
                    FileName = FFMpegPath,
                    Arguments = args,
                    WindowStyle = ProcessWindowStyle.Hidden,
                    ErrorDialog = false,
                    RedirectStandardInput = true
                }
            };

            _logger.Info(process.StartInfo.FileName + " " + process.StartInfo.Arguments);

            await resourcePool.WaitAsync(cancellationToken).ConfigureAwait(false);

            bool ranToCompletion = false;

            using (var processWrapper = new ProcessWrapper(process, this, _logger))
            {
                try
                {
                    StartProcess(processWrapper);

                    // Need to give ffmpeg enough time to make all the thumbnails, which could be a while,
                    // but we still need to detect if the process hangs.
                    // Making the assumption that as long as new jpegs are showing up, everything is good.

                    bool isResponsive = true;
                    int lastCount = 0;

                    while (isResponsive)
                    {
                        if (process.WaitForExit(30000))
                        {
                            ranToCompletion = true;
                            break;
                        }

                        cancellationToken.ThrowIfCancellationRequested();

                        var jpegCount = Directory.GetFiles(targetDirectory)
                            .Count(i => string.Equals(Path.GetExtension(i), ".jpg", StringComparison.OrdinalIgnoreCase));

                        isResponsive = (jpegCount > lastCount);
                        lastCount = jpegCount;
                    }

                    if (!ranToCompletion)
                    {
                        StopProcess(processWrapper, 1000, false);
                    }
                }
                finally
                {
                    resourcePool.Release();
                }

                var exitCode = ranToCompletion ? processWrapper.ExitCode ?? 0 : -1;

                if (exitCode == -1)
                {
                    var msg = string.Format("ffmpeg image extraction failed for {0}", inputArgument);

                    _logger.Error(msg);

                    throw new ApplicationException(msg);
                }
            }
        }

        public async Task<string> EncodeAudio(EncodingJobOptions options,
            IProgress<double> progress,
            CancellationToken cancellationToken)
        {
            var job = await new AudioEncoder(this,
                _logger,
                ConfigurationManager,
                FileSystem,
                IsoManager,
                LibraryManager,
                SessionManager,
                SubtitleEncoder(),
                MediaSourceManager())
                .Start(options, progress, cancellationToken).ConfigureAwait(false);

            await job.TaskCompletionSource.Task.ConfigureAwait(false);

            return job.OutputFilePath;
        }

        public async Task<string> EncodeVideo(EncodingJobOptions options,
            IProgress<double> progress,
            CancellationToken cancellationToken)
        {
            var job = await new VideoEncoder(this,
                _logger,
                ConfigurationManager,
                FileSystem,
                IsoManager,
                LibraryManager,
                SessionManager,
                SubtitleEncoder(),
                MediaSourceManager())
                .Start(options, progress, cancellationToken).ConfigureAwait(false);

            await job.TaskCompletionSource.Task.ConfigureAwait(false);

            return job.OutputFilePath;
        }

        private void StartProcess(ProcessWrapper process)
        {
            process.Process.Start();

            lock (_runningProcesses)
            {
                _runningProcesses.Add(process);
            }
        }
        private void StopProcess(ProcessWrapper process, int waitTimeMs, bool enableForceKill)
        {
            try
            {
                _logger.Info("Killing ffmpeg process");

                try
                {
                    process.Process.StandardInput.WriteLine("q");
                }
                catch (Exception)
                {
                    _logger.Error("Error sending q command to process");
                }

                try
                {
                    if (process.Process.WaitForExit(waitTimeMs))
                    {
                        return;
                    }
                }
                catch (Exception ex)
                {
                    _logger.Error("Error in WaitForExit", ex);
                }

                if (enableForceKill)
                {
                    process.Process.Kill();
                }
            }
            catch (Exception ex)
            {
                _logger.ErrorException("Error killing process", ex);
            }
        }

        private void StopProcesses()
        {
            List<ProcessWrapper> proceses;
            lock (_runningProcesses)
            {
                proceses = _runningProcesses.ToList();
            }
            _runningProcesses.Clear();

            foreach (var process in proceses)
            {
                if (!process.HasExited)
                {
                    StopProcess(process, 500, true);
                }
            }
        }

        public string EscapeSubtitleFilterPath(string path)
        {
            return path.Replace('\\', '/').Replace(":/", "\\:/").Replace("'", "'\\\\\\''");
        }

        /// <summary>
        /// Performs application-defined tasks associated with freeing, releasing, or resetting unmanaged resources.
        /// </summary>
        public void Dispose()
        {
            Dispose(true);
        }

        /// <summary>
        /// Releases unmanaged and - optionally - managed resources.
        /// </summary>
        /// <param name="dispose"><c>true</c> to release both managed and unmanaged resources; <c>false</c> to release only unmanaged resources.</param>
        protected virtual void Dispose(bool dispose)
        {
            if (dispose)
            {
                _videoImageResourcePool.Dispose();
                StopProcesses();
            }
        }

        private class ProcessWrapper : IDisposable
        {
            public readonly Process Process;
            public bool HasExited;
            public int? ExitCode;
            private readonly MediaEncoder _mediaEncoder;
            private readonly ILogger _logger;

            public ProcessWrapper(Process process, MediaEncoder mediaEncoder, ILogger logger)
            {
                Process = process;
                _mediaEncoder = mediaEncoder;
                _logger = logger;
                Process.Exited += Process_Exited;
            }

            void Process_Exited(object sender, EventArgs e)
            {
                var process = (Process)sender;

                HasExited = true;

                try
                {
                    ExitCode = process.ExitCode;
                }
                catch (Exception ex)
                {
                }

                lock (_mediaEncoder._runningProcesses)
                {
                    _mediaEncoder._runningProcesses.Remove(this);
                }

                process.Dispose();
            }

            private bool _disposed;
            private readonly object _syncLock = new object();
            public void Dispose()
            {
                lock (_syncLock)
                {
                    if (!_disposed)
                    {
                        if (Process != null)
                        {
                            Process.Exited -= Process_Exited;
                            Process.Dispose();
                        }
                    }

                    _disposed = true;
                }
            }
        }
    }
}
