using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Configuration;
using PrototypeLib;
using NLog.Extensions.Logging;
using Newtonsoft.Json;
using System.Collections.Concurrent;


namespace Prototype
{
    class Program
    {
        public static AppSettings appSettings { get; private set; }
        public static ServiceProvider serviceProvider { get; private set; }

        private static ILogger logger;

        static async Task Main(string[] args)
        {
            var appsettingsJson = "appsettings.json";
            Console.WriteLine($"Loading {appsettingsJson}...");
            if (!System.IO.File.Exists(appsettingsJson))
            {
                Console.WriteLine("Configuration file not found !");
                return;
            }
            var configurationBuilder = new ConfigurationBuilder()
                .SetBasePath(System.IO.Directory.GetCurrentDirectory())
                .AddJsonFile(appsettingsJson);

            var conf = configurationBuilder.Build();
            appSettings = conf.GetSection("App").Get<AppSettings>();

            //setup our DI
            serviceProvider = new ServiceCollection()
                .AddSingleton<IAlgo, Algo>()
                .AddSingleton<ILoggerFactory, LoggerFactory>()
                .AddSingleton(typeof(ILogger<>), typeof(Logger<>))
                .AddTransient<F23.StringSimilarity.Interfaces.IStringSimilarity>(s => new F23.StringSimilarity.Cosine())
                .AddTransient<IAggregation, Aggregation>()
                .BuildServiceProvider();


            var loggerFactory = serviceProvider.GetRequiredService<ILoggerFactory>();
            //configure NLog
            loggerFactory.AddNLog(new NLogProviderOptions { CaptureMessageTemplates = true, CaptureMessageProperties = true });
            loggerFactory.ConfigureNLog("nlog.config");

            logger = serviceProvider.GetService<ILogger<Program>>();
            var sw = new System.Diagnostics.Stopwatch();
            sw.Start();
            logger.LogInformation("Start !");
            logger.LogInformation($"Algo version : {serviceProvider.GetService<IAlgo>().GetType().Name}");
            try
            {
                await MainAlgo();
            }
            catch (System.Exception e)
            {
                logger.LogCritical(e.Message);
                logger.LogCritical(e.StackTrace);
                logger.LogCritical(e.ToString());
            }

            sw.Stop();
            logger.LogInformation($"All done in {(sw.Elapsed.Days > 0 ? sw.Elapsed.Days + " day(s) " : string.Empty) }{sw.Elapsed.Hours}:{sw.Elapsed.Minutes}:{sw.Elapsed.Seconds} !");
        }


