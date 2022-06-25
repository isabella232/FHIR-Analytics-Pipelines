﻿// -------------------------------------------------------------------------------------------------
// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License (MIT). See LICENSE in the repo root for license information.
// -------------------------------------------------------------------------------------------------

using System;
using System.Threading;
using System.Threading.Channels;
using System.Threading.Tasks;
using EnsureThat;
using Microsoft.Extensions.Logging;
using Microsoft.Health.Fhir.Synapse.Common.Models.Jobs;
using Microsoft.Health.Fhir.Synapse.Common.Models.Tasks;
using Microsoft.Health.Fhir.Synapse.Core.Extensions;

namespace Microsoft.Health.Fhir.Synapse.Core.Jobs
{
    public class JobProgressUpdater
    {
        private readonly IJobStore _jobStore;
        private readonly Job _job;
        private readonly Channel<TaskContext> _progressChannel;
        private readonly ILogger<JobProgressUpdater> _logger;

        // Time interval to sync job to store.
        private const int UploadDataIntervalInSeconds = 30;

        public JobProgressUpdater(
            IJobStore jobStore,
            Job job,
            ILogger<JobProgressUpdater> logger)
        {
            EnsureArg.IsNotNull(jobStore, nameof(jobStore));
            EnsureArg.IsNotNull(job, nameof(job));
            EnsureArg.IsNotNull(logger, nameof(logger));

            _jobStore = jobStore;
            _job = job;
            _logger = logger;
            var channelOptions = new UnboundedChannelOptions
            {
                SingleReader = true,
                SingleWriter = false,
            };
            _progressChannel = Channel.CreateUnbounded<TaskContext>(channelOptions);
        }

        public async Task Consume(CancellationToken cancellationToken = default)
        {
            var uploadTime = DateTimeOffset.MinValue;

            var channelReader = _progressChannel.Reader;
            while (await channelReader.WaitToReadAsync(cancellationToken))
            {
                if (channelReader.TryRead(out TaskContext context))
                {
                    _job.RunningTasks[context.Id] = context;

                    if (context.IsCompleted)
                    {
                        _job.TotalResourceCounts = DictionaryExtensions.ConcatDictionaryCount(_job.TotalResourceCounts, context.SearchCount);
                        _job.SkippedResourceCounts = DictionaryExtensions.ConcatDictionaryCount(_job.SkippedResourceCounts, context.SkippedCount);
                        _job.ProcessedResourceCounts = DictionaryExtensions.ConcatDictionaryCount(_job.ProcessedResourceCounts, context.ProcessedCount);

                        _job.RunningTasks.Remove(context.Id);
                    }

                    if (uploadTime.AddSeconds(UploadDataIntervalInSeconds) < DateTimeOffset.UtcNow)
                    {
                        uploadTime = DateTimeOffset.UtcNow;

                        // Upload to job store.
                        await _jobStore.UpdateJobAsync(_job, cancellationToken);
                        _logger.LogInformation("Update job {jobId} progress successfully.", _job.Id);
                    }
                }
            }

            await _jobStore.UpdateJobAsync(_job, cancellationToken);
        }

        public async Task Produce(TaskContext context, CancellationToken cancellationToken = default)
        {
            var channelWriter = _progressChannel.Writer;
            await channelWriter.WriteAsync(context, cancellationToken);
        }

        public void Complete()
        {
            _progressChannel.Writer.Complete();
        }
    }
}
