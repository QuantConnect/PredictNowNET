/*
 * QUANTCONNECT.COM - Democratizing Finance, Empowering Individuals.
 * Lean Algorithmic Trading Engine v2.0. Copyright 2014 QuantConnect Corporation.
 *
 * Licensed under the Apache License, Version 2.0 (the "License");
 * you may not use this file except in compliance with the License.
 * You may obtain a copy of the License at http://www.apache.org/licenses/LICENSE-2.0
 *
 * Unless required by applicable law or agreed to in writing, software
 * distributed under the License is distributed on an "AS IS" BASIS,
 * WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied.
 * See the License for the specific language governing permissions and
 * limitations under the License.
*/

using Newtonsoft.Json;
using Python.Runtime;
using QuantConnect.Configuration;
using QuantConnect.PredictNowNET.Models;
using RestSharp;

namespace QuantConnect.PredictNowNET;

/// <summary>
/// REST Client for PredictNow CPO 
/// </summary>
public class PredictNowClient
{
    private readonly RestClient _client;
    private readonly string _userId;

    /// <summary>
    /// Creates a new instance of REST Client for PredictNow CPO for a given user 
    /// </summary>
    /// <param name="userId">User identification</param>
    /// <returns></returns>
    public static PredictNowClient CreateClient(string userId) 
    {
        var baseUrl = Config.Get("predict-now-url");
        return new PredictNowClient(baseUrl, userId);
    }

    /// <summary>
    /// Creates a new instance of the REST Client for PredictNow CPO 
    /// </summary>
    /// <param name="baseUrl">The base URL to PredictNow REST endpoints</param>
    /// <param name="userId">User identification</param>
    protected PredictNowClient(string baseUrl, string userId)
    {
        if (string.IsNullOrWhiteSpace(baseUrl))
        {
            throw new ArgumentNullException($"PredictNowClient: {nameof(baseUrl)} cannot be null, empty, or consists only of white-space characters.");
        }

        if (string.IsNullOrWhiteSpace(userId))
        {
            throw new ArgumentNullException($"PredictNowClient: {nameof(userId)} cannot be null, empty, or consists only of white-space characters.");
        }

        _userId = userId;
        _client = new RestClient(baseUrl);
    }

    /// <summary>
    /// Checks whether we can connect to the endpoint
    /// </summary>
    public bool Connected => _client.Execute(new RestRequest()).IsSuccessful;

    /// <summary>
    /// List all files with return information
    /// </summary>
    /// <returns>Array of string with the files names</returns>
    public string[] ListReturnsFiles() => ListFiles("return");

    /// <summary>
    /// List all files with constraint information
    /// </summary>
    /// <returns>Array of string with the files names</returns>
    public string[] ListConstraintFiles() => ListFiles("constraint");

    /// <summary>
    /// List all files with feature information
    /// </summary>
    /// <returns>Array of string with the files names</returns>
    public string[] ListFeaturesFiles() => ListFiles("feature");

    /// <summary>
    /// Uploads one Returns file
    /// </summary>
    /// <param name="filename">Absolute file path</param>
    /// <returns>String with information about the file</returns>
    public string UploadReturnsFile(string filename) => UploadFile(filename, "Returns");

    /// <summary>
    /// Uploads one Constraint file
    /// <param name="filename">Absolute file path</param>
    /// </summary>
    /// <returns>String with information about the file</returns>
    public string UploadConstraintFile(string filename) => UploadFile(filename, "Constraint");

    /// <summary>
    /// Uploads one Constraint file
    /// </summary>
    /// <param name="filename">Absolute file path</param>
    /// <returns>String with information about the file</returns>
    public string UploadFeaturesFile(string filename) => UploadFile(filename, "features");

    /// <summary>
    /// Creates a job to run a in-sample backtest
    /// </summary>
    /// <param name="portfolioParameters">Portfolio parameters</param>
    /// <param name="trainingStartDate">Start date of the first rebalancing period to be included in the experiment.</param>
    /// <param name="trainingEndDate">The experiment terminates when the start of the period exceed the training end date.</param>
    /// <param name="samplingProportion">Float between 0 and 1, the fraction of base strategies to be kept. This parameter is usually set to 0.3 or 0.4.</param>
    /// <param name="debug">Will output more information in the backend when set to `debug`, and will not affect the performance or prediction.</param>
    /// <returns>Tuple of string. The first contains the message on the job submission, and the second the job id.</returns>
    public JobCreationResult RunInSampleBacktest(PortfolioParameters portfolioParameters, DateTime trainingStartDate, DateTime trainingEndDate, double samplingProportion, string? debug = null)
    {
        return RunBacktest("run-insample-backtest", portfolioParameters, trainingStartDate, trainingEndDate, samplingProportion, debug);
    }

