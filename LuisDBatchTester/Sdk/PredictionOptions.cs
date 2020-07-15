// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

namespace LuisPredict.Sdk
{
    /// <summary>
    /// Specifies prediction options.
    /// </summary>
    public class PredictionOptions
    {
        /// <summary>
        /// Gets or sets a value indicating whether or not to include the classifier scores in the prediction result.
        /// </summary>
        public bool IncludeClassifierScores { get; set; } = true;

        /// <summary>
        /// Gets or sets a value indicating whether or not to include verbose extraction information in the prediction result.
        /// </summary>
        public bool IncludeVerboseExtractionInformation { get; set; } = true;

        /// <summary>
        /// Gets or sets a value indicating whether or not the query should be retained by the server for future training.
        /// </summary>
        public bool LogQuery { get; set; } = false;
    }
}
