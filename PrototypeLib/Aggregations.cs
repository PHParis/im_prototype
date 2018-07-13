using System.Collections.Generic;
using System.Linq;

namespace PrototypeLib
{
    public class Aggregation : IAggregation
    {
        public double Aggregate(SubScore score)
        {
            var (w1, w2, w3, w4, w5) = (1, 1, 1, 0.26531416795464, 0.73468583204536);
            return Aggregate(score, w1, w2, w3, w4, w5);
        }
        public double Aggregate(SubScore score, double w1, double w2, double w3, double w4, double w5)
        {
            if (!score.n.Any()) return 0d;
            var subscores = score.n.Select(s => (w1 * s.m + w2 * (1d - (s.v / s.c)) + w3 * (1d - (s.o / s.v))) / (w1 + w2 + w3)).Average();
            return (w4 * score.w + w5 * subscores) / (w4 + w5);
        }


        public IEnumerable<SimilarityResult> Filter(IEnumerable<SimilarityResult> scores, IEnumerable<string> sourceSubjects, IEnumerable<string> targetSubjects)
        {
            var subScores = scores.OrderByDescending(x => x.Result).ToList();
            var filteredSubScores = new List<SimilarityResult>();
            foreach (var subj in subScores.Select(x => x.Uri1).Distinct())
            {
                var topScore = subScores.First(x => x.Uri1 == subj);
                filteredSubScores.Add(topScore);
            }
            filteredSubScores = filteredSubScores.OrderByDescending(x => x.Result).ToList();
            var scToRemove = new List<SimilarityResult>();
            foreach (var sc in filteredSubScores)
            {
                if (filteredSubScores.Count(x => x.Uri2 == sc.Uri2) > 1)
                {
                    scToRemove.AddRange(filteredSubScores.Where(x => x.Uri2 == sc.Uri2).Skip(1));
                }
            }
            filteredSubScores = filteredSubScores.Except(scToRemove).ToList();
            return filteredSubScores.Where(x => x.Result >= 0.4323);
        }
    }

}