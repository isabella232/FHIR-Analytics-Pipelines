﻿// -------------------------------------------------------------------------------------------------
// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License (MIT). See LICENSE in the repo root for license information.
// -------------------------------------------------------------------------------------------------

using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using DotLiquid;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Microsoft.Health.Fhir.Synapse.SchemaManagement.Exceptions;
using Microsoft.Health.Fhir.TemplateManagement;
using Microsoft.Health.Fhir.TemplateManagement.Exceptions;
using Microsoft.Health.Fhir.TemplateManagement.Models;

namespace Microsoft.Health.Fhir.Synapse.SchemaManagement.ContainerRegistry
{
    public class ContainerRegistryTemplateProvider : IContainerRegistryTemplateProvider
    {
        private readonly IContainerRegistryTokenProvider _containerRegistryTokenProvider;
        private readonly ITemplateCollectionProviderFactory _templateCollectionProviderFactory;
        private readonly ILogger<ContainerRegistryTemplateProvider> _logger;

        public ContainerRegistryTemplateProvider(
            IContainerRegistryTokenProvider containerRegistryTokenProvider,
            ILogger<ContainerRegistryTemplateProvider> logger)
        {
            _containerRegistryTokenProvider = containerRegistryTokenProvider;
            _logger = logger;

            var templateCollectionCache = new MemoryCache(new MemoryCacheOptions() { SizeLimit = 100000000 });
            var config = new TemplateCollectionConfiguration();
            _templateCollectionProviderFactory = new TemplateCollectionProviderFactory(templateCollectionCache, Options.Create(config));
        }

        public async Task<List<Dictionary<string, Template>>> GetTemplateCollectionAsync(string schemaImageReference, CancellationToken cancellationToken)
        {
            ImageInfo imageInfo = ImageInfo.CreateFromImageReference(schemaImageReference);
            var accessToken = await _containerRegistryTokenProvider.GetTokenAsync(imageInfo.Registry, cancellationToken);

            try
            {
                var provider = _templateCollectionProviderFactory.CreateTemplateCollectionProvider(schemaImageReference, accessToken);
                return await provider.GetTemplateCollectionAsync(cancellationToken);
            }
            catch (ContainerRegistryAuthenticationException authEx)
            {
                _logger.LogError(authEx, "Failed to access container registry.");
                throw new ContainerRegistrySchemaException("Failed to access container registry.", authEx);
            }
            catch (ImageFetchException fetchEx)
            {
                _logger.LogError(fetchEx, "Failed to fetch template image.");
                throw new ContainerRegistrySchemaException("Failed to fetch template image.", fetchEx);
            }
            catch (TemplateManagementException templateEx)
            {
                _logger.LogError(templateEx, "Template collection is invalid.");
                throw new ContainerRegistrySchemaException("Template collection is invalid.", templateEx);
            }
            catch (Exception unhandledEx)
            {
                _logger.LogError(unhandledEx, "Unhandled exception: failed to get template collection.");
                throw new ContainerRegistrySchemaException("Unhandled exception: failed to get template collection.", unhandledEx);
            }
        }
    }
}
