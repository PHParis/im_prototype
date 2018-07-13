using System.Collections.Generic;
using System.Linq;
using Microsoft.Extensions.Logging;

namespace PrototypeLib
{
    public class Evaluation
    {
        private readonly ILogger<Evaluation> _logger;
        public Evaluation(IEnumerable<SimilarityResult> similarityResultList, IEnumerable<Alignment> alignments, ILoggerFactory loggerFactory)//, double threshold)
        {
            _logger = loggerFactory.CreateLogger<Evaluation>();
            Init(similarityResultList, alignments);
        }

        private void Init(IEnumerable<SimilarityResult> similarityResultList, IEnumerable<Alignment> alignments)
        {
            double tp = 0d; // true positive : number of alignments predicted and which are really true
            double fp = 0d; // false positive : number of alignments predicted and which are false in reality
            double fn = 0d; // false negative : true alignment (gold standard) not found among those predicted
            var tpScores = new List<SimilarityResult>();
            var fpScores = new List<SimilarityResult>();
            var fnScores = new List<Alignment>();
            foreach (var alignment in alignments)
            {
                var s = alignment.Source;
                var t = alignment.Target;
                var similarityResult = similarityResultList.SingleOrDefault(sr => sr.Uri1 == s && sr.Uri2 == t);
                if (similarityResult == null || (similarityResult.Uri1 == null && similarityResult.Uri2 == null))
                    similarityResult = similarityResultList.SingleOrDefault(sr => sr.Uri2 == s && sr.Uri1 == t);
                if (similarityResult == null || (similarityResult.Uri1 == null && similarityResult.Uri2 == null))
                {
                    _logger.LogDebug($"fn : {s} || {t}");
                    fn++;
                    fnScores.Add(alignment);
                }
            }
            foreach (var similarityResult in similarityResultList)//.Where(sr => sr.Result >= threshold))
            {
                var alignement = alignments.SingleOrDefault(a => a.Source == similarityResult.Uri1 && a.Target == similarityResult.Uri2);
                if (alignement == null)
                    alignement = alignments.SingleOrDefault(a => a.Source == similarityResult.Uri2 && a.Target == similarityResult.Uri1);
                if (alignement == null)
                {
                    _logger.LogDebug($"fp : {similarityResult.Uri1} || {similarityResult.Uri2} instead of {alignments.FirstOrDefault(a => a.Source == similarityResult.Uri1)?.Target}");
                    fp++;
                    fpScores.Add(similarityResult);
                }
                else
                {
                    tp++;
                    tpScores.Add(similarityResult);
                }
            }
            Precision = tp / (tp + fp);
            Recall = tp / (tp + fn);
            _logger.LogInformation($"tp : {tp}");
            _logger.LogInformation($"fp : {fp}");
            _logger.LogInformation($"fn : {fn}");
            var minTP = (tpScores.Any() ? tpScores.Select(f => f.Result).Min() : 0d);
            _logger.LogInformation($"Min tp : {minTP}");
            _logger.LogInformation($"# fp that could have been avoided (above Min tp) : {(fpScores.Any() ? fpScores.Where(s => s.Result < minTP).Count() : 0)}");
            _logger.LogDebug($"avg tp : {(tpScores.Any() ? tpScores.Select(f => f.Result).Average() : 0d)}");
            _logger.LogDebug($"avg fp : {(fpScores.Any() ? fpScores.Select(f => f.Result).Average() : 0d)}");
        }
        public double Precision { get; set; }
        public double Recall { get; set; }
        public double FMeasure
        {
            get
            {
                return 2d * (Precision * Recall) / (Precision + Recall);
            }
        }
    }
}