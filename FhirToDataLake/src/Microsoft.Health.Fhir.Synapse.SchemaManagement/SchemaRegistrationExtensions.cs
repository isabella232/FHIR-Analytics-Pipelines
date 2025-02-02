﻿// -------------------------------------------------------------------------------------------------
// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License (MIT). See LICENSE in the repo root for license information.
// -------------------------------------------------------------------------------------------------

using Microsoft.Extensions.DependencyInjection;
using Microsoft.Health.Fhir.Synapse.SchemaManagement.ContainerRegistry;
using Microsoft.Health.Fhir.Synapse.SchemaManagement.Exceptions;
using Microsoft.Health.Fhir.Synapse.SchemaManagement.Parquet;
using Microsoft.Health.Fhir.Synapse.SchemaManagement.Parquet.SchemaProvider;

namespace Microsoft.Health.Fhir.Synapse.SchemaManagement
{
    public static class SchemaRegistrationExtensions
    {
        public static IServiceCollection AddSchema(this IServiceCollection services)
        {
            services.AddSingleton<IContainerRegistryTokenProvider, ContainerRegistryAccessTokenProvider>();
            services.AddSingleton<IContainerRegistryTemplateProvider, ContainerRegistryTemplateProvider>();

            services.AddSchemaProviders();

            services.AddSingleton<IFhirSchemaManager<FhirParquetSchemaNode>, FhirParquetSchemaManager>();

            return services;
        }

        public static IServiceCollection AddSchemaProviders(this IServiceCollection services)
        {
            services.AddTransient<AcrCustomizedSchemaProvider>();
            services.AddTransient<LocalDefaultSchemaProvider>();

            services.AddTransient<ParquetSchemaProviderDelegate>(delegateProvider => name =>
            {
                switch (name)
                {
                    case FhirParquetSchemaConstants.DefaultSchemaProviderKey:
                        return delegateProvider.GetService<LocalDefaultSchemaProvider>();
                    case FhirParquetSchemaConstants.CustomSchemaProviderKey:
                        return delegateProvider.GetService<AcrCustomizedSchemaProvider>();
                    default:
                        throw new FhirSchemaException($"Schema delegate name {name} not found when injecting");
                }
            });

            return services;
        }
    }
}
