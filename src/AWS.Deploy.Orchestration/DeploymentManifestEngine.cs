// Copyright Amazon.com, Inc. or its affiliates. All Rights Reserved.
// SPDX-License-Identifier: Apache-2.0

using System;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;
using AWS.Deploy.Common.DeploymentManifest;
using AWS.Deploy.Common.IO;
using Newtonsoft.Json;
using System.Linq;
using AWS.Deploy.Common;
using System.Diagnostics;

namespace AWS.Deploy.Orchestration.DeploymentManifest
{
    public interface IDeploymentManifestEngine
    {
        Task UpdateSaveCdkProject(string saveCdkDirectoryFullPath);
        Task UpdateLastDeployedStack(string stackName);
        Task<DeploymentManifestModel?> GetDeploymentManifest();
        Task DeleteLastDeployedStack(string stackName);
        Task<List<string>> GetRecipeDefinitionPaths();
        Task CleanOrphanStacks(List<string> deployedStacks);
    }

    /// <summary>
    /// This class contains the helper methods to update the deployment manifest file
    /// that keeps track of the save CDK deployment projects.
    /// </summary>
    public class DeploymentManifestEngine : IDeploymentManifestEngine
    {
        private readonly IDirectoryManager _directoryManager;
        private readonly IFileManager _fileManager;
        private readonly OrchestratorSession _session;
        private readonly string _targetApplicationFullPath;

        private const string DEPLOYMENT_MANIFEST_FILE_NAME = "aws-deployments.json";

        public DeploymentManifestEngine(IDirectoryManager directoryManager, IFileManager fileManager, OrchestratorSession session, string targetApplicationFullPath)
        {
            _directoryManager = directoryManager;
            _fileManager = fileManager;
            _session = session;
            _targetApplicationFullPath = targetApplicationFullPath;
        }

        /// <summary>
        /// This method updates the deployment manifest json file by adding the directory path at which the CDK deployment project is saved.
        /// If the manifest file does not exists then a new file is generated.
        /// <param name="saveCdkDirectoryFullPath">The absolute path to the directory at which the CDK deployment project is saved</param>
        /// <exception cref="FailedToUpdateDeploymentManifestFileException">Thrown if an error occured while trying to update the deployment manifest file.</exception>
        /// </summary>
        public async Task UpdateSaveCdkProject(string saveCdkDirectoryFullPath)
        {
            try
            {
                if (!_directoryManager.Exists(saveCdkDirectoryFullPath))
                    return;

                var deploymentManifestModel = await GetDeploymentManifest();
                var targetApplicationDirectoryPath = _directoryManager.GetDirectoryInfo(_targetApplicationFullPath).Parent.FullName;
                var saveCdkDirectoryRelativePath = _directoryManager.GetRelativePath(targetApplicationDirectoryPath, saveCdkDirectoryFullPath);

                if (deploymentManifestModel != null && deploymentManifestModel.DeploymentManifestEntries != null)
                {
                    deploymentManifestModel.DeploymentManifestEntries.Add(new DeploymentManifestEntry(saveCdkDirectoryRelativePath));
                }
                else
                {
                    var deploymentManifestEntries = new List<DeploymentManifestEntry> { new DeploymentManifestEntry(saveCdkDirectoryRelativePath) };
                    deploymentManifestModel = new DeploymentManifestModel(new(), deploymentManifestEntries);
                }

                await WriteDeploymentManifestFile(deploymentManifestModel);
            }
            catch (Exception ex)
            {
                throw new FailedToUpdateDeploymentManifestFileException($"Failed to update the deployment manifest file " +
                    $"for the deployment project stored at '{saveCdkDirectoryFullPath}'", ex);
            }
            
        }

        /// <summary>
        /// This method updates the deployment manifest json file by adding the name of the stack that was most recently used.
        /// If the manifest file does not exists then a new file is generated.
        /// </summary>
        public async Task UpdateLastDeployedStack(string stackName)
        {
            try
            {
                if (_session.AWSAccountId == null || _session.AWSRegion == null)
                    throw new FailedToUpdateDeploymentManifestFileException("The AWS Account Id or Region is not defined.");

                var deploymentManifestModel = await GetDeploymentManifest();
                var lastDeployedStack = deploymentManifestModel?.LastDeployedStacks?
                    .FirstOrDefault(x => x.AWSAccountId.Equals(_session.AWSAccountId) && x.AWSRegion.Equals(_session.AWSRegion));

                if (deploymentManifestModel != null && lastDeployedStack != null)
                {
                    if (lastDeployedStack != null)
                    {
                        lastDeployedStack.Stacks.Remove(stackName);
                        lastDeployedStack.Stacks.Insert(0, stackName);
                    }
                    else
                    {
                        deploymentManifestModel.LastDeployedStacks = new List<LastDeployedStack>() {
                            new LastDeployedStack(
                                _session.AWSAccountId,
                                _session.AWSRegion,
                                new List<string>() { stackName })};
                    }
                }
                else
                {

                    var lastDeployedStacks = new List<LastDeployedStack> {
                        new LastDeployedStack(
                            _session.AWSAccountId,
                            _session.AWSRegion,
                            new List<string>() { stackName }) };
                    deploymentManifestModel = new DeploymentManifestModel(lastDeployedStacks, new());
                }

                await WriteDeploymentManifestFile(deploymentManifestModel);
            }
            catch (Exception ex)
            {
                throw new FailedToUpdateDeploymentManifestFileException($"Failed to update the deployment manifest file " +
                    $"to include the last deployed to stack '{stackName}'.", ex);
            }
        }

