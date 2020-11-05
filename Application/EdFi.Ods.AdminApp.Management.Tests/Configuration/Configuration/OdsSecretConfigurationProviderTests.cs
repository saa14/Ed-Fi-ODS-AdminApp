// SPDX-License-Identifier: Apache-2.0
// Licensed to the Ed-Fi Alliance under one or more agreements.
// The Ed-Fi Alliance licenses this file to you under the Apache License, Version 2.0.
// See the LICENSE and NOTICES files in the project root for more information.

using System;
using System.Linq;
using System.Runtime.Caching;
using System.Threading.Tasks;
using EdFi.Ods.AdminApp.Management.Database;
using EdFi.Ods.AdminApp.Management.Database.Models;
using EdFi.Ods.AdminApp.Management.Services;
using NUnit.Framework;
using Shouldly;

namespace EdFi.Ods.AdminApp.Management.Tests.Configuration.Configuration
{
    public class OdsSecretConfigurationProviderTests : AdminAppDataTestBase
    {
        private AdminAppDbContext _dbContext;
        private OdsSecretConfigurationProvider _provider;

        [SetUp]
        public void Init()
        {
            _dbContext = TestContext;
            _provider = new OdsSecretConfigurationProvider(
                new StringEncryptorService(
                    new EncryptionConfigurationProviderService()), _dbContext);
        }

        private static OdsSecretConfigurationProvider OdsSecretConfigurationProvider(AdminAppDbContext database)
        {
            return new OdsSecretConfigurationProvider(
                new StringEncryptorService(
                    new EncryptionConfigurationProviderService()), database );
        }

        private async Task<OdsSqlConfiguration> GetSqlConfiguration()
        {
            return await _provider.GetSqlConfiguration();
        }

        private async Task<OdsSecretConfiguration> GetSecretConfiguration(int? instanceRegistrationId = null)
        {
            return await _provider.GetSecretConfiguration(instanceRegistrationId);
        }

        private async Task SetSecretConfiguration(OdsSecretConfiguration configuration, int? instanceRegistrationId = null)
        {
            await _provider.SetSecretConfiguration(configuration, instanceRegistrationId);
        }

        private readonly ObjectCache _cache = MemoryCache.Default;

        [Test]
        public async Task ShouldReturnNullWhenThereAreZeroSecretConfigurations()
        {
            ClearSecretConfigurationCache();
            EnsureZeroSecretConfigurations();

            var secretConfiguration = await GetSecretConfiguration();

            secretConfiguration.ShouldBe(null);
        }

        [Test]
        public async Task ShouldRetrieveUnencryptedSecretConfiguration_SingleInstance()
        {
            const string jsonConfiguration =
                "{\"ProductionApiKeyAndSecret\":{\"Key\":\"productionKey\",\"Secret\":\"productionSecret\"}," +
                "\"BulkUploadCredential\":{\"ApiKey\":\"bulkKey\",\"ApiSecret\":\"bulkSecret\"}," +
                "\"LearningStandardsCredential\":null," +
                "\"ProductionAcademicBenchmarkApiClientKeyAndSecret\":null}";


            ClearSecretConfigurationCache();

            var odsInstanceRegistration = CreateOdsInstanceRegistration("SingleInstance_" + Guid.NewGuid());

            odsInstanceRegistration.ShouldNotBeNull();

            AddSecretConfiguration(jsonConfiguration, odsInstanceRegistration.Id);

            var secretConfiguration = await GetSecretConfiguration(odsInstanceRegistration.Id);

            secretConfiguration.ProductionApiKeyAndSecret.Key.ShouldBe("productionKey");
            secretConfiguration.ProductionApiKeyAndSecret.Secret.ShouldBe("productionSecret");
            secretConfiguration.BulkUploadCredential.ApiKey.ShouldBe("bulkKey");
            secretConfiguration.BulkUploadCredential.ApiSecret.ShouldBe("bulkSecret");
            secretConfiguration.LearningStandardsCredential.ShouldBe(null);
            secretConfiguration.ProductionAcademicBenchmarkApiClientKeyAndSecret.ShouldBe(null);
        }

