// Copyright Amazon.com, Inc. or its affiliates. All Rights Reserved.
// SPDX-License-Identifier: Apache-2.0

using System;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;
using AWS.Deploy.Common.IO;
using Newtonsoft.Json;
using Xunit;
using Should;
using AWS.Deploy.CLI.Common.UnitTests.IO;
using AWS.Deploy.Orchestration.DeploymentManifest;
using AWS.Deploy.Common;
using Moq;
using AWS.Deploy.Orchestration;
using AWS.Deploy.Common.DeploymentManifest;
using Amazon.Runtime;

namespace AWS.Deploy.CLI.Common.UnitTests.DeploymentManifestFile
{
    public class DeploymentManifestFileTests : IDisposable
    {
        private readonly IFileManager _fileManager;
        private readonly IDirectoryManager _directoryManager;
        private readonly string _targetApplicationFullPath;
        private readonly string _targetApplicationDirectoryFullPath;

        private bool _isDisposed;

        public DeploymentManifestFileTests()
        {
            _fileManager = new FileManager();
            _directoryManager = new DirectoryManager();
            var targetApplicationPath = Path.Combine("testapps", "WebAppWithDockerFile", "WebAppWithDockerFile.csproj");
            _targetApplicationFullPath = _directoryManager.GetDirectoryInfo(targetApplicationPath).FullName;
            _targetApplicationDirectoryFullPath = _directoryManager.GetDirectoryInfo(targetApplicationPath).Parent.FullName;
        }

        private async Task<DeploymentManifestEngine> BuildDeploymentManifestEngine()
        {
            var parser = new ProjectDefinitionParser(_fileManager, _directoryManager);
            var awsCredentials = new Mock<AWSCredentials>();
            var session = new OrchestratorSession(
                await parser.Parse(_targetApplicationFullPath),
                awsCredentials.Object,
                "us-west-2",
                "123456789012");

            return new DeploymentManifestEngine(_directoryManager, _fileManager, session, _targetApplicationFullPath);
        }

        [Fact]
        public async Task UpdateDeploymentManifestFile()
        {
            // Arrange
            var deploymentManifestEngine = await BuildDeploymentManifestEngine();
            var saveCdkDirectoryFullPath = Path.Combine(_targetApplicationDirectoryFullPath, "DeploymentProjects", "MyCdkApp");
            var saveCdkDirectoryFullPath2 = Path.Combine(_targetApplicationDirectoryFullPath, "DeploymentProjects", "MyCdkApp2");
            var saveCdkDirectoryFullPath3 = Path.Combine(_targetApplicationDirectoryFullPath, "DeploymentProjects", "MyCdkApp3");

            _directoryManager.CreateDirectory(saveCdkDirectoryFullPath);
            _directoryManager.CreateDirectory(saveCdkDirectoryFullPath2);
            _directoryManager.CreateDirectory(saveCdkDirectoryFullPath3);

            var saveCdkDirectoryRelativePath = Path.GetRelativePath(_targetApplicationDirectoryFullPath, saveCdkDirectoryFullPath);
            var saveCdkDirectoryRelativePath2 = Path.GetRelativePath(_targetApplicationDirectoryFullPath, saveCdkDirectoryFullPath2);
            var saveCdkDirectoryRelativePath3 = Path.GetRelativePath(_targetApplicationDirectoryFullPath, saveCdkDirectoryFullPath3);

            var deploymentManifestFilePath = Path.Combine(_targetApplicationDirectoryFullPath, "aws-deployments.json");

            // Act
            await deploymentManifestEngine.UpdateSaveCdkProject(saveCdkDirectoryFullPath);

            // Assert
            Assert.True(_fileManager.Exists(deploymentManifestFilePath));
            var deploymentProjectPaths = await GetDeploymentManifestEntries(deploymentManifestFilePath);
            Assert.Single(deploymentProjectPaths);
            deploymentProjectPaths.ShouldContain(saveCdkDirectoryRelativePath);

            // Update deployment-manifest file
            await deploymentManifestEngine.UpdateSaveCdkProject(saveCdkDirectoryFullPath2);
            await deploymentManifestEngine.UpdateSaveCdkProject(saveCdkDirectoryFullPath3);

            // Assert
            Assert.True(_fileManager.Exists(deploymentManifestFilePath));
            deploymentProjectPaths = await GetDeploymentManifestEntries(deploymentManifestFilePath);
            Assert.Equal(3, deploymentProjectPaths.Count);
            deploymentProjectPaths.ShouldContain(saveCdkDirectoryRelativePath);
            deploymentProjectPaths.ShouldContain(saveCdkDirectoryRelativePath2);
            deploymentProjectPaths.ShouldContain(saveCdkDirectoryRelativePath3);

            // cleanup
            File.Delete(deploymentManifestFilePath);
            Assert.False(_fileManager.Exists(deploymentManifestFilePath));
        }