        /// <summary>
        /// This method updates the deployment manifest json file by deleting the stack that was most recently used.
        /// </summary>
        public async Task DeleteLastDeployedStack(string stackName)
        {
            try
            {
                var deploymentManifestModel = await GetDeploymentManifest();
                var lastDeployedStack = deploymentManifestModel?.LastDeployedStacks
                    .FirstOrDefault(x => x.AWSAccountId.Equals(_session.AWSAccountId) && x.AWSRegion.Equals(_session.AWSRegion));

                if (deploymentManifestModel == null || lastDeployedStack == null)
                    return;
                
                lastDeployedStack.Stacks.Remove(stackName);

                await WriteDeploymentManifestFile(deploymentManifestModel);
            }
            catch (Exception ex)
            {
                throw new FailedToUpdateDeploymentManifestFileException($"Failed to update the deployment manifest file " +
                    $"to delete the stack '{stackName}'.", ex);
            }
        }

        /// <summary>
        /// This method updates the deployment manifest json file by deleting orphan stacks.
        /// </summary>
        public async Task CleanOrphanStacks(List<string> deployedStacks)
        {
            try
            {
                var deploymentManifestModel = await GetDeploymentManifest();
                var localStacks = deploymentManifestModel?.LastDeployedStacks
                    .FirstOrDefault(x => x.AWSAccountId.Equals(_session.AWSAccountId) && x.AWSRegion.Equals(_session.AWSRegion));

                if (deploymentManifestModel == null || localStacks == null)
                    return;

                var validStacks = deployedStacks.Intersect(localStacks.Stacks);

                localStacks.Stacks = validStacks.ToList();

                await WriteDeploymentManifestFile(deploymentManifestModel);
            }
            catch (Exception ex)
            {
                throw new FailedToUpdateDeploymentManifestFileException($"Failed to update the deployment manifest file " +
                    $"to delete orphan stacks.", ex);
            }
        }

        /// <summary>
        /// This method deserializes the deployment-manifest file and returns a list of absolute paths of directories at which different CDK
        /// deployment projects are stored. The custom recipe snapshots are stored in this directory.
        /// </summary>
        /// <returns> A list containing absolute directory paths for CDK deployment projects.</returns>
        public async Task<List<string>> GetRecipeDefinitionPaths()
        {
            var recipeDefinitionPaths = new List<string>();
            var targetApplicationDirectoryPath = _directoryManager.GetDirectoryInfo(_targetApplicationFullPath).Parent.FullName;

            var deploymentManifestModel = await GetDeploymentManifest();
            if (deploymentManifestModel == null || deploymentManifestModel.DeploymentManifestEntries == null)
                return recipeDefinitionPaths;

            foreach (var entry in deploymentManifestModel.DeploymentManifestEntries)
            {
                var saveCdkDirectoryRelativePath = entry.SaveCdkDirectoryRelativePath;
                if (string.IsNullOrEmpty(saveCdkDirectoryRelativePath))
                    continue;

                var saveCdkDirectoryAbsolutePath = _directoryManager.GetAbsolutePath(targetApplicationDirectoryPath, saveCdkDirectoryRelativePath);

                if (_directoryManager.Exists(saveCdkDirectoryAbsolutePath))
                    recipeDefinitionPaths.Add(saveCdkDirectoryAbsolutePath);
            }

            return recipeDefinitionPaths;
        }

        /// <summary>
        /// This method parses the deployment-manifest file into a <see cref="DeploymentManifestModel"/>
        /// </summary>
        public async Task<DeploymentManifestModel?> GetDeploymentManifest()
        {
            try
            {
                var deploymentManifestFilePath = GetDeploymentManifestFilePath(_targetApplicationFullPath);

                if (!_fileManager.Exists(deploymentManifestFilePath))
                    return null;
                var manifestFilejsonString = await _fileManager.ReadAllTextAsync(deploymentManifestFilePath);
                return JsonConvert.DeserializeObject<DeploymentManifestModel>(manifestFilejsonString);
            }
            catch (Exception ex)
            {
                throw new InvalidDeploymentManifestFileException("The Deployment Manifest File is invalid.", ex);
            }
        }

        /// <summary>
        /// This method parses the <see cref="DeploymentManifestModel"/> into a string and writes it to disk.
        /// </summary>
        private async Task WriteDeploymentManifestFile(DeploymentManifestModel deploymentManifestModel)
        {
            var deploymentManifestFilePath = GetDeploymentManifestFilePath(_targetApplicationFullPath);
            var manifestFileJsonString = JsonConvert.SerializeObject(deploymentManifestModel, new JsonSerializerSettings
            {
                Formatting = Formatting.Indented,
                NullValueHandling = NullValueHandling.Ignore,
                ContractResolver = new SerializeRecipeContractResolver()
            });

            await _fileManager.WriteAllTextAsync(deploymentManifestFilePath, manifestFileJsonString);
        }

        /// <summary>
        /// This method returns the path at which the deployment-manifest file will be stored.
        /// <param name="targetApplicationFullPath">The absolute path to the target application csproj or fsproj file</param>
        /// </summary>
        private string GetDeploymentManifestFilePath(string targetApplicationFullPath)
        {
            var projectDirectoryFullPath = _directoryManager.GetDirectoryInfo(targetApplicationFullPath).Parent.FullName;
            var deploymentManifestFileFullPath = Path.Combine(projectDirectoryFullPath, DEPLOYMENT_MANIFEST_FILE_NAME);
            return deploymentManifestFileFullPath;
        }
    }
}