    /// <summary>
    /// Creates a job to run a out-of-sample backtest
    /// </summary>
    /// <param name="portfolioParameters">Portfolio parameters</param>
    /// <param name="trainingStartDate">Start date of the first rebalancing period to be included in the experiment.</param>
    /// <param name="trainingEndDate">The experiment terminates when the start of the period exceed the training end date.</param>
    /// <param name="debug">Will output more information in the backend when set to `debug`, and will not affect the performance or prediction.</param>
    /// <returns>Tuple of string. The first contains the message on the job submission, and the second the job id.</returns>
    public JobCreationResult RunOutOfSampleBacktest(PortfolioParameters portfolioParameters, DateTime trainingStartDate, DateTime trainingEndDate, string? debug = null)
    {
        return RunBacktest("run-oos-backtest", portfolioParameters, trainingStartDate, trainingEndDate, null, debug);
    }

    /// <summary>
    /// Creates a job to run a live prediction
    /// </summary>
    /// <param name="portfolioParameters">Portfolio parameters</param>
    /// <param name="rebalanceDate">The target rebalance date.</param>
    /// <param name="nextRebalanceDate">The next rebalance date after current target date.</param>
    /// <param name="marketDays">The number of market days in the incoming rebalancing period. </param>
    /// <param name="debug">Will output more information in the backend when set to `debug`, and will not affect the performance or prediction.</param>
    /// <returns>Tuple of string. The first contains the message on the job submission, and the second the job id.</returns>
    public JobCreationResult RunLivePrediction(PortfolioParameters portfolioParameters, DateTime rebalanceDate, DateTime nextRebalanceDate, int? marketDays = null, string? debug = null)
    {
        portfolioParameters.SetUserId(_userId);
        var livePredictionParameters = new LivePredictionParameters(portfolioParameters, rebalanceDate, nextRebalanceDate, marketDays, debug);
        var value = JsonConvert.SerializeObject(livePredictionParameters);

        var request = new RestRequest("run-live-prediction", Method.POST) { RequestFormat = DataFormat.Json };
        request.AddParameter("application/json", value, ParameterType.RequestBody);

        var response = _client.Execute(request);
        if (string.IsNullOrEmpty(response?.Content)) return JobCreationResult.Null;

        return JsonConvert.DeserializeObject<JobCreationResult>(response.Content) ?? JobCreationResult.Null;
    }

    /// <summary>
    /// Get the job for a given id from in-sample and out-of-sample backtests and live prediction
    /// </summary>
    /// <param name="jobId">The id of the job.</param>
    /// <returns>The Job object with current information</returns>
    public Job GetJobForId(string jobId)
    {
        var request = new RestRequest($"get-cpo-job-status/{jobId}");
        var response = _client.Execute(request);

        var result = JsonConvert.DeserializeObject<Job>(response?.Content ?? string.Empty);
        return result ?? Job.Null;
    }

    /// <summary>
    /// Get the backtest performance
    /// </summary>
    /// <param name="portfolioParameters">Portfolio parameters</param>
    /// <param name="trainingStartDate">Start date of the first rebalancing period to be included in the experiment.</param>
    /// <param name="trainingEndDate">The experiment terminates when the start of the period exceed the training end date.</param>
    /// <param name="debug">Will output more information in the backend when set to `debug`, and will not affect the performance or prediction.</param>
    /// <returns>Performance object with metrics of the backtest</returns>
    public Performance GetBacktestPerformance(PortfolioParameters portfolioParameters, DateTime trainingStartDate, DateTime trainingEndDate, string? debug = null)
    {
        var value = GetBacktestResults("get-backtest-performance", portfolioParameters, trainingStartDate, trainingEndDate, debug);
        var result = JsonConvert.DeserializeObject<Performance>(value);

        return result ?? Performance.Null;
    }

