// Copyright Amazon.com, Inc. or its affiliates. All Rights Reserved.
// SPDX-License-Identifier: Apache-2.0

using System.Collections.Generic;
using Newtonsoft.Json;

namespace AWS.Deploy.Common.DeploymentManifest
{
    /// <summary>
    /// This class supports serialization and de-serialization of the deployment-manifest file.
    /// </summary>
    public class DeploymentManifestModel
    {
        public List<LastDeployedStack>? LastDeployedStacks { get; set; }

        public List<DeploymentManifestEntry>? DeploymentManifestEntries { get; set; }

        public DeploymentManifestModel(List<LastDeployedStack> lastDeployedStacks, List<DeploymentManifestEntry> deploymentManifestEntries)
        {
            LastDeployedStacks = lastDeployedStacks;
            DeploymentManifestEntries = deploymentManifestEntries;
        }
    }
}
