// Copyright Amazon.com, Inc. or its affiliates. All Rights Reserved.\r
// SPDX-License-Identifier: Apache-2.0

using System;
using AWS.Deploy.Common;

namespace AWS.Deploy.CLI.ServerMode
{
    /// <summary>
    /// Throw if the selected recommendation is null.
    /// </summary>
    public class SelectedRecommendationIsNullException : Exception
    {
        public SelectedRecommendationIsNullException(string message, Exception? innerException = null) : base(message, innerException) { }
    }

    /// <summary>
    /// Throw if the tool was not able to retrieve the AWS Credentials.
    /// </summary>
    public class FailedToRetrieveAWSCredentialsException : Exception
    {
        public FailedToRetrieveAWSCredentialsException(string message, Exception? innerException = null) : base(message, innerException) { }
    }

    /// <summary>
    /// Throw if encryption key info passed in through stdin is invalid.
    /// </summary>
    public class InvalidEncryptionKeyInfoException : Exception
    {
        public InvalidEncryptionKeyInfoException(string message, Exception? innerException = null) : base(message, innerException) { }
    }

    /// <summary>
    /// Throw if could not find deployment targets mapping.
    /// </summary>
    public class FailedToFindDeploymentTargetsMappingException : Exception
    {
        public FailedToFindDeploymentTargetsMappingException(string message, Exception? innerException = null) : base(message, innerException) { }
    }
}