    /// <summary>
    /// Get the backtest weights
    /// </summary>
    /// <param name="portfolioParameters">Portfolio parameters</param>
    /// <param name="trainingStartDate">Start date of the first rebalancing period to be included in the experiment.</param>
    /// <param name="trainingEndDate">The experiment terminates when the start of the period exceed the training end date.</param>
    /// <param name="debug">Will output more information in the backend when set to `debug`, and will not affect the performance or prediction.</param>
    /// <returns>Dictionary of weights ordered keyed by date</returns>
    public Dictionary<DateTime, Dictionary<string, double>> GetBacktestWeights(PortfolioParameters portfolioParameters, DateTime trainingStartDate, DateTime trainingEndDate, string? debug = null)
    {
        var value = GetBacktestResults("get-backtest-weights", portfolioParameters, trainingStartDate, trainingEndDate, debug);
        var result = JsonConvert.DeserializeObject<Dictionary<DateTime, Dictionary<string, double>>>(value);

        return result ?? new Dictionary<DateTime, Dictionary<string, double>>();
    }

    /// <summary>
    /// Get the backtest weights
    /// </summary>
    /// <param name="portfolioParameters">Portfolio parameters</param>
    /// <param name="trainingStartDate">Start date of the first rebalancing period to be included in the experiment.</param>
    /// <param name="trainingEndDate">The experiment terminates when the start of the period exceed the training end date.</param>
    /// <param name="debug">Will output more information in the backend when set to `debug`, and will not affect the performance or prediction.</param>
    /// <returns>Dictionary of weights ordered keyed by date</returns>
    public PyDict GetBacktestWeights(PyObject portfolioParameters, DateTime trainingStartDate, DateTime trainingEndDate, string? debug = null)
    {
        var weights = GetBacktestWeights(portfolioParameters.As<PortfolioParameters>(), trainingStartDate, trainingEndDate, debug);
        return ConvertCSharpDictionaryToPythonDict(weights);
    }

    /// <summary>
    /// Get the live prediction weights
    /// </summary>
    /// <param name="portfolioParameters">Portfolio parameters</param>
    /// <param name="rebalanceDate">The target rebalance date.</param>
    /// <param name="marketDays">The number of market days in the incoming rebalancing period. </param>
    /// <param name="debug">Will output more information in the backend when set to `debug`, and will not affect the performance or prediction.</param>
    /// <returns>Dictionary of weights ordered keyed by date</returns>
    public Dictionary<DateTime, Dictionary<string, double>> GetLivePredictionWeights(PortfolioParameters portfolioParameters, DateTime rebalanceDate, int? marketDays = null, string? debug = null)
    {
        portfolioParameters.SetUserId(_userId);
        var livePredictionParameters = new LivePredictionParameters(portfolioParameters, rebalanceDate, null, marketDays, debug);
        var value = JsonConvert.SerializeObject(livePredictionParameters);

        var request = new RestRequest("get-live-prediction-weights") { RequestFormat = DataFormat.Json };
        request.AddParameter("application/json", value, ParameterType.RequestBody);

        var response = _client.Execute(request);

        var result = JsonConvert.DeserializeObject<Dictionary<DateTime, Dictionary<string, double>>>(response?.Content ?? string.Empty);

        return result ?? new Dictionary<DateTime, Dictionary<string, double>>();
    }

    /// <summary>
    /// Get the live prediction weights
    /// </summary>
    /// <param name="portfolioParameters">Portfolio parameters</param>
    /// <param name="rebalanceDate">The target rebalance date.</param>
    /// <param name="marketDays">The number of market days in the incoming rebalancing period. </param>
    /// <param name="debug">Will output more information in the backend when set to `debug`, and will not affect the performance or prediction.</param>
    /// <returns>Dictionary of weights ordered keyed by date</returns>
    public PyDict GetLivePredictionWeights(PyObject portfolioParameters, DateTime rebalanceDate, int? marketDays = null, string? debug = null)
    {
        var weights = GetLivePredictionWeights(portfolioParameters.As<PortfolioParameters>(), rebalanceDate, marketDays, debug);
        return ConvertCSharpDictionaryToPythonDict(weights);
    }

    /// <summary>
    /// Get the backtest results. Weight or Performance.
    /// </summary>
    /// <param name="type">Type of result: weight or performance</param>
    /// <param name="portfolioParameters">Portfolio parameters</param>
    /// <param name="trainingStartDate">Start date of the first rebalancing period to be included in the experiment.</param>
    /// <param name="trainingEndDate">The experiment terminates when the start of the period exceed the training end date.</param>
    /// <param name="debug">Will output more information in the backend when set to `debug`, and will not affect the performance or prediction.</param>
    /// <returns>String to be deserialized</returns>
    private string GetBacktestResults(string type, PortfolioParameters portfolioParameters, DateTime trainingStartDate, DateTime trainingEndDate, string? debug = null)
    {
        portfolioParameters.SetUserId(_userId);
        var backtestParameters = new BacktestParameters(portfolioParameters, trainingStartDate, trainingEndDate, null, debug);
        var value = JsonConvert.SerializeObject(backtestParameters);

        var request = new RestRequest(type) { RequestFormat = DataFormat.Json };
        request.AddParameter("application/json", value, ParameterType.RequestBody);

        var response = _client.Execute(request);
        
        return response?.Content ?? string.Empty;
    }

