// Copyright Amazon.com, Inc. or its affiliates. All Rights Reserved.
// SPDX-License-Identifier: Apache-2.0

using System;
using System.Collections.Generic;
using System.Linq;
using Amazon.Runtime.Internal;
using AWS.Deploy.Common.Recipes;
using AWS.Deploy.Common.Recipes.Validation;
using Newtonsoft.Json;
using Should;
using Xunit;

// Justification: False Positives with assertions, also test class
// ReSharper disable PossibleNullReferenceException

namespace AWS.Deploy.CLI.Common.UnitTests.Recipes.Validation
{
    /// <summary>
    /// Tests for <see cref="ValidatorFactory"/>
    /// </summary>
    public class ValidatorFactoryTests
    {
        [Fact]
        public void HasABindingForAllOptionSettingItemValidators()
        {
            // ARRANGE
            var allValidators = Enum.GetValues(typeof(OptionSettingItemValidatorList));

            var optionSettingItem = new OptionSettingItem("id", "name", "description")
            {
                Validators =
                    allValidators
                        .Cast<OptionSettingItemValidatorList>()
                        .Select(validatorType =>
                            new OptionSettingItemValidatorConfig
                            {
                                ValidatorType = validatorType
                            }
                        )
                        .ToList()
            };

            // ACT
            var validators = optionSettingItem.BuildValidators();

            // ASSERT
            validators.Length.ShouldEqual(allValidators.Length);
        }

        [Fact]
        public void HasABindingForAllRecipeValidators()
        {
            // ARRANGE
            var allValidators = Enum.GetValues(typeof(RecipeValidatorList));

            var recipeDefinition = new RecipeDefinition("id", "version", "name",
                DeploymentTypes.CdkProject, DeploymentBundleTypes.Container, "template", "templateId", "description", "shortDescription", "targetService")
            {
                Validators =
                    allValidators
                        .Cast<RecipeValidatorList>()
                        .Select(validatorType =>
                            new RecipeValidatorConfig
                            {
                                ValidatorType = validatorType
                            }
                        )
                        .ToList()
            };

            // ACT
            var validators = recipeDefinition.BuildValidators();

            // ASSERT
            validators.Length.ShouldEqual(allValidators.Length);
        }

        /// <summary>
        /// Make sure we build correctly when coming from json
        /// </summary>
        [Fact]
        public void CanBuildRehydratedOptionSettingsItem()
        {
            // ARRANGE
            var expectedValidator = new RequiredValidator
            {
                ValidationFailedMessage = "Custom Test Message"
            };

            var optionSettingItem = new OptionSettingItem("id", "name", "description")
            {
                Name = "Test Item",
                Validators = new List<OptionSettingItemValidatorConfig>
                {
                    new OptionSettingItemValidatorConfig
                    {
                        ValidatorType = OptionSettingItemValidatorList.Required,
                        Configuration = expectedValidator
                    }
                }
            };

            var json = JsonConvert.SerializeObject(optionSettingItem, Formatting.Indented);

            var deserialized = JsonConvert.DeserializeObject<OptionSettingItem>(json);

            // ACT
            var validators = deserialized.BuildValidators();

            // ASSERT
            validators.Length.ShouldEqual(1);
            validators.First().ShouldBeType(expectedValidator.GetType());
            validators.OfType<RequiredValidator>().First().ValidationFailedMessage.ShouldEqual(expectedValidator.ValidationFailedMessage);
        }

        /// <summary>
        /// This tests captures the behavior of the system.  Requirements for this area are a little unclear
        /// and can be adjusted as needed.  This test is not meant to show 'ideal' behavior; only 'current'
        /// behavior.
        /// <para />
        /// This test behavior is dependent on using intermediary json.  If you just
        /// used a fully populated <see cref="OptionSettingItem"/>, this test would behave differently.
        /// Coming from json, <see cref="OptionSettingItemValidatorConfig.ValidatorType"/> wins,
        /// coming from object model <see cref="OptionSettingItemValidatorConfig.Configuration"/> wins.
        /// </summary>
        [Fact]
        public void WhenValidatorTypeAndConfigurationHaveAMismatchThenValidatorTypeWins()
        {
            // ARRANGE
            var optionSettingItem = new OptionSettingItem("id", "name", "description")
            {
                Name = "Test Item",
                Validators = new List<OptionSettingItemValidatorConfig>
                {
                    new OptionSettingItemValidatorConfig
                    {
                        ValidatorType = OptionSettingItemValidatorList.Regex,
                        // Required can only map to RequiredValidator, this setup doesn't make sense:
                        Configuration = new RangeValidator
                        {
                            Min = 1
                        }
                    }
                }
            };
            
            var json = JsonConvert.SerializeObject(optionSettingItem, Formatting.Indented);

            var deserialized = JsonConvert.DeserializeObject<OptionSettingItem>(json);

            Exception exception = null;
            IOptionSettingItemValidator[] validators = null;

            // ACT
            try
            {
                validators = deserialized.BuildValidators();
            }
            catch (Exception e)
            {
                exception = e;
            }

            // ASSERT
            exception.ShouldBeNull();

            // we have our built validator
            validators.ShouldNotBeNull();
            validators.Length.ShouldEqual(1);

            // things get a little odd, the type is correct,
            // but the output messages is going to be from the RangeValidator.
            validators.First().ShouldBeType<RegexValidator>();
            validators.OfType<RegexValidator>().First().ValidationFailedMessage.ShouldEqual(
                new RangeValidator().ValidationFailedMessage);
        }
    }
}
