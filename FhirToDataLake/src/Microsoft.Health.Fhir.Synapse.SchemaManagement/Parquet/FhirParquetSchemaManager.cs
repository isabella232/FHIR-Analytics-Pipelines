﻿// -------------------------------------------------------------------------------------------------
// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License (MIT). See LICENSE in the repo root for license information.
// -------------------------------------------------------------------------------------------------

using System.Collections.Generic;
using System.Linq;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Microsoft.Health.Fhir.Synapse.Common.Configurations;
using Microsoft.Health.Fhir.Synapse.SchemaManagement.Parquet.SchemaProvider;

namespace Microsoft.Health.Fhir.Synapse.SchemaManagement.Parquet
{
    public class FhirParquetSchemaManager : IFhirSchemaManager<FhirParquetSchemaNode>
    {
        private readonly Dictionary<string, List<string>> _schemaTypesMap;
        private readonly Dictionary<string, FhirParquetSchemaNode> _resourceSchemaNodesMap;
        private readonly ILogger<FhirParquetSchemaManager> _logger;
        private readonly IParquetSchemaProvider _defaultSchemaProvider;
        private readonly IParquetSchemaProvider _customizedSchemaProvider;

        public FhirParquetSchemaManager(
            IOptions<SchemaConfiguration> schemaConfiguration,
            ParquetSchemaProviderDelegate parquetSchemaDelegate,
            ILogger<FhirParquetSchemaManager> logger)
        {
            _logger = logger;

            _defaultSchemaProvider = parquetSchemaDelegate(FhirParquetSchemaConstants.DefaultSchemaProviderKey);

            // Get default schema, the default schema keys are resource types, like "Patient", "Encounter".
            _resourceSchemaNodesMap = _defaultSchemaProvider.GetSchemasAsync(schemaConfiguration.Value.SchemaCollectionDirectory).Result;
            _logger.LogInformation($"{_resourceSchemaNodesMap.Count} resource default schemas have been loaded.");

            if (schemaConfiguration.Value.EnableCustomizedSchema)
            {
                var customizedSchemaProvider = parquetSchemaDelegate(FhirParquetSchemaConstants.CustomSchemaProviderKey);

                // Get default schema, the customized schema keys are resource types with "_customized" suffix, like "Patient_Customized", "Encounter_Customized".
                var resourceCustomizedSchemaNodesMap = customizedSchemaProvider.GetSchemasAsync(schemaConfiguration.Value.CustomizedSchemaImageReference).Result;
                _logger.LogInformation($"{resourceCustomizedSchemaNodesMap.Count} resource customized schemas have been loaded.");
                _resourceSchemaNodesMap = _resourceSchemaNodesMap.Concat(resourceCustomizedSchemaNodesMap).ToDictionary(x => x.Key, x => x.Value);
            }

            _logger.LogInformation($"Initialize FHIR schemas completed.");

            _schemaTypesMap = new Dictionary<string, List<string>>();

            // Temporarily set schema type list only contains single value for each resource type
            // E.g:
            // Schema list for "Patient" resource is ["Patient"]
            // Schema list for "Observation" resource is ["Observation"]
            foreach (var schemaNodeItem in _resourceSchemaNodesMap)
            {
                if (_schemaTypesMap.ContainsKey(schemaNodeItem.Value.Type))
                {
                    _schemaTypesMap[schemaNodeItem.Value.Type].Add(schemaNodeItem.Key);
                }
                else
                {
                    _schemaTypesMap.Add(schemaNodeItem.Value.Type, new List<string>() { schemaNodeItem.Key });
                }
            }
        }

        public List<string> GetSchemaTypes(string resourceType)
        {
            if (!_schemaTypesMap.ContainsKey(resourceType))
            {
                _logger.LogError($"Schema types for {resourceType} is empty.");
                return new List<string>();
            }

            return _schemaTypesMap[resourceType];
        }

        public FhirParquetSchemaNode GetSchema(string schemaType)
        {
            if (!_resourceSchemaNodesMap.ContainsKey(schemaType))
            {
                _logger.LogError($"Schema for schema type {schemaType} is not supported.");
                return null;
            }

            return _resourceSchemaNodesMap[schemaType];
        }

        public Dictionary<string, FhirParquetSchemaNode> GetAllSchemas()
        {
            return new Dictionary<string, FhirParquetSchemaNode>(_resourceSchemaNodesMap);
        }
    }
}
