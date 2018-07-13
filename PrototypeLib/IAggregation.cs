using System.Collections.Generic;

namespace PrototypeLib
{
    public interface IAggregation
    {
        double Aggregate(SubScore score);
        double Aggregate(SubScore score, double w1, double w2, double w3, double w4, double w5);

        IEnumerable<SimilarityResult> Filter(IEnumerable<SimilarityResult> scores, IEnumerable<string> sourceSubjects, IEnumerable<string> targetSubjects);

    }
}