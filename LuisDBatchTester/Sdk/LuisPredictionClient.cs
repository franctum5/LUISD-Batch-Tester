// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;

namespace LuisPredict.Sdk
{
    /// <summary>
    /// Client for using a published LUIS model to perform prediction.
    /// </summary>
    public class LuisPredictionClient : IDisposable
    {
        private static readonly JsonSerializerOptions _serializerOptions = new JsonSerializerOptions { PropertyNamingPolicy = JsonNamingPolicy.CamelCase };
        private static readonly HttpClient _client = new HttpClient();
        private readonly Uri _endpointBaseUri;

        /// <summary>
        /// Gets or sets the polling interval when waiting for an operation to complete.
        /// </summary>
        public TimeSpan PollingInterval { get; set; } = TimeSpan.FromSeconds(1);

        /// <summary>
        /// Creates a new instance of <see cref="LuisPredictionClient"/>.
        /// </summary>
        /// <param name="endpointBaseUri">Base URI of the LUIS endpoint to use (e.g., https://westus.api.cognitive.microsoft.com/ ).</param>
        /// <param name="predictionKey">Prediction key for the LUIS endpoint.</param>
        public LuisPredictionClient(Uri endpointBaseUri, string predictionKey)
        {
            // Validate arguments.
            if (endpointBaseUri == null)
            {
                throw new ArgumentNullException(nameof(endpointBaseUri));
            }
            if (predictionKey == null)
            {
                throw new ArgumentNullException(nameof(predictionKey));
            }

            // Create HTTP client and add authentication header.
            _client.DefaultRequestHeaders.Add("Ocp-Apim-Subscription-Key", predictionKey);
            _client.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
            _endpointBaseUri = endpointBaseUri;
        }

        /// <summary>
        /// Converts the specified file to text. The conversion API supports the following extensions: .pdf, .docx, .pptx, .eml, .msg, .html.
        /// </summary>
        /// <param name="filePath">Path to the file to convert.</param>
        /// <param name="cancellation">Optional cancellation token.</param>
        /// <returns></returns>
        public async Task<IReadOnlyList<string>> ConvertToTextAsync(string filePath, CancellationToken cancellation = default)
        {
            // Validate argument.
            if (filePath == null)
            {
                throw new ArgumentNullException(nameof(filePath));
            }

            // Start convert operation.
            Uri operationUri;
            using (var stream = File.OpenRead(filePath))
            using (var content = new MultipartFormDataContent())
            using (var streamContent = new StreamContent(stream))
            {
                // Request includes the contents of the file as well as its filename (without path). The extension of the file
                // is used by the server to determine the file type.
                var fileName = Path.GetFileName(filePath);
                content.Add(streamContent, "document", fileName);

                // Start convert operation.
                var convertUri = new Uri(_endpointBaseUri, "./luis/prediction/v4.0-preview/documents/convert");
                var (_, headers) = await DoHttpPostAsync<NoResponse>(convertUri, content, cancellation).ConfigureAwait(false);

                // Parse result.
                operationUri = GetOperationLocation(headers);
            }

            // Wait for operation to complete and read result.
            var result = await WaitForOperationAndReadResultAsync<ConvertResponse>(operationUri, cancellation).ConfigureAwait(false);

            // Result is actually JSON, so reinterpret as JSON and return.
            var text = JsonSerializer.Deserialize<string[]>(result.DocumentText, _serializerOptions);
            return text;
        }

        /// <summary>
        /// Uses the specified model to perform prediction on text.
        /// </summary>
        /// <param name="text">Text on which to perform prediction.</param>
        /// <param name="appId">App ID of the application with the published model.</param>
        /// <param name="publishSlot">Slot of the published model.</param>
        /// <param name="options">Optional prediction options.</param>
        /// <param name="cancellation">Optional cancellation token.</param>
        /// <returns></returns>
        public async Task<PredictionResult> PredictAsync(string text, Guid appId, PublishSlot publishSlot, PredictionOptions options = default, CancellationToken cancellation = default)
        {
            // Validate argument.
            if (text == null)
            {
                throw new ArgumentNullException(nameof(text));
            }

            // Determine URI to published model.
            options ??= new PredictionOptions();
            var appPart = Uri.EscapeUriString(appId.ToString());
            var slotPart = Uri.EscapeUriString(publishSlot.ToString().ToLowerInvariant());
            var expandPart = Uri.EscapeUriString(GetExpandParameters(options).ToLowerInvariant());
            var logPart = Uri.EscapeUriString(options.LogQuery.ToString().ToLowerInvariant());
            var predictUri = new Uri(_endpointBaseUri, $"./luis/prediction/v4.0-preview/documents/apps/{appPart}/slots/{slotPart}/predictText?$expand={expandPart}&log={logPart}");

            // Start predict operation.
            var request = new PredictRequest {Query = text};
            var (_, headers) = await DoHttpPostAsync<PredictRequest, NoResponse>(predictUri, request, cancellation).ConfigureAwait(false);

            // Parse result.
            var operationUri = GetOperationLocation(headers);

            // Wait for operation to complete and read result.
            var result = await WaitForOperationAndReadResultAsync<PredictResponse>(operationUri, cancellation).ConfigureAwait(false);

            // Convert result to a friendlier form.
            return ConvertPredictResponseToResult(result);
        }

