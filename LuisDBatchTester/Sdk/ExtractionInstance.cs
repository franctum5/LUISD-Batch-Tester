// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using System;
using System.Collections.Generic;
using System.Linq;

namespace LuisPredict.Sdk
{
    /// <summary>
    /// Represents an instance extracted from the prediction text, which may also contain child extraction instances.
    /// </summary>
    public class ExtractionInstance
    {
        /// <summary>
        /// Gets the name of the entity for this extraction.
        /// </summary>
        public string EntityName { get; }

        /// <summary>
        /// Gets the extracted text, if known.
        /// </summary>
        public string Text { get; }

        /// <summary>
        /// Gets the starting position of the extracted text in the prediction text, if known.
        /// </summary>
        public int? Position { get; }

        /// <summary>
        /// Gets the list of child extraction instances within this instance.
        /// </summary>
        public IReadOnlyList<ExtractionInstance> Children { get; }

        private ExtractionInstance(string entityName, string text, int? position, IEnumerable<ExtractionInstance> children)
        {
            EntityName = entityName;
            Text = text;
            Position = position;
            Children = children?.ToArray() ?? Array.Empty<ExtractionInstance>();
        }

        /// <inheritdoc/>
        public override string ToString()
        {
            return $"[EntityName: {EntityName}, Text: {Text}, Position: {Position}, Children: {Children.Count} children]";
        }

        /// <summary>
        /// Creates an <see cref="ExtractionInstance"/>, initialized with the specified values.
        /// </summary>
        /// <param name="entityName">Name of the entity for this extraction.</param>
        /// <param name="text">Extracted text.</param>
        /// <param name="position">Starting position of the extracted text in the prediction text.</param>
        /// <param name="children">List of child extraction instances within this instance.</param>
        /// <returns></returns>
        public static ExtractionInstance FromValues(string entityName, string text, int? position, IEnumerable<ExtractionInstance> children = null)
        {
            return new ExtractionInstance(entityName, text, position, children);
        }
    }
}