        [Test]
        public async Task ShouldRetrieveUnencryptedSecretConfiguration_MultiInstance()
        {
            const string jsonConfiguration1 =
                "{\"ProductionApiKeyAndSecret\":{\"Key\":\"productionKey1\",\"Secret\":\"productionSecret1\"}," +
                "\"BulkUploadCredential\":{\"ApiKey\":\"bulkKey1\",\"ApiSecret\":\"bulkSecret1\"}," +
                "\"LearningStandardsCredential\":null," +
                "\"ProductionAcademicBenchmarkApiClientKeyAndSecret\":null}";
            const string jsonConfiguration2 =
                "{\"ProductionApiKeyAndSecret\":{\"Key\":\"productionKey2\",\"Secret\":\"productionSecret2\"}," +
                "\"BulkUploadCredential\":{\"ApiKey\":\"bulkKey2\",\"ApiSecret\":\"bulkSecret2\"}," +
                "\"LearningStandardsCredential\":null," +
                "\"ProductionAcademicBenchmarkApiClientKeyAndSecret\":null}";

            var odsInstanceRegistration1 = CreateOdsInstanceRegistration("MultiInstance1_" + Guid.NewGuid());
            var odsInstanceRegistration2 = CreateOdsInstanceRegistration("MultiInstance2_" + Guid.NewGuid());

            ClearSecretConfigurationCache();

            odsInstanceRegistration1.ShouldNotBeNull();
            odsInstanceRegistration2.ShouldNotBeNull();

            AddSecretConfiguration(jsonConfiguration1, odsInstanceRegistration1.Id);

            var secretConfigurationForInstance1 =
                await GetSecretConfiguration(odsInstanceRegistration1.Id);

            secretConfigurationForInstance1.ProductionApiKeyAndSecret.Key.ShouldBe("productionKey1");
            secretConfigurationForInstance1.ProductionApiKeyAndSecret.Secret.ShouldBe("productionSecret1");
            secretConfigurationForInstance1.BulkUploadCredential.ApiKey.ShouldBe("bulkKey1");
            secretConfigurationForInstance1.BulkUploadCredential.ApiSecret.ShouldBe("bulkSecret1");
            secretConfigurationForInstance1.LearningStandardsCredential.ShouldBe(null);
            secretConfigurationForInstance1.ProductionAcademicBenchmarkApiClientKeyAndSecret.ShouldBe(null);

            AddSecretConfiguration(jsonConfiguration2, odsInstanceRegistration2.Id);
            var secretConfigurationForInstance2 =
                await GetSecretConfiguration(odsInstanceRegistration2.Id);

            secretConfigurationForInstance2.ProductionApiKeyAndSecret.Key.ShouldBe("productionKey2");
            secretConfigurationForInstance2.ProductionApiKeyAndSecret.Secret.ShouldBe("productionSecret2");
            secretConfigurationForInstance2.BulkUploadCredential.ApiKey.ShouldBe("bulkKey2");
            secretConfigurationForInstance2.BulkUploadCredential.ApiSecret.ShouldBe("bulkSecret2");
            secretConfigurationForInstance2.LearningStandardsCredential.ShouldBe(null);
            secretConfigurationForInstance2.ProductionAcademicBenchmarkApiClientKeyAndSecret.ShouldBe(null);
        }