        /// <inheritdoc/>
        public void Dispose()
        {
            _client.Dispose();
        }

        #region Helper functions for HTTP request/response handling
        private async Task<T> WaitForOperationAndReadResultAsync<T>(Uri operationUri, CancellationToken cancellation)
        {
            // Polling loop to check when operation completes.
            Uri resultUri;
            while (true)
            {
                // Get latest operation status.
                var (status, headers) = await DoHttpGetAsync<OperationStatusResponse>(operationUri, cancellation).ConfigureAwait(false);

                // Parse response.
                bool success = false;
                switch (status.Status.ToLowerInvariant())
                {
                    case "notstarted":
                    case "running":
                        break;
                    case "succeeded":
                        success = true;
                        break;
                    default:
                        throw new InvalidOperationException("Conversion operation failed");
                }
                if (success)
                {
                    // Once the status returned "succeeded", then the header specifies the location of the operation result.
                    resultUri = GetOperationLocation(headers);
                    break;
                }

                // If not done or failed, then wait a bit and poll again.
                await Task.Delay(PollingInterval, cancellation).ConfigureAwait(false);
            }

            // Read the final result.
            var (result, _) = await DoHttpGetAsync<T>(resultUri, cancellation).ConfigureAwait(false);
            return result;
        }

        private async Task<(T Body, HttpResponseHeaders Headers)> DoHttpGetAsync<T>(Uri uri, CancellationToken cancellation)
        {
            // Perform HTTP GET call.
            using var response = await _client.GetAsync(uri, cancellation).ConfigureAwait(false);

            // Read HTTP response.
            string body = "";
            if (response.Content != null)
            {
                body = await response.Content.ReadAsStringAsync().ConfigureAwait(false);
            }

            // Verify response reports success, throwing if not.
            if (!response.IsSuccessStatusCode)
            {
                var message = $"HTTP GET to {uri} failed with status {response.StatusCode} ({response.ReasonPhrase})";
                if (body.Length > 0)
                {
                    message += $":{Environment.NewLine}{body}";
                }
                throw new InvalidOperationException(message);
            }

            // Deserialize response to expected type.
            var deserialized = JsonSerializer.Deserialize<T>(body, _serializerOptions);
            return (deserialized, response.Headers);
        }

        private async Task<(TResponse Body, HttpResponseHeaders Headers)> DoHttpPostAsync<TRequest, TResponse>(Uri uri, TRequest request, CancellationToken cancellation)
        {
            // Serialize request.
            var serialized = JsonSerializer.SerializeToUtf8Bytes(request, _serializerOptions);

            // Send request.
            using var content = new ByteArrayContent(serialized);
            return await DoHttpPostAsync<TResponse>(uri, content, cancellation).ConfigureAwait(false);
        }

        private async Task<(T Body, HttpResponseHeaders Headers)> DoHttpPostAsync<T>(Uri uri, HttpContent request, CancellationToken cancellation)
        {
            // Perform HTTP POST call.
            using var response = await _client.PostAsync(uri, request, cancellation).ConfigureAwait(false);

            // Read HTTP response.
            string body = "";
            if (response.Content != null)
            {
                body = await response.Content.ReadAsStringAsync().ConfigureAwait(false);
            }

            // Verify response reports success, throwing if not.
            if (!response.IsSuccessStatusCode)
            {
                var message = $"HTTP POST to {uri} failed with status {response.StatusCode} ({response.ReasonPhrase})";
                if (body.Length > 0)
                {
                    message += $":{Environment.NewLine}{body}";
                }
                throw new InvalidOperationException(message);
            }

            // Deserialize response to expected type.
            var deserialized = JsonSerializer.Deserialize<T>(body, _serializerOptions);
            return (deserialized, response.Headers);
        }

        private static Uri GetOperationLocation(HttpResponseHeaders headers)
        {
            // The "operation-location" header is used in responses to both indicate the location of a running operation
            // as well as an operation's final result.
            var location = headers.GetValues("Operation-location").Single();
            return new Uri(location);
        }

