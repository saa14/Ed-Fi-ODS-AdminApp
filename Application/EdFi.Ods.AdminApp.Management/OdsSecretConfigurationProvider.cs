// SPDX-License-Identifier: Apache-2.0
// Licensed to the Ed-Fi Alliance under one or more agreements.
// The Ed-Fi Alliance licenses this file to you under the Apache License, Version 2.0.
// See the LICENSE and NOTICES files in the project root for more information.

using System;
using System.Threading.Tasks;
using EdFi.Ods.AdminApp.Management.Services;
using System.Runtime.Caching;
using Microsoft.EntityFrameworkCore;
using EdFi.Ods.AdminApp.Management.Database;
using EdFi.Ods.AdminApp.Management.Database.Models;
using Newtonsoft.Json;

namespace EdFi.Ods.AdminApp.Management
{
    public class OdsSecretConfigurationProvider : IOdsSecretConfigurationProvider
    {
        private readonly IStringEncryptorService _stringEncryptorService;
        private readonly AdminAppDbContext _database;
        private readonly ObjectCache _cache = MemoryCache.Default;
        private const string OdsSecretCacheKey = "OdsSecretConfiguration";
        private const string OdsSqlCacheKey = "OdsSqlConfiguration";

        public OdsSecretConfigurationProvider(IStringEncryptorService stringEncryptorService, AdminAppDbContext database)
        {
            _stringEncryptorService = stringEncryptorService;
            _database = database;
        }

        public async Task<OdsSqlConfiguration> GetSqlConfiguration()
        {
            if (_cache.Get(OdsSqlCacheKey) is OdsSqlConfiguration result)
                return result;

            result = await ReadSqlConfigurations();

            if (result != null)
            {
                CacheConfiguration(result, OdsSqlCacheKey);
                await WriteSqlConfiguration(result);
            }

            return result;
        }

        public async Task<OdsSecretConfiguration> GetSecretConfiguration(int? instanceRegistrationId = null)
        {
            var cacheKey = instanceRegistrationId != null ? $"{OdsSecretCacheKey}_{instanceRegistrationId}" : OdsSecretCacheKey;

            if (_cache.Get(cacheKey) is OdsSecretConfiguration result)
                return result;

            result = await ReadSecretConfigurations(instanceRegistrationId);

            if (result != null)
                CacheConfiguration(result, cacheKey);

            return result;
        }

        public async Task SetSecretConfiguration(OdsSecretConfiguration configuration, int? instanceRegistrationId = null)
        {
            var cacheKey = instanceRegistrationId != null ? $"{OdsSecretCacheKey}_{instanceRegistrationId}" : OdsSecretCacheKey;
            
            await WriteSecretConfiguration(configuration, instanceRegistrationId);
            CacheConfiguration(configuration, cacheKey);
        }

        private void CacheConfiguration<T>(T result, string cacheKey)
        {
            _cache.Set(cacheKey, result, DateTimeOffset.Now.AddMinutes(5));
        }

        private async Task<OdsSqlConfiguration> ReadSqlConfigurations()
        {
            var rawValue = (await _database.AzureSqlConfigurations.SingleOrDefaultAsync())?.Configurations;

            if (rawValue == null)
            {
                return null;
            }

            return JsonConvert.DeserializeObject<OdsSqlConfiguration>(
                _stringEncryptorService.TryDecrypt(rawValue, out var unencryptedValue)
                    ? unencryptedValue
                    : rawValue, GetSerializerSettings());
        }

        private async Task<OdsSecretConfiguration> ReadSecretConfigurations(int? instanceRegistrationId)
        {
            var rawValue = (await _database.SecretConfigurations.SingleOrDefaultAsync(x =>
                x.OdsInstanceRegistrationId == instanceRegistrationId))?.EncryptedData;

            if (rawValue == null)
            {
                return null;
            }

            return JsonConvert.DeserializeObject<OdsSecretConfiguration>(
                _stringEncryptorService.TryDecrypt(rawValue, out var unencryptedValue)
                    ? unencryptedValue
                    : rawValue, GetSerializerSettings());
        }

        private async Task WriteSecretConfiguration(OdsSecretConfiguration configuration, int? instanceRegistrationId)
        {
            var stringValue = JsonConvert.SerializeObject(configuration, GetSerializerSettings());
            var encryptedValue = _stringEncryptorService.Encrypt(stringValue);

            var secretConfiguration =
                await _database.SecretConfigurations.SingleOrDefaultAsync(x =>
                    x.OdsInstanceRegistrationId == instanceRegistrationId);
            if (secretConfiguration == null)
                _database.SecretConfigurations.Add(new SecretConfiguration
                    {EncryptedData = encryptedValue, OdsInstanceRegistrationId = instanceRegistrationId});
            else
                secretConfiguration.EncryptedData = encryptedValue;

            await _database.SaveChangesAsync();
        }

        private async Task WriteSqlConfiguration(OdsSqlConfiguration configuration)
        {
            var stringValue = JsonConvert.SerializeObject(configuration, GetSerializerSettings());
            var encryptedValue = _stringEncryptorService.Encrypt(stringValue);

            var sqlConfiguration = await _database.AzureSqlConfigurations.SingleOrDefaultAsync();
            if (sqlConfiguration == null)
                _database.AzureSqlConfigurations.Add(new AzureSqlConfiguration
                { Configurations = encryptedValue });
            else
                sqlConfiguration.Configurations = encryptedValue;

            await _database.SaveChangesAsync();
        }

        private JsonSerializerSettings GetSerializerSettings()
        {
            return new JsonSerializerSettings
            {
                DateFormatHandling = DateFormatHandling.MicrosoftDateFormat
            };
        }
    }
}
