// Copyright Amazon.com, Inc. or its affiliates. All Rights Reserved.
// SPDX-License-Identifier: Apache-2.0

using System.Collections.Generic;

namespace AWS.Deploy.Common.DeploymentManifest
{
    public class LastDeployedStack
    {
        public string AWSAccountId { get; set; }

        public string AWSRegion { get; set; }

        public List<string> Stacks { get; set; }

        public LastDeployedStack(string awsAccountId, string awsRegion, List<string> stacks)
        {
            AWSAccountId = awsAccountId;
            AWSRegion = awsRegion;
            Stacks = stacks;
        }
    }
}
