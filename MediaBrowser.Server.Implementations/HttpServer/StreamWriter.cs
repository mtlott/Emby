﻿using MediaBrowser.Model.Logging;
using ServiceStack.Web;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;

namespace MediaBrowser.Server.Implementations.HttpServer
{
    /// <summary>
    /// Class StreamWriter
    /// </summary>
    public class StreamWriter : IStreamWriter, IHasOptions
    {
        private ILogger Logger { get; set; }

        private static readonly CultureInfo UsCulture = new CultureInfo("en-US");
        
        /// <summary>
        /// Gets or sets the source stream.
        /// </summary>
        /// <value>The source stream.</value>
        private Stream SourceStream { get; set; }

        /// <summary>
        /// The _options
        /// </summary>
        private readonly IDictionary<string, string> _options = new Dictionary<string, string>();
        /// <summary>
        /// Gets the options.
        /// </summary>
        /// <value>The options.</value>
        public IDictionary<string, string> Options
        {
            get { return _options; }
        }

        public Action OnComplete { get; set; }

        /// <summary>
        /// Initializes a new instance of the <see cref="StreamWriter" /> class.
        /// </summary>
        /// <param name="source">The source.</param>
        /// <param name="contentType">Type of the content.</param>
        /// <param name="logger">The logger.</param>
        public StreamWriter(Stream source, string contentType, ILogger logger)
        {
            if (string.IsNullOrEmpty(contentType))
            {
                throw new ArgumentNullException("contentType");
            }

            SourceStream = source;
            Logger = logger;

            Options["Content-Type"] = contentType;

            if (source.CanSeek)
            {
                Options["Content-Length"] = source.Length.ToString(UsCulture);
            }
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="StreamWriter"/> class.
        /// </summary>
        /// <param name="source">The source.</param>
        /// <param name="contentType">Type of the content.</param>
        /// <param name="logger">The logger.</param>
        public StreamWriter(byte[] source, string contentType, ILogger logger)
            : this(new MemoryStream(source), contentType, logger)
        {
        }

        /// <summary>
        /// Writes to.
        /// </summary>
        /// <param name="responseStream">The response stream.</param>
        public void WriteTo(Stream responseStream)
        {
            WriteToInternal(responseStream);
        }

        // 256k
        private const int BufferSize = 262144;
        
        /// <summary>
        /// Writes to async.
        /// </summary>
        /// <param name="responseStream">The response stream.</param>
        /// <returns>Task.</returns>
        private void WriteToInternal(Stream responseStream)
        {
            try
            {
                using (var src = SourceStream)
                {
                    src.CopyTo(responseStream, BufferSize);
                }
            }
            catch (Exception ex)
            {
                Logger.ErrorException("Error streaming data", ex);

                throw;
            }
            finally
            {
                if (OnComplete != null)
                {
                    OnComplete();
                }
            }
        }
    }
}