    /// <summary>
    /// Uploads a given file to its type: returns, contraits or features
    /// </summary>
    /// <param name="filename">Absolute file path</param>
    /// <param name="type">Type of file content: returns, contraits or features</param>
    /// <returns>String with information about the file</returns>
    private string UploadFile(string filename, string type)
    {
        var fileInfo = new FileInfo(filename);
        if (!fileInfo.Exists)
        {
            return $"{fileInfo.FullName} does not exist";
        }

        var request = new RestRequest("upload-data", Method.POST) { RequestFormat = DataFormat.Json };
        request.AddHeader("Content-Type", "multipart/form-data");
        request.AddParameter("email", _userId);
        request.AddParameter("type", type);
        request.AddFile("file", File.ReadAllBytes(filename), fileInfo.Name);

        var response = _client.Execute(request);

        var value = response?.Content ?? string.Empty;
        var deserializedContent = JsonConvert.DeserializeObject<Dictionary<string, string>>(value);
        if (deserializedContent?.TryGetValue("message", out var message) ?? false)
        {
            return message;
        }

        return $"The content of the response is invalid: {value}";
    }

    /// <summary>
    /// List all files of a given type: returns, contraits or features
    /// </summary>
    /// <param name="type">Type of file content: returns, contraits or features</param>
    /// <returns>Array of string with the files names</returns>
    private string[] ListFiles(string type)
    {
        var request = new RestRequest($"list-{type}-files/{_userId}");

        var response = _client.Execute(request);

        var value = response?.Content ?? string.Empty;
        var deserializedContent = JsonConvert.DeserializeObject<Dictionary<string, List<string>>>(value);
        if (deserializedContent?.Values == null)
        {
            return Array.Empty<string>();
        }

        return deserializedContent.Values.SelectMany(x => x).ToArray();
    }

    /// <summary>
    /// Creates a job to run a backtest
    /// </summary>
    /// <param name="resource">Name of the endpoint resource</param>
    /// <param name="portfolioParameters">Portfolio parameters</param>
    /// <param name="trainingStartDate">Start date of the first rebalancing period to be included in the experiment.</param>
    /// <param name="trainingEndDate">The experiment terminates when the start of the period exceed the training end date.</param>
    /// <param name="samplingProportion">Float between 0 and 1, the fraction of base strategies to be kept. This parameter is usually set to 0.3 or 0.4.</param>
    /// <param name="debug">Will output more information in the backend when set to `debug`, and will not affect the performance or prediction.</param>
    /// <returns>Tuple of string. The first contains the message on the job submission, and the second the job id.</returns>
    private JobCreationResult RunBacktest(string resource, PortfolioParameters portfolioParameters,
        DateTime trainingStartDate, DateTime trainingEndDate, double? samplingProportion = null, string? debug = null)
    {
        portfolioParameters.SetUserId(_userId);
        var backtestParameters = new BacktestParameters(portfolioParameters, trainingStartDate, trainingEndDate, samplingProportion, debug);
        var value = JsonConvert.SerializeObject(backtestParameters);

        var request = new RestRequest(resource, Method.POST) { RequestFormat = DataFormat.Json };
        request.AddParameter("application/json", value, ParameterType.RequestBody);

        var response = _client.Execute(request);
        if (string.IsNullOrEmpty(response?.Content)) return JobCreationResult.Null;

        return JsonConvert.DeserializeObject<JobCreationResult>(response.Content) ?? JobCreationResult.Null;
    }

    /// <summary>
    /// Converts C# Dictionary to PyDict
    /// </summary>
    /// <param name="keyValuePairs">C# Dictionary</param>
    /// <returns>Python Dictionary</returns>
    private PyDict ConvertCSharpDictionaryToPythonDict(Dictionary<DateTime, Dictionary<string, double>> keyValuePairs)
    {
        PyDict result = new();
        foreach (var kvp in keyValuePairs)
        {
            var innerDict = new PyDict();

            foreach (var pair in kvp.Value)
            {
                innerDict.SetItem(pair.Key, pair.Value.ToPython());
            }
            result.SetItem(kvp.Key.ToPython(), innerDict);

        }
        return result;
    }
}