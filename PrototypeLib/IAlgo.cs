using System.Threading.Tasks;
using System.Collections.Generic;

namespace PrototypeLib
{
    public interface IAlgo
    {
        void SetEndpoints(string endpoint1, string endpoint2);
        void SetRdfFilePaths(string file1, string file2);
        void SetNamedGraph1(string namedGraphUri);
        void SetNamedGraph2(string namedGraphUri);

        Task<SimilarityResult> SimilarityComputation(string uri1, string uri2);
        Task<bool> Init(string translationsPath, string[] classesToSelect1, string[] classesToSelect2);
        Task SaveTranslations(string translationsPath);


        (List<string>, List<string>) GetSubjects();

    }
}