        [Test]
        public async Task ShouldEditSecretConfiguration_SingleInstance()
        {
            ClearSecretConfigurationCache();

            var productionApiKey = Guid.NewGuid().ToString();
            var productionApiSecret = Guid.NewGuid().ToString();

            var newProductionApiKey = Guid.NewGuid().ToString();
            var newProductionApiSecret = Guid.NewGuid().ToString();

            var secretConfiguration = new OdsSecretConfiguration
            {
                ProductionApiKeyAndSecret = new OdsApiCredential
                {
                    Key = productionApiKey,
                    Secret = productionApiSecret
                }
            };

            var editSecretConfiguration = new OdsSecretConfiguration
            {
                ProductionApiKeyAndSecret = new OdsApiCredential
                {
                    Key = newProductionApiKey,
                    Secret = newProductionApiSecret
                }
            };

            var odsInstanceRegistration = CreateOdsInstanceRegistration("SingleInstance-EditConfig_" + Guid.NewGuid());

            odsInstanceRegistration.ShouldNotBeNull();
            
            await SetSecretConfiguration(secretConfiguration, odsInstanceRegistration.Id);
            var createdSecretConfiguration = await GetSecretConfiguration(odsInstanceRegistration.Id);

            createdSecretConfiguration.ProductionApiKeyAndSecret.Key.ShouldBe(productionApiKey);
            createdSecretConfiguration.ProductionApiKeyAndSecret.Secret.ShouldBe(productionApiSecret);
            createdSecretConfiguration.BulkUploadCredential.ShouldBe(null);
            createdSecretConfiguration.LearningStandardsCredential.ShouldBe(null);
            createdSecretConfiguration.ProductionAcademicBenchmarkApiClientKeyAndSecret.ShouldBe(null);

            await SetSecretConfiguration(editSecretConfiguration, odsInstanceRegistration.Id);

            var editedSecretConfiguration = await GetSecretConfiguration(odsInstanceRegistration.Id);

            editedSecretConfiguration.ProductionApiKeyAndSecret.Key.ShouldBe(newProductionApiKey);
            editedSecretConfiguration.ProductionApiKeyAndSecret.Secret.ShouldBe(newProductionApiSecret);
            editedSecretConfiguration.BulkUploadCredential.ShouldBe(null);
            editedSecretConfiguration.LearningStandardsCredential.ShouldBe(null);
            editedSecretConfiguration.ProductionAcademicBenchmarkApiClientKeyAndSecret.ShouldBe(null);

        }

        [Test]
        public async Task ShouldEditSecretConfiguration_MultiInstance()
        {
            ClearSecretConfigurationCache();

            var productionApiKey = Guid.NewGuid().ToString();
            var productionApiSecret = Guid.NewGuid().ToString();

            var newProductionApiKey = Guid.NewGuid().ToString();
            var newProductionApiSecret = Guid.NewGuid().ToString();

            var secretConfiguration = new OdsSecretConfiguration
            {
                ProductionApiKeyAndSecret = new OdsApiCredential
                {
                    Key = productionApiKey,
                    Secret = productionApiSecret
                }
            };

            var editSecretConfiguration = new OdsSecretConfiguration
            {
                ProductionApiKeyAndSecret = new OdsApiCredential
                {
                    Key = newProductionApiKey,
                    Secret = newProductionApiSecret
                }
            };

            var odsInstanceRegistration1 = CreateOdsInstanceRegistration("MultiInstance1-EditConfig_" + Guid.NewGuid());
            var odsInstanceRegistration2 = CreateOdsInstanceRegistration("MultiInstance2-EditConfig_" + Guid.NewGuid());

            odsInstanceRegistration1.ShouldNotBeNull();
            odsInstanceRegistration2.ShouldNotBeNull();

            // Set secret configuration for instance 1
            await SetSecretConfiguration(secretConfiguration, odsInstanceRegistration1.Id);

            // Set secret configuration for instance 2
            await SetSecretConfiguration(secretConfiguration, odsInstanceRegistration2.Id);

            // Edit the secret configuration for instance 1
            await SetSecretConfiguration(editSecretConfiguration, odsInstanceRegistration1.Id);

            // Verify the secret configuration for instance 2
            var createdSecretConfigurationForInstance2 = await GetSecretConfiguration(odsInstanceRegistration2.Id);
            createdSecretConfigurationForInstance2.ProductionApiKeyAndSecret.Key.ShouldBe(productionApiKey);
            createdSecretConfigurationForInstance2.ProductionApiKeyAndSecret.Secret.ShouldBe(productionApiSecret);
            createdSecretConfigurationForInstance2.BulkUploadCredential.ShouldBe(null);
            createdSecretConfigurationForInstance2.LearningStandardsCredential.ShouldBe(null);
            createdSecretConfigurationForInstance2.ProductionAcademicBenchmarkApiClientKeyAndSecret.ShouldBe(null);

            // Verify the edited secret configuration for instance 1
            var editedSecretConfigurationForInstance1 = await GetSecretConfiguration(odsInstanceRegistration1.Id);
            editedSecretConfigurationForInstance1.ProductionApiKeyAndSecret.Key.ShouldBe(newProductionApiKey);
            editedSecretConfigurationForInstance1.ProductionApiKeyAndSecret.Secret.ShouldBe(newProductionApiSecret);
            editedSecretConfigurationForInstance1.BulkUploadCredential.ShouldBe(null);
            editedSecretConfigurationForInstance1.LearningStandardsCredential.ShouldBe(null);
            editedSecretConfigurationForInstance1.ProductionAcademicBenchmarkApiClientKeyAndSecret.ShouldBe(null);
        }

