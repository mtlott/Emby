﻿using System.Globalization;
using MediaBrowser.Controller.Persistence;
using MediaBrowser.Model.Entities;
using MediaBrowser.Model.Logging;
using System;
using System.Collections.Generic;
using System.Data;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace MediaBrowser.Server.Implementations.Persistence
{
    class SqliteMediaStreamsRepository
    {
        private IDbConnection _connection;

        private readonly ILogger _logger;

        private IDbCommand _deleteStreamsCommand;
        private IDbCommand _saveStreamCommand;

        public SqliteMediaStreamsRepository(IDbConnection connection, ILogManager logManager)
        {
            _connection = connection;

            _logger = logManager.GetLogger(GetType().Name);
        }

        /// <summary>
        /// Opens the connection to the database
        /// </summary>
        /// <returns>Task.</returns>
        public void Initialize()
        {
            var createTableCommand
                = "create table if not exists mediastreams ";

            // Add PixelFormat column

            createTableCommand += "(ItemId GUID, StreamIndex INT, StreamType TEXT, Codec TEXT, Language TEXT, ChannelLayout TEXT, Profile TEXT, AspectRatio TEXT, Path TEXT, IsInterlaced BIT, BitRate INT NULL, Channels INT NULL, SampleRate INT NULL, IsDefault BIT, IsForced BIT, IsExternal BIT, Height INT NULL, Width INT NULL, AverageFrameRate FLOAT NULL, RealFrameRate FLOAT NULL, Level FLOAT NULL, PixelFormat TEXT, BitDepth INT NULL, IsAnamorphic BIT NULL, RefFrames INT NULL, IsCabac BIT NULL, KeyFrames TEXT NULL, PRIMARY KEY (ItemId, StreamIndex))";

            string[] queries = {

                                createTableCommand,

                                "create index if not exists idx_mediastreams on mediastreams(ItemId, StreamIndex)",

                                //pragmas
                                "pragma temp_store = memory",

                                "pragma shrink_memory"
                               };

            _connection.RunQueries(queries, _logger);

            AddPixelFormatColumnCommand();
            AddBitDepthCommand();
            AddIsAnamorphicColumn();
            AddIsCabacColumn();
            AddKeyFramesColumn();
            AddRefFramesCommand();

            PrepareStatements();
        }

        private void AddPixelFormatColumnCommand()
        {
            using (var cmd = _connection.CreateCommand())
            {
                cmd.CommandText = "PRAGMA table_info(mediastreams)";

                using (var reader = cmd.ExecuteReader(CommandBehavior.SequentialAccess | CommandBehavior.SingleResult))
                {
                    while (reader.Read())
                    {
                        if (!reader.IsDBNull(1))
                        {
                            var name = reader.GetString(1);

                            if (string.Equals(name, "PixelFormat", StringComparison.OrdinalIgnoreCase))
                            {
                                return;
                            }
                        }
                    }
                }
            }

            var builder = new StringBuilder();

            builder.AppendLine("alter table mediastreams");
            builder.AppendLine("add column PixelFormat TEXT");

            _connection.RunQueries(new[] { builder.ToString() }, _logger);
        }

        private void AddBitDepthCommand()
        {
            using (var cmd = _connection.CreateCommand())
            {
                cmd.CommandText = "PRAGMA table_info(mediastreams)";

                using (var reader = cmd.ExecuteReader(CommandBehavior.SequentialAccess | CommandBehavior.SingleResult))
                {
                    while (reader.Read())
                    {
                        if (!reader.IsDBNull(1))
                        {
                            var name = reader.GetString(1);

                            if (string.Equals(name, "BitDepth", StringComparison.OrdinalIgnoreCase))
                            {
                                return;
                            }
                        }
                    }
                }
            }

            var builder = new StringBuilder();

            builder.AppendLine("alter table mediastreams");
            builder.AppendLine("add column BitDepth INT NULL");

            _connection.RunQueries(new[] { builder.ToString() }, _logger);
        }

        private void AddRefFramesCommand()
        {
            using (var cmd = _connection.CreateCommand())
            {
                cmd.CommandText = "PRAGMA table_info(mediastreams)";

                using (var reader = cmd.ExecuteReader(CommandBehavior.SequentialAccess | CommandBehavior.SingleResult))
                {
                    while (reader.Read())
                    {
                        if (!reader.IsDBNull(1))
                        {
                            var name = reader.GetString(1);

                            if (string.Equals(name, "RefFrames", StringComparison.OrdinalIgnoreCase))
                            {
                                return;
                            }
                        }
                    }
                }
            }

            var builder = new StringBuilder();

            builder.AppendLine("alter table mediastreams");
            builder.AppendLine("add column RefFrames INT NULL");

            _connection.RunQueries(new[] { builder.ToString() }, _logger);
        }

        private void AddIsCabacColumn()
        {
            using (var cmd = _connection.CreateCommand())
            {
                cmd.CommandText = "PRAGMA table_info(mediastreams)";

                using (var reader = cmd.ExecuteReader(CommandBehavior.SequentialAccess | CommandBehavior.SingleResult))
                {
                    while (reader.Read())
                    {
                        if (!reader.IsDBNull(1))
                        {
                            var name = reader.GetString(1);

                            if (string.Equals(name, "IsCabac", StringComparison.OrdinalIgnoreCase))
                            {
                                return;
                            }
                        }
                    }
                }
            }

            var builder = new StringBuilder();

            builder.AppendLine("alter table mediastreams");
            builder.AppendLine("add column IsCabac BIT NULL");

            _connection.RunQueries(new[] { builder.ToString() }, _logger);
        }

        private void AddKeyFramesColumn()
        {
            using (var cmd = _connection.CreateCommand())
            {
                cmd.CommandText = "PRAGMA table_info(mediastreams)";

                using (var reader = cmd.ExecuteReader(CommandBehavior.SequentialAccess | CommandBehavior.SingleResult))
                {
                    while (reader.Read())
                    {
                        if (!reader.IsDBNull(1))
                        {
                            var name = reader.GetString(1);

                            if (string.Equals(name, "KeyFrames", StringComparison.OrdinalIgnoreCase))
                            {
                                return;
                            }
                        }
                    }
                }
            }

            var builder = new StringBuilder();

            builder.AppendLine("alter table mediastreams");
            builder.AppendLine("add column KeyFrames TEXT NULL");

            _connection.RunQueries(new[] { builder.ToString() }, _logger);
        }

        private void AddIsAnamorphicColumn()
        {
            using (var cmd = _connection.CreateCommand())
            {
                cmd.CommandText = "PRAGMA table_info(mediastreams)";

                using (var reader = cmd.ExecuteReader(CommandBehavior.SequentialAccess | CommandBehavior.SingleResult))
                {
                    while (reader.Read())
                    {
                        if (!reader.IsDBNull(1))
                        {
                            var name = reader.GetString(1);

                            if (string.Equals(name, "IsAnamorphic", StringComparison.OrdinalIgnoreCase))
                            {
                                return;
                            }
                        }
                    }
                }
            }

            var builder = new StringBuilder();

            builder.AppendLine("alter table mediastreams");
            builder.AppendLine("add column IsAnamorphic BIT NULL");

            _connection.RunQueries(new[] { builder.ToString() }, _logger);
        }

        private readonly string[] _saveColumns =
        {
            "ItemId",
            "StreamIndex",
            "StreamType",
            "Codec",
            "Language",
            "ChannelLayout",
            "Profile",
            "AspectRatio",
            "Path",
            "IsInterlaced",
            "BitRate",
            "Channels",
            "SampleRate",
            "IsDefault",
            "IsForced",
            "IsExternal",
            "Height",
            "Width",
            "AverageFrameRate",
            "RealFrameRate",
            "Level",
            "PixelFormat",
            "BitDepth",
            "IsAnamorphic",
            "RefFrames",
            "IsCabac",
            "KeyFrames"
        };

        /// <summary>
        /// The _write lock
        /// </summary>
        private readonly SemaphoreSlim _writeLock = new SemaphoreSlim(1, 1);

        /// <summary>
        /// Prepares the statements.
        /// </summary>
        private void PrepareStatements()
        {
            _deleteStreamsCommand = _connection.CreateCommand();
            _deleteStreamsCommand.CommandText = "delete from mediastreams where ItemId=@ItemId";
            _deleteStreamsCommand.Parameters.Add(_deleteStreamsCommand, "@ItemId");

            _saveStreamCommand = _connection.CreateCommand();

            _saveStreamCommand.CommandText = string.Format("replace into mediastreams ({0}) values ({1})",
                string.Join(",", _saveColumns),
                string.Join(",", _saveColumns.Select(i => "@" + i).ToArray()));

            foreach (var col in _saveColumns)
            {
                _saveStreamCommand.Parameters.Add(_saveStreamCommand, "@" + col);
            }
        }

        public IEnumerable<MediaStream> GetMediaStreams(MediaStreamQuery query)
        {
            if (query == null)
            {
                throw new ArgumentNullException("query");
            }

            using (var cmd = _connection.CreateCommand())
            {
                var cmdText = "select " + string.Join(",", _saveColumns) + " from mediastreams where";

                cmdText += " ItemId=@ItemId";
                cmd.Parameters.Add(cmd, "@ItemId", DbType.Guid).Value = query.ItemId;

                if (query.Type.HasValue)
                {
                    cmdText += " AND StreamType=@StreamType";
                    cmd.Parameters.Add(cmd, "@StreamType", DbType.String).Value = query.Type.Value.ToString();
                }

                if (query.Index.HasValue)
                {
                    cmdText += " AND StreamIndex=@StreamIndex";
                    cmd.Parameters.Add(cmd, "@StreamIndex", DbType.Int32).Value = query.Index.Value;
                }

                cmdText += " order by StreamIndex ASC";

                cmd.CommandText = cmdText;

                using (var reader = cmd.ExecuteReader(CommandBehavior.SequentialAccess | CommandBehavior.SingleResult))
                {
                    while (reader.Read())
                    {
                        yield return GetMediaStream(reader);
                    }
                }
            }
        }

        /// <summary>
        /// Gets the chapter.
        /// </summary>
        /// <param name="reader">The reader.</param>
        /// <returns>ChapterInfo.</returns>
        private MediaStream GetMediaStream(IDataReader reader)
        {
            var item = new MediaStream
            {
                Index = reader.GetInt32(1)
            };

            item.Type = (MediaStreamType)Enum.Parse(typeof(MediaStreamType), reader.GetString(2), true);

            if (!reader.IsDBNull(3))
            {
                item.Codec = reader.GetString(3);
            }

            if (!reader.IsDBNull(4))
            {
                item.Language = reader.GetString(4);
            }

            if (!reader.IsDBNull(5))
            {
                item.ChannelLayout = reader.GetString(5);
            }

            if (!reader.IsDBNull(6))
            {
                item.Profile = reader.GetString(6);
            }

            if (!reader.IsDBNull(7))
            {
                item.AspectRatio = reader.GetString(7);
            }

            if (!reader.IsDBNull(8))
            {
                item.Path = reader.GetString(8);
            }

            item.IsInterlaced = reader.GetBoolean(9);

            if (!reader.IsDBNull(10))
            {
                item.BitRate = reader.GetInt32(10);
            }

            if (!reader.IsDBNull(11))
            {
                item.Channels = reader.GetInt32(11);
            }

            if (!reader.IsDBNull(12))
            {
                item.SampleRate = reader.GetInt32(12);
            }

            item.IsDefault = reader.GetBoolean(13);
            item.IsForced = reader.GetBoolean(14);
            item.IsExternal = reader.GetBoolean(15);

            if (!reader.IsDBNull(16))
            {
                item.Width = reader.GetInt32(16);
            }

            if (!reader.IsDBNull(17))
            {
                item.Height = reader.GetInt32(17);
            }

            if (!reader.IsDBNull(18))
            {
                item.AverageFrameRate = reader.GetFloat(18);
            }

            if (!reader.IsDBNull(19))
            {
                item.RealFrameRate = reader.GetFloat(19);
            }

            if (!reader.IsDBNull(20))
            {
                item.Level = reader.GetFloat(20);
            }

            if (!reader.IsDBNull(21))
            {
                item.PixelFormat = reader.GetString(21);
            }

            if (!reader.IsDBNull(22))
            {
                item.BitDepth = reader.GetInt32(22);
            }

            if (!reader.IsDBNull(23))
            {
                item.IsAnamorphic = reader.GetBoolean(23);
            }

            if (!reader.IsDBNull(24))
            {
                item.RefFrames = reader.GetInt32(24);
            }

            if (!reader.IsDBNull(25))
            {
                item.IsCabac = reader.GetBoolean(25);
            }

            if (!reader.IsDBNull(26))
            {
                var frames = reader.GetString(26);
                if (!string.IsNullOrWhiteSpace(frames))
                {
                    item.KeyFrames = frames.Split(',').Select(i => int.Parse(i, CultureInfo.InvariantCulture)).ToList();
                }
            }

            return item;
        }

        public async Task SaveMediaStreams(Guid id, IEnumerable<MediaStream> streams, CancellationToken cancellationToken)
        {
            if (id == Guid.Empty)
            {
                throw new ArgumentNullException("id");
            }

            if (streams == null)
            {
                throw new ArgumentNullException("streams");
            }

            cancellationToken.ThrowIfCancellationRequested();

            await _writeLock.WaitAsync(cancellationToken).ConfigureAwait(false);

            IDbTransaction transaction = null;

            try
            {
                transaction = _connection.BeginTransaction();

                // First delete chapters
                _deleteStreamsCommand.GetParameter(0).Value = id;

                _deleteStreamsCommand.Transaction = transaction;

                _deleteStreamsCommand.ExecuteNonQuery();

                foreach (var stream in streams)
                {
                    cancellationToken.ThrowIfCancellationRequested();

                    var index = 0;

                    _saveStreamCommand.GetParameter(index++).Value = id;
                    _saveStreamCommand.GetParameter(index++).Value = stream.Index;
                    _saveStreamCommand.GetParameter(index++).Value = stream.Type.ToString();
                    _saveStreamCommand.GetParameter(index++).Value = stream.Codec;
                    _saveStreamCommand.GetParameter(index++).Value = stream.Language;
                    _saveStreamCommand.GetParameter(index++).Value = stream.ChannelLayout;
                    _saveStreamCommand.GetParameter(index++).Value = stream.Profile;
                    _saveStreamCommand.GetParameter(index++).Value = stream.AspectRatio;
                    _saveStreamCommand.GetParameter(index++).Value = stream.Path;

                    _saveStreamCommand.GetParameter(index++).Value = stream.IsInterlaced;

                    _saveStreamCommand.GetParameter(index++).Value = stream.BitRate;
                    _saveStreamCommand.GetParameter(index++).Value = stream.Channels;
                    _saveStreamCommand.GetParameter(index++).Value = stream.SampleRate;

                    _saveStreamCommand.GetParameter(index++).Value = stream.IsDefault;
                    _saveStreamCommand.GetParameter(index++).Value = stream.IsForced;
                    _saveStreamCommand.GetParameter(index++).Value = stream.IsExternal;

                    _saveStreamCommand.GetParameter(index++).Value = stream.Width;
                    _saveStreamCommand.GetParameter(index++).Value = stream.Height;
                    _saveStreamCommand.GetParameter(index++).Value = stream.AverageFrameRate;
                    _saveStreamCommand.GetParameter(index++).Value = stream.RealFrameRate;
                    _saveStreamCommand.GetParameter(index++).Value = stream.Level;
                    _saveStreamCommand.GetParameter(index++).Value = stream.PixelFormat;
                    _saveStreamCommand.GetParameter(index++).Value = stream.BitDepth;
                    _saveStreamCommand.GetParameter(index++).Value = stream.IsAnamorphic;
                    _saveStreamCommand.GetParameter(index++).Value = stream.RefFrames;
                    _saveStreamCommand.GetParameter(index++).Value = stream.IsCabac;

                    if (stream.KeyFrames == null || stream.KeyFrames.Count == 0)
                    {
                        _saveStreamCommand.GetParameter(index++).Value = null;
                    }
                    else
                    {
                        _saveStreamCommand.GetParameter(index++).Value = string.Join(",", stream.KeyFrames.Select(i => i.ToString(CultureInfo.InvariantCulture)).ToArray());
                    }

                    _saveStreamCommand.Transaction = transaction;
                    _saveStreamCommand.ExecuteNonQuery();
                }

                transaction.Commit();
            }
            catch (OperationCanceledException)
            {
                if (transaction != null)
                {
                    transaction.Rollback();
                }

                throw;
            }
            catch (Exception e)
            {
                _logger.ErrorException("Failed to save media streams:", e);

                if (transaction != null)
                {
                    transaction.Rollback();
                }

                throw;
            }
            finally
            {
                if (transaction != null)
                {
                    transaction.Dispose();
                }

                _writeLock.Release();
            }
        }

        /// <summary>
        /// Performs application-defined tasks associated with freeing, releasing, or resetting unmanaged resources.
        /// </summary>
        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }

        private readonly object _disposeLock = new object();

        /// <summary>
        /// Releases unmanaged and - optionally - managed resources.
        /// </summary>
        /// <param name="dispose"><c>true</c> to release both managed and unmanaged resources; <c>false</c> to release only unmanaged resources.</param>
        protected virtual void Dispose(bool dispose)
        {
            if (dispose)
            {
                try
                {
                    lock (_disposeLock)
                    {
                        if (_connection != null)
                        {
                            if (_connection.IsOpen())
                            {
                                _connection.Close();
                            }

                            _connection.Dispose();
                            _connection = null;
                        }
                    }
                }
                catch (Exception ex)
                {
                    _logger.ErrorException("Error disposing database", ex);
                }
            }
        }
    }
}