        [Fact]
        public async Task GetRecipeDefinitionPaths()
        {
            // Arrange
            var deploymentManifestEngine = await BuildDeploymentManifestEngine();
            var saveCdkDirectoryFullPath = Path.Combine(_targetApplicationDirectoryFullPath, "DeploymentProjects", "MyCdkApp");
            var saveCdkDirectoryFullPath2 = Path.Combine(_targetApplicationDirectoryFullPath, "DeploymentProjects", "MyCdkApp2");
            var saveCdkDirectoryFullPath3 = Path.Combine(_targetApplicationDirectoryFullPath, "DeploymentProjects", "MyCdkApp3");

            _directoryManager.CreateDirectory(saveCdkDirectoryFullPath);
            _directoryManager.CreateDirectory(saveCdkDirectoryFullPath2);
            _directoryManager.CreateDirectory(saveCdkDirectoryFullPath3);

            await deploymentManifestEngine.UpdateSaveCdkProject(saveCdkDirectoryFullPath);
            await deploymentManifestEngine.UpdateSaveCdkProject(saveCdkDirectoryFullPath2);
            await deploymentManifestEngine.UpdateSaveCdkProject(saveCdkDirectoryFullPath3);

            var deploymentManifestFilePath = Path.Combine(_targetApplicationDirectoryFullPath, "aws-deployments.json");

            // Act
            var recipeDefinitionPaths = await deploymentManifestEngine.GetRecipeDefinitionPaths();

            // Assert
            Assert.True(_fileManager.Exists(deploymentManifestFilePath));
            recipeDefinitionPaths.Count.ShouldEqual(3);
            recipeDefinitionPaths.ShouldContain(saveCdkDirectoryFullPath);
            recipeDefinitionPaths.ShouldContain(saveCdkDirectoryFullPath2);
            recipeDefinitionPaths.ShouldContain(saveCdkDirectoryFullPath3);

            // cleanup
            File.Delete(deploymentManifestFilePath);
            Assert.False(_fileManager.Exists(deploymentManifestFilePath));
        }

        private async Task<List<string>> GetDeploymentManifestEntries(string deploymentManifestFilePath)
        {
            var deploymentProjectPaths = new List<string>();
            var manifestFilejsonString = await _fileManager.ReadAllTextAsync(deploymentManifestFilePath);
            var deploymentManifestModel = JsonConvert.DeserializeObject<DeploymentManifestModel>(manifestFilejsonString);

            foreach (var entry in deploymentManifestModel.DeploymentManifestEntries)
            {
                deploymentProjectPaths.Add(entry.SaveCdkDirectoryRelativePath);
            }

            return deploymentProjectPaths;
        }

        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }

        protected virtual void Dispose(bool disposing)
        {
            if (_isDisposed) return;

            if (disposing)
            {
                var deploymentManifestFilePath = Path.Combine(_targetApplicationDirectoryFullPath, "aws-deployments.json");
                if (_fileManager.Exists(deploymentManifestFilePath))
                {
                    File.Delete(deploymentManifestFilePath);
                }
            }

            _isDisposed = true;
        }

        ~DeploymentManifestFileTests()
        {
            Dispose(false);
        }
    }
}