        [Test]
        public async Task ShouldReturnNullWhenThereAreZeroSqlConfigurations()
        {
            ClearSqlConfigurationCache();
            EnsureZeroSqlConfigurations();

            var sqlConfiguration = await GetSqlConfiguration();

            sqlConfiguration.ShouldBe(null);
        }

        [Test]
        public async Task ShouldRetrieveUnencryptedSqlConfiguration()
        {
            const string jsonConfiguration = "{\"AdminCredentials\":{\"Password\":\"adminPassword\",\"UserName\":\"adminUser\"}," +
                                             "\"HostName\":\"localhost\"," +
                                             "\"ProductionApiCredentials\":{\"Password\":\"ProductionApiPassword\",\"UserName\":\"EdFiOdsProductionApi\"}," +
                                             "\"AdminAppCredentials\":{\"Password\":\"AdminAppPassword\",\"UserName\":\"EdFiOdsAdminApp\"}}";

            ClearSqlConfigurationCache();
            EnsureOneSqlConfiguration(jsonConfiguration);

            var sqlConfiguration = await GetSqlConfiguration();

            sqlConfiguration.HostName.ShouldBe("localhost");
            sqlConfiguration.AdminCredentials.UserName.ShouldBe("adminUser");
            sqlConfiguration.AdminCredentials.Password.ShouldBe("adminPassword");
            sqlConfiguration.ProductionApiCredentials.UserName.ShouldBe("EdFiOdsProductionApi");
            sqlConfiguration.ProductionApiCredentials.Password.ShouldBe("ProductionApiPassword");
            sqlConfiguration.AdminAppCredentials.UserName.ShouldBe("EdFiOdsAdminApp");
            sqlConfiguration.AdminAppCredentials.Password.ShouldBe("AdminAppPassword");
        }

        private void EnsureZeroSecretConfigurations()
        {

            foreach (var entity in _dbContext.SecretConfigurations)
                _dbContext.SecretConfigurations.Remove(entity);

            _dbContext.SaveChanges();

        }

        private void EnsureZeroSqlConfigurations()
        {
            foreach (var entity in _dbContext.AzureSqlConfigurations)
                _dbContext.AzureSqlConfigurations.Remove(entity);

            _dbContext.SaveChanges();
        }

        private void AddSecretConfiguration(string jsonConfiguration, int odsInstanceRegistrationId)
        {

            _dbContext.SecretConfigurations.Add(
                new SecretConfiguration
                {
                    EncryptedData = jsonConfiguration, OdsInstanceRegistrationId = odsInstanceRegistrationId
                });

            _dbContext.SaveChanges();

        }

        private OdsInstanceRegistration CreateOdsInstanceRegistration(string instanceName)
        {
            _dbContext.OdsInstanceRegistrations.Add(new OdsInstanceRegistration {Name = instanceName});
            _dbContext.SaveChanges();

            var createdOdsInstanceRegistration = _dbContext.OdsInstanceRegistrations.FirstOrDefault(x => x.Name == instanceName);

            return createdOdsInstanceRegistration;
        }

        private void EnsureOneSqlConfiguration(string jsonConfiguration)
        {
            EnsureZeroSqlConfigurations();

            _dbContext.AzureSqlConfigurations.Add(
                new AzureSqlConfiguration {Configurations = jsonConfiguration});

            _dbContext.SaveChanges();

        }

        private void ClearSecretConfigurationCache()
        {
            var cacheKeys = _cache.Select(k => k.Key).ToList();
            foreach (var cacheKey in cacheKeys)
            {
                _cache.Remove(cacheKey);
            }
        }

        private void ClearSqlConfigurationCache()
        {
            _cache.Remove("OdsSqlConfiguration");
        }
    }
}
