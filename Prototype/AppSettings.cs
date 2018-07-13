namespace Prototype
{
    public class AppSettings
    {
        // TODO: Supprimer enpointUri, namedGraph, rdfFile pour ne garder que des listes de SourceTarget
        public string endpointUri1 { get; set; }
        public string endpointUri2 { get; set; }
        public string namedGraph1 { get; set; }
        public string namedGraph2 { get; set; }
        public string testDataFile { get; set; }
        public string similarityResultsFile { get; set; }

        public string rdfFile1 { get; set; }
        public string rdfFile2 { get; set; }
        public string conceptDistancesFile { get; set; }
        public SourceTarget[] SourceTargetCouples { get; set; }
        public string defaultDirectory { get; set; }

        /// <summary>
        /// Subjects having a type in this array are discarded.
        /// </summary>
        /// <returns></returns>
        public string[] classesToIgnore { get; set; }

        /// <summary>
        /// Force to (re)compute similarity between entities.!-- If false, last saved similarity (if exist) will be used !
        /// </summary>
        /// <returns></returns>
        public bool ForceSimilarityComputation { get; set; }
        public string translationsPath { get; set; }
    }

    public class SourceTarget
    {
        public string endpointUri1 { get; set; }
        public string endpointUri2 { get; set; }
        public string namedGraph1 { get; set; }
        public string namedGraph2 { get; set; }
        public string rdfFile1 { get; set; }
        public string rdfFile2 { get; set; }
        public string refalign { get; set; }
        public string Name { get; set; }
        public string[] classesToIgnore { get; set; }

        public bool Active { get; set; }

        /// <summary>
        /// par exemple pour Spimbench 2017 c'est CreativeWork
        /// </summary>
        /// <returns></returns>
        public string[] classesToSelect1 { get; set; }
        public string[] classesToSelect2 { get; set; }
    }
}