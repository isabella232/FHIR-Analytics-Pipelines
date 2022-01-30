// -------------------------------------------------------------------------------------------------
// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License (MIT). See LICENSE in the repo root for license information.
// -------------------------------------------------------------------------------------------------

using System;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Azure.WebJobs;
using Microsoft.Extensions.Logging;
using Microsoft.Health.Fhir.Synapse.Core.Jobs;

namespace Microsoft.Health.Fhir.Synapse.FunctionApp
{
    public class JobManagerFunction
    {
        private readonly JobManager _jobManager;

        public JobManagerFunction(JobManager jobManager)
        {
            _jobManager = jobManager;
        }

        [FunctionName("JobManagerFunction")]
        public async Task Run(
            [TimerTrigger("0 */1 * * * *", RunOnStartup = false)] TimerInfo myTimer,
            ILogger log,
            CancellationToken stoppingToken)
        {
            log.LogInformation($"Timer trigger function executed at: {DateTime.Now}");

            try
            {
                await _jobManager.RunAsync(stoppingToken);
            }
            catch (OperationCanceledException canceledException)
            {
                log.LogError(canceledException, "Function execution has been canceled or timed out.");
            }
            catch (Exception exception)
            {
                log.LogError(exception, "Function execution failed.");
            }
            finally
            {
                _jobManager.Dispose();
                log.LogInformation("Function exit gracefully.");
            }
        }
    }
}