        static async Task MainAlgo()
        {

            logger.LogInformation($"Starting");
            var timestamp = DateTime.Now.ToString("yyyyMMddHHmmss");
            var dir = appSettings.defaultDirectory;
            var sourceTargetCouples = appSettings.SourceTargetCouples.Where(c => c.Active).Select(couple => couple);
            foreach (var sourceTarget in sourceTargetCouples)
            {
                logger.LogInformation($"Batch : {sourceTarget.Name}");
                var sparqlQueryable1 = SparqlQueryable.Create(string.IsNullOrWhiteSpace(sourceTarget.endpointUri1) ? sourceTarget.rdfFile1 : sourceTarget.endpointUri1, sourceTarget.namedGraph1);
                var sparqlQueryable2 = SparqlQueryable.Create(string.IsNullOrWhiteSpace(sourceTarget.endpointUri2) ? sourceTarget.rdfFile2 : sourceTarget.endpointUri2, sourceTarget.namedGraph2);

                var similarityResults = new BlockingCollection<SimilarityResult>();
                var file = System.IO.Path.Combine(dir, $"{sourceTarget.Name}.json");
                var sourceSubjects = new List<string>();
                var targetSubjects = new List<string>();
                IAlgo algo = null;
                if (!appSettings.ForceSimilarityComputation && System.IO.File.Exists(file))
                {
                    logger.LogInformation($"opening file : {file}");
                    var similarityResultsArray = await GetSimilarityResults(file);
                    foreach (var item in similarityResultsArray)
                    {
                        similarityResults.Add(item);
                    }
                    sourceSubjects.AddRange(similarityResults.Select(sr => sr.Uri1).Distinct());
                    targetSubjects.AddRange(similarityResults.Select(sr => sr.Uri2).Distinct());
                }
                else
                {
                    algo = await Setup(sourceTarget.endpointUri1, sourceTarget.endpointUri2, sourceTarget.rdfFile1, sourceTarget.rdfFile2, sourceTarget.namedGraph1, sourceTarget.namedGraph2, sourceTarget.classesToSelect1, sourceTarget.classesToSelect2);
                    if (algo == null) return;
                    var (subs1, subs2) = algo.GetSubjects();
                    sourceSubjects.AddRange(subs1);

                    targetSubjects.AddRange(subs2);
                    var numberOfSimilarityComputation = (double)sourceSubjects.Count * targetSubjects.Count;
                    logger.LogInformation($"{numberOfSimilarityComputation} to process...");
                    var count = 0d;
                    var pourcentage = 0;

                    // parallel loop on instances from the source
                    await Task.Run(() => Parallel.ForEach(sourceSubjects, s1 =>
                    {
                        foreach (var s2 in targetSubjects)
                        {
                            var similarityResult = algo.SimilarityComputation(s1, s2).Result;
                            similarityResults.Add(similarityResult);
                            count++;
                            var currentPourcentage = (int)Math.Floor(count / numberOfSimilarityComputation * 100);
                            if (currentPourcentage > pourcentage)
                            {
                                pourcentage++;
                                logger.LogInformation($"{pourcentage}%");
                            }
                        }
                    }));
                    try
                    {
                        await System.IO.File.WriteAllTextAsync(System.IO.Path.Combine(appSettings.defaultDirectory, $"scoresNonAggregated_{sourceTarget.Name}.json"), JsonConvert.SerializeObject(((Algo)algo).subScores
                            .Where(s => s.s != null && s.t != null && s.w > 0 && s.n != null && s.n.Any(sub => sub.c > 0 && sub.v > 0 && sub.o > 0 && sub.m > 0 && sub.p != null))));

                        await algo.SaveTranslations(System.IO.Path.Combine(appSettings.defaultDirectory, appSettings.translationsPath));
                        if (similarityResults != null && similarityResults.Any())
                            await SaveSimilarityResults(similarityResults.ToList(), System.IO.Path.Combine(dir, $"{sourceTarget.Name}.json"));
                        else throw new Exception("similarityResults is null or empty !");
                    }
                    catch (System.Exception e)
                    {
                        logger.LogError(e.Message);
                        logger.LogError(e.StackTrace);
                        logger.LogError(e.ToString());
                    }
                }

                if (string.IsNullOrWhiteSpace(sourceTarget.refalign)) continue;
                var dic = similarityResults
                    .Where(s => s != null && !string.IsNullOrWhiteSpace(s.Uri1))
                    .GroupBy(s => s.Uri1).ToDictionary(s => s.Key, s => s.ToList());
                var positions = new List<int>(); // contains the position of the referrer in our results list. The lower, the better.
                var positionsFound = new List<int>(); // contains the position of the referrer in our results list. The lower it is, the better (without those not found)
                var indexNegativeCount = 0;
                var notFoundInDict = 0;
                var s2FoundInDict = 0;
                var alignments = Tools.GetRefalignResources(sourceTarget.refalign);
                logger.LogInformation($"dictionary size : {dic.Count}");
                var sourceAlignmentNotInSourceSubjects = 0;
                var targetAlignmentNotInTargetSubjects = 0;
                var predictedMatch = new List<SimilarityResult>();
                var numberOfBetterElementsList = new List<int>();
                var minSimilarityValue = double.PositiveInfinity;
                foreach (var alignment in alignments)
                {
                    try
                    {
                        var s1 = alignment.Source;
                        if (!sourceSubjects.Contains(s1))
                        {
                            Console.Write(s1);
                            sourceAlignmentNotInSourceSubjects++;
                        }
                        var s2 = alignment.Target;
                        if (!targetSubjects.Contains(s2)) targetAlignmentNotInTargetSubjects++;
                        if (!sourceSubjects.Contains(s1) && !targetSubjects.Contains(s2))
                        {
                            s1 = alignment.Target;
                            s2 = alignment.Source;
                        }
                        if (!dic.ContainsKey(s1))
                        {
                            notFoundInDict++;
                            if (dic.ContainsKey(s2))
                            {
                                s2FoundInDict++;
                            }
                            continue;
                        }
                        var similarityResultList = dic[s1].OrderByDescending(s => s.Result).Where(s => s.Uri1 != null && s.Uri2 != null).ToList();
                        predictedMatch.Add(similarityResultList.ElementAt(0));


                        var index = similarityResultList.Select(s => s.Uri2).ToList().IndexOf(s2);
                        var s1s2 = similarityResultList.ElementAt(similarityResultList.Count > index ? (index >= 0 ? index : similarityResultList.Count - 1) : similarityResultList.Count - 1);
                        if (s1s2.Uri2 != s2)
                            throw new Exception("s1s2.Uri2 != s2");
                        if (s1s2.Uri1 != s1)
                            throw new Exception("s1s2.Uri1 != s1");
                        if (double.IsNaN(s1s2.Result))
                            logger.LogInformation($"Nan : {s1} // {s2}");
                        var numberOfBetterElements = similarityResultList.Where(sr1 => sr1.Result > s1s2.Result).Count();
                        minSimilarityValue = minSimilarityValue > s1s2.Result ? s1s2.Result : minSimilarityValue;

                        numberOfBetterElementsList.Add(numberOfBetterElements);
                        if (index == -1)
                        {
                            index = targetSubjects.Count;
                            indexNegativeCount++;
                        }
                        else
                        {
                            positionsFound.Add(index);
                        }
                        positions.Add(index);

                    }
                    catch (System.Exception e)
                    {
                        logger.LogError(e.Message);
                        logger.LogError(e.StackTrace);
                        logger.LogError(e.ToString());
                    }
                }
                logger.LogInformation($"# of not found match (should be 0) : {positions.Count - positionsFound.Count}");
                logger.LogInformation($"# of found match (i.e. index = 0) (should NOT be 0) : {positionsFound.Count}");
                logger.LogInformation($"# of positions (should NOT be 0) : {positions.Count}");
                logger.LogInformation($"# of source alignment not in source subjects (should be 0) : {sourceAlignmentNotInSourceSubjects}");
                logger.LogInformation($"# of target alignment not in target subjects (should be 0) : {targetAlignmentNotInTargetSubjects}");
                logger.LogInformation($"# negative index (should be 0) : {indexNegativeCount}");
                logger.LogInformation($"# source alignment not found in dictionary (should be 0) : {notFoundInDict}");
                logger.LogInformation($"# target alignment found in dictionary (should be 0 if there are no common resources) : {s2FoundInDict}");
                logger.LogInformation($"average position : {(positions.Any() ? positions.Average() : 0)}");
                logger.LogInformation($"average position (with only found): {(positionsFound.Any() ? positionsFound.Average() : 0)}");
                logger.LogInformation($"# of alignments (for real)=(in theory): {positions.Count} = {alignments.Count()} ({(int)Math.Floor(((double)positions.Count) / ((double)alignments.Count()) * 100d)})");
                logger.LogInformation($"# of alignments in results (for real)=(in theory): {positionsFound.Count} = {alignments.Count()} ({(int)Math.Floor(((double)positionsFound.Count) / ((double)alignments.Count()) * 100d)}%)");
                logger.LogInformation($"# of good positions (good alignments) (should be equal to {alignments.Count()}) : {(positions.Where(p => p == 0).Count())} ({(int)Math.Floor(((double)positions.Where(p => p == 0).Count()) / ((double)alignments.Count()) * 100d)}%)");


                double threshold = 0.0357769130450459;//DOREMUS FPT
                logger.LogInformation($"average # of better elements : {numberOfBetterElementsList.Average()}");
                logger.LogInformation($"# of predicted match : {predictedMatch.Count}");
                logger.LogInformation($"min result : {(predictedMatch.Any() ? predictedMatch.Min(p => p.Result) : 0)}");
                logger.LogInformation($"max result : {(predictedMatch.Any() ? predictedMatch.Max(p => p.Result) : 0)}");
                logger.LogInformation($"average result : {(predictedMatch.Any() ? predictedMatch.Average(p => p.Result) : 0)}");

                var filteredPredictedMatch = serviceProvider.GetService<IAggregation>().Filter(similarityResults, sourceSubjects, targetSubjects);
                logger.LogInformation($"# of filtered predicted match : {filteredPredictedMatch.Count()}");
                var evaluation = new Evaluation(filteredPredictedMatch, alignments, serviceProvider.GetRequiredService<ILoggerFactory>());
                logger.LogInformation($"threshold : {threshold}");
                logger.LogInformation($"Precision : {evaluation.Precision}");
                logger.LogInformation($"Recall : {evaluation.Recall}");
                logger.LogInformation($"F-measure : {evaluation.FMeasure}");


            }
        }