        private static string GetExpandParameters(PredictionOptions options)
        {
            if (options.IncludeClassifierScores && options.IncludeVerboseExtractionInformation)
            {
                return "classifier,extractor";
            }
            else if (options.IncludeClassifierScores)
            {
                return "classifier";
            }
            else if (options.IncludeVerboseExtractionInformation)
            {
                return "extractor";
            }
            return "";
        }
        #endregion Helper functions for HTTP request/response handling

        #region Parse prediction result
        private static PredictionResult ConvertPredictResponseToResult(PredictResponse response)
        {
            var positiveClassifiers = response.Prediction.PositiveClassifiers;
            var classifierScores = response.Prediction.Classifiers
                ?.Where(v => v.Value.Score.HasValue)
                .ToDictionary(v => v.Key, v => v.Value.Score ?? 0);
            var extractions = ProcessEntities(response.Prediction.Extractors);
            return PredictionResult.FromValues(positiveClassifiers, classifierScores, extractions);
        }

        private static IReadOnlyList<ExtractionInstance> ProcessEntities(JsonElement rawEntities)
        {
            // Verify not empty.
            if (rawEntities.ValueKind != JsonValueKind.Object)
            {
                return Array.Empty<ExtractionInstance>();
            }

            // Loop through each entity.
            var entityInstances = new Dictionary<string, List<ExtractionInstance>>();
            foreach (var rawEntity in rawEntities.EnumerateObject().Where(v => !v.NameEquals("$instance")))
            {
                // Loop through each instance of entity.
                var entityName = rawEntity.Name;
                var currEntityInstances = new List<ExtractionInstance>();
                foreach (var rawInstance in rawEntity.Value.EnumerateArray())
                {
                    if (rawInstance.ValueKind == JsonValueKind.Object)
                    {
                        // If current element is an object, then it contains a list of child entities, so extract recursively.
                        var subEntities = ProcessEntities(rawInstance);
                        var instance = ExtractionInstance.FromValues(entityName, null, null, subEntities);
                        currEntityInstances.Add(instance);
                    }
                    else if (rawInstance.ValueKind == JsonValueKind.String)
                    {
                        // If current element is text, then it is a leaf entity, so remember extracted text.
                        var text = rawInstance.GetString();
                        var instance = ExtractionInstance.FromValues(entityName, text, null);
                        currEntityInstances.Add(instance);
                    }
                }
                entityInstances[entityName] = currEntityInstances;
            }

            // The loop above provides just the text of leaf entities. If verbose information is present, it adds missing
            // information about position and non-left text to the entities extracted above.
            if (rawEntities.TryGetProperty("$instance", out var rawVerboseEntities))
            {
                // Loop through each entity.
                foreach (var rawVerboseEntity in rawVerboseEntities.EnumerateObject())
                {
                    // The verbose information should match 1-to-1 to the entities above, so verify length is identical.
                    var entityName = rawVerboseEntity.Name;
                    var rawVerboseInstances = rawVerboseEntity.Value;
                    if (entityInstances.TryGetValue(entityName, out var currEntityInstances) &&
                        currEntityInstances.Count == rawVerboseInstances.GetArrayLength())
                    {
                        // Loop through each instance of entity and add information provided by the verbose information.
                        for (int i = 0; i < currEntityInstances.Count; i++)
                        {
                            var rawVerboseInstance = rawVerboseInstances[i];
                            var text = rawVerboseInstance.GetProperty("text").GetString();
                            var position = rawVerboseInstance.GetProperty("startIndex").GetInt32();
                            var originalInstance = currEntityInstances[i];
                            currEntityInstances[i] = ExtractionInstance.FromValues(entityName, text, position, originalInstance.Children);
                        }
                    }
                }
            }

            // Flatten list of instances.
            return entityInstances.SelectMany(v => v.Value).ToList();
        }
        #endregion Parse prediction result

        #region Request/Response Models
        // The classes listed below are used to serialize/deserialize the JSON request/responses from the HTTP calls.
        // ReSharper disable ClassNeverInstantiated.Local
        // ReSharper disable UnusedAutoPropertyAccessor.Local
        private class PredictRequest
        {
            public string Query { get; set; }
        }

        private class NoResponse
        {
        }

        private class ConvertResponse
        {
            public string DocumentText { get; set; }
        }

        private class OperationStatusResponse
        {
            public string Status { get; set; }
        }

        private class ClassifierResponse
        {
            public float? Score { get; set; }
        }

        private class PredictionResponse
        {
            public IReadOnlyList<string> PositiveClassifiers { get; set; }
            public IReadOnlyDictionary<string, ClassifierResponse> Classifiers { get; set; }
            public JsonElement Extractors { get; set; }
        }

        private class PredictResponse
        {
            public PredictionResponse Prediction { get; set; }
        }
        // ReSharper restore ClassNeverInstantiated.Local
        // ReSharper restore UnusedAutoPropertyAccessor.Local
        #endregion Request/Response Models
    }
}
