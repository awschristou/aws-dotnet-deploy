// Copyright Amazon.com, Inc. or its affiliates. All Rights Reserved.
// SPDX-License-Identifier: Apache-2.0

using System;
using System.Diagnostics;
using System.Threading;
using System.Threading.Tasks;
using AWS.Deploy.Orchestration.Utilities;

namespace AWS.Deploy.Orchestration.CDK
{
    /// <summary>
    /// Makes sure that a compatible version of CDK CLI is installed either in the global node_modules
    /// or local node_modules.
    /// </summary>
    public interface ICDKManager
    {
        /// <summary>
        /// Detects whether CDK CLI is installed or not in global node_modules.
        /// If global node_modules don't contain, it checks in local node_modules
        /// If local npm package isn't initialized, it initializes a npm package at <see cref="workingDirectory"/>.
        /// If local node_modules don't contain, it installs CDK CLI version <see cref="cdkVersion"/> in local modules.
        /// </summary>
        /// <param name="workingDirectory">Directory used for local node app</param>
        /// <param name="cdkVersion">Version of CDK CLI</param>
        Task EnsureCompatibleCDKExists(string workingDirectory, Version cdkVersion);
    }

    public class CDKManager : ICDKManager
    {
        private static readonly SemaphoreSlim s_cdkManagerSemaphoreSlim = new(1,1);

        private readonly ICDKInstaller _cdkInstaller;
        private readonly INPMPackageInitializer _npmPackageInitializer;
        private readonly IOrchestratorInteractiveService _interactiveService;

        public CDKManager(ICDKInstaller cdkInstaller, INPMPackageInitializer npmPackageInitializer, IOrchestratorInteractiveService interactiveService)
        {
            _cdkInstaller = cdkInstaller;
            _npmPackageInitializer = npmPackageInitializer;
            _interactiveService = interactiveService;
        }

        public async Task EnsureCompatibleCDKExists(string workingDirectory, Version cdkVersion)
        {
            await s_cdkManagerSemaphoreSlim.WaitAsync();

            try
            {
                var installedCdkVersion = await _cdkInstaller.GetVersion(workingDirectory);
                if (installedCdkVersion.Success && installedCdkVersion.Result?.CompareTo(cdkVersion) >= 0)
                {
                    _interactiveService.LogDebugLine($"CDK version {installedCdkVersion.Result} found in global node_modules.");
                    return;
                }

                var isNPMPackageInitialized = _npmPackageInitializer.IsInitialized(workingDirectory);
                if (!isNPMPackageInitialized)
                {
                    await _npmPackageInitializer.Initialize(workingDirectory, cdkVersion);
                    return; // There is no need to install CDK CLI explicitly, npm install takes care of first time bootstrap.
                }

                await _cdkInstaller.Install(workingDirectory, cdkVersion);
            }
            finally
            {
                s_cdkManagerSemaphoreSlim.Release();
            }
        }
    }
}