        static async Task<IAlgo> Setup(string endpointUri1, string endpointUri2, string rdfFile1, string rdfFile2, string namedGraph1, string namedGraph2, string[] classesToSelect1, string[] classesToSelect2)
        {
            var algo = serviceProvider.GetService<IAlgo>();


            algo.SetEndpoints(endpointUri1, endpointUri2);
            algo.SetRdfFilePaths(rdfFile1, rdfFile2);
            algo.SetNamedGraph1(namedGraph1);
            algo.SetNamedGraph2(namedGraph2);
            try
            {
                logger.LogDebug($"Init starting");
                if (!await algo.Init(System.IO.Path.Combine(appSettings.defaultDirectory, appSettings.translationsPath), classesToSelect1, classesToSelect2))
                {
                    throw new Exception("An error occurred during initialization.");
                }
                logger.LogDebug($"Init done");
            }
            catch (Exception e)
            {
                logger.LogCritical(e.Message);
                logger.LogCritical(e.StackTrace);
                logger.LogCritical(e.ToString());
                if (e.InnerException != null)
                {
                    logger.LogCritical(e.InnerException.Message);
                    logger.LogCritical(e.InnerException.StackTrace);
                    logger.LogCritical(e.InnerException.ToString());
                }
                throw new Exception($"Initialization failed : {e.Message}");
            }
            return algo;
        }


        /// <summary>
        /// Recording of results for further processing.
        /// </summary>
        /// <param name="similarityResultList"></param>
        /// <param name="path"></param>
        /// <returns></returns>
        private static async Task SaveSimilarityResults(List<SimilarityResult> similarityResultList, string path)
        {
            logger.LogInformation($"Saving similarity results ({similarityResultList.Count}) in {path}...");
            string json = JsonConvert.SerializeObject(similarityResultList.Where(s => s != null && s.Uri1 != null && s.Uri2 != null));
            await System.IO.File.WriteAllTextAsync(path, json);
        }
        private static async Task<SimilarityResult[]> GetSimilarityResults(string path)
        {
            if (!System.IO.File.Exists(path)) throw new System.IO.FileNotFoundException(path);
            // path : appSettings.similarityResultsFile
            var jsonContent = await System.IO.File.ReadAllTextAsync(path);
            return JsonConvert.DeserializeObject<SimilarityResult[]>(jsonContent);
        }
    }
}