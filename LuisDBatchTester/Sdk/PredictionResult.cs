// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using System;
using System.Collections.Generic;
using System.Linq;

namespace LuisPredict.Sdk
{
    /// <summary>
    /// Represents a prediction result.
    /// </summary>
    public class PredictionResult
    {
        /// <summary>
        /// Gets the names of the classifiers which are predicted positive.
        /// </summary>
        public IReadOnlyList<string> PositiveClassifiers { get; }

        /// <summary>
        /// Gets the score for each classifier, if known.
        /// </summary>
        public IReadOnlyDictionary<string, float> ClassifierScores { get; }

        /// <summary>
        /// Gets the extracted text.
        /// </summary>
        public IReadOnlyList<ExtractionInstance> Extractions { get; }

        private PredictionResult(IEnumerable<string> positiveClassifiers, IReadOnlyDictionary<string, float> classifierScores, IEnumerable<ExtractionInstance> extractions)
        {
            PositiveClassifiers = positiveClassifiers?.ToArray() ?? Array.Empty<string>();
            ClassifierScores = classifierScores?.ToDictionary(v => v.Key, v => v.Value) ?? new Dictionary<string, float>();
            Extractions = extractions?.ToArray() ?? Array.Empty<ExtractionInstance>();
        }

        /// <summary>
        /// Creates a <see cref="PredictionResult"/>, initialized with the specified values.
        /// </summary>
        /// <param name="positiveClassifiers">Name of the predicted positive classifiers.</param>
        /// <param name="classifierScores">Score for each classifier.</param>
        /// <param name="extractions">Extracted text.</param>
        public static PredictionResult FromValues(IEnumerable<string> positiveClassifiers = null, IReadOnlyDictionary<string, float> classifierScores = null, IEnumerable<ExtractionInstance> extractions = null)
        {
            return new PredictionResult(positiveClassifiers, classifierScores, extractions);
        }
    }
}
