using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.Extensions.Logging;
using System.Threading.Tasks;
using VDS.RDF;
using VDS.RDF.Query;
using System.Collections.Concurrent;
using VDS.RDF.Parsing;

namespace PrototypeLib
{
    public class Algo : IAlgo
    {
        private readonly F23.StringSimilarity.Interfaces.IStringSimilarity stringSimilarity;
        private readonly IAggregation aggregation;
        public int initialTranslationsCount { get; set; }
        private ConcurrentDictionary<string, string> translations { get; set; }
        private readonly ILogger<Algo> _logger;
        private Dictionary<String, int> ntc;
        private Dictionary<String, Dictionary<String, int>> ntcp;
        private Dictionary<String, Dictionary<String, int>> ntcpo;
        private Dictionary<string, Dictionary<string, Dictionary<string, int>>> nscpo;
        private HashSet<TripleImpl> sourceTriples;
        private HashSet<TripleImpl> targetTriples;
        public string RdfFilePath1 { get; private set; }
        public string RdfFilePath2 { get; private set; }
        public string Endpoint1 { get; private set; }
        public string Endpoint2 { get; private set; }
        public Dictionary<string, HashSet<string>> sameAsCouples { get; set; }
        public SparqlQueryable source { get; set; }
        public SparqlQueryable target { get; set; }
        public string NamedGraphUri1 { get; private set; }
        public string NamedGraphUri2 { get; private set; }
        public Dictionary<string, Dictionary<string, int>> ConceptDistances { get; private set; }

        public Algo(ILoggerFactory loggerFactory, F23.StringSimilarity.Interfaces.IStringSimilarity stringSimilarity, IAggregation aggregation)
        {
            this._logger = loggerFactory.CreateLogger<Algo>();
            this.stringSimilarity = stringSimilarity;
            this.aggregation = aggregation;
            this._logger.LogInformation($"Aggregation class : {this.aggregation.GetType().Name}");
            // var cosine = new F23.StringSimilarity.Cosine(1);
        }

        #region Configuration    
        public void SetEndpoints(string endpoint1, string endpoint2)
        {
            Endpoint1 = endpoint1;
            Endpoint2 = endpoint2;
        }
        public void SetRdfFilePaths(string file1, string file2)
        {
            RdfFilePath1 = file1;
            RdfFilePath2 = file2;
        }
        public void SetNamedGraph1(string namedGraphUri)
        {
            NamedGraphUri1 = namedGraphUri;
        }

        public void SetNamedGraph2(string namedGraphUri)
        {
            NamedGraphUri2 = namedGraphUri;
        }



        public static void AugmentationTriplesAvecSemantiqueADeplacerParallel(HashSet<TripleImpl> spoTriples)
        {
            var count = spoTriples.Count;
            var classes = spoTriples.Where(t => t.p == Vocabulary.RDF.type.AbsoluteUri).Select(t => t.o).ToHashSet();
            classes.UnionWith(spoTriples.Where(t => t.p == Vocabulary.RDFS.subClassOf.AbsoluteUri).SelectMany(t => new[] { t.s, t.o }));
            classes.UnionWith(spoTriples.Where(t => t.p == Vocabulary.OWL.equivalentClass.AbsoluteUri).SelectMany(t => new[] { t.s, t.o }));
            classes.UnionWith(spoTriples.Where(t => t.p == Vocabulary.RDFS.domain.AbsoluteUri).Select(t => t.o));
            classes.UnionWith(spoTriples.Where(t => t.p == Vocabulary.RDFS.range.AbsoluteUri).Select(t => t.o));
            foreach (var classToAdd in classes)
            {
                spoTriples.Add(new TripleImpl { s = classToAdd, p = Vocabulary.RDFS.subClassOf.AbsoluteUri, o = classToAdd });
            }

            // types avec domain
            // _logger.LogInformation("Domain");
            var predDomain = spoTriples.Where(t => t.p == PrototypeLib.Vocabulary.RDFS.domain.AbsoluteUri).GroupBy(g => g.s).ToDictionary(t => t.Key, t => t.Select(g => g.o).ToHashSet());
            do
            {
                count = spoTriples.Count;
                // foreach (var triple in spoTriples.Where(t => t.p == PrototypeLib.Vocabulary.RDF.type.AbsoluteUri))
                var triplesToAdd = new ConcurrentBag<TripleImpl>();
                foreach (var pred in predDomain.Keys)
                {
                    var domains = predDomain[pred];
                    spoTriples.AsParallel().Where(t => t.p == pred).ForAll(triple =>
                    {
                        // foreach (var triple in spoTriples.Where(t => t.p == pred))
                        // {
                        foreach (var domain in domains)
                        {
                            var newTriple = new TripleImpl { s = triple.s, p = PrototypeLib.Vocabulary.RDF.type.AbsoluteUri, o = domain };
                            triplesToAdd.Add(newTriple);
                        }
                    }
                    );
                }
                spoTriples.UnionWith(triplesToAdd);
            } while (spoTriples.Count > count);

            // types avec range
            // _logger.LogInformation("range");
            var predRange = spoTriples.Where(t => t.p == PrototypeLib.Vocabulary.RDFS.range.AbsoluteUri).GroupBy(g => g.s).ToDictionary(t => t.Key, t => t.Select(g => g.o).ToHashSet());
            do
            {
                count = spoTriples.Count;
                // foreach (var triple in spoTriples.Where(t => t.p == PrototypeLib.Vocabulary.RDF.type.AbsoluteUri))
                var triplesToAdd = new ConcurrentBag<TripleImpl>();
                foreach (var pred in predRange.Keys)
                {
                    var ranges = predRange[pred];
                    spoTriples.AsParallel().Where(t => t.p == pred).ForAll(triple =>
                    {
                        // foreach (var triple in spoTriples.Where(t => t.p == pred))
                        // {
                        foreach (var range in ranges)
                        {
                            var newTriple = new TripleImpl { s = triple.o, p = PrototypeLib.Vocabulary.RDF.type.AbsoluteUri, o = range };
                            triplesToAdd.Add(newTriple);
                        }
                    }
                    );
                }
                spoTriples.UnionWith(triplesToAdd);
            } while (spoTriples.Count > count);

            // types avec subclass
            // _logger.LogInformation("subclass");
            var subSuper = spoTriples.Where(t => t.p == PrototypeLib.Vocabulary.RDFS.subClassOf.AbsoluteUri).GroupBy(g => g.s).ToDictionary(t => t.Key, t => t.Select(g => g.o).ToHashSet());
            do
            {
                count = spoTriples.Count;
                // foreach (var triple in spoTriples.Where(t => t.p == PrototypeLib.Vocabulary.RDF.type.AbsoluteUri))
                var triplesToAdd = new ConcurrentBag<TripleImpl>();
                foreach (var sub in subSuper.Keys)
                {
                    var supers = subSuper[sub];
                    spoTriples.AsParallel().Where(t => t.p == PrototypeLib.Vocabulary.RDF.type.AbsoluteUri && t.o == sub).ForAll(triple =>
                    {
                        // foreach (var triple in spoTriples.Where(t => t.p == PrototypeLib.Vocabulary.RDF.type.AbsoluteUri && t.o == sub))
                        // {
                        foreach (var super in supers)
                        {
                            var newTriple = new TripleImpl { s = triple.s, p = PrototypeLib.Vocabulary.RDF.type.AbsoluteUri, o = super };
                            triplesToAdd.Add(newTriple);
                        }
                    }
                    );
                }
                spoTriples.UnionWith(triplesToAdd);
            } while (spoTriples.Count > count);
            // FunctionalProperty    
            // _logger.LogInformation("FunctionalProperty");
            var functionalProperties = spoTriples.Where(t => t.p == PrototypeLib.Vocabulary.OWL.FunctionalProperty.AbsoluteUri).Select(t => t.s);//.GroupBy(g => g.s).ToDictionary(t => t.Key, t => t.Select(g => g.o).ToHashSet());
            do
            {
                count = spoTriples.Count;
                // foreach (var triple in spoTriples.Where(t => t.p == PrototypeLib.Vocabulary.RDF.type.AbsoluteUri))
                var triplesToAdd = new ConcurrentBag<TripleImpl>();
                foreach (var functionalProperty in functionalProperties)
                {
                    spoTriples.AsParallel().Where(t => t.p == functionalProperty).GroupBy(t => t.s).Where(g => g.Count() > 1).Select(g => g.Select(t => t.o).ToList()).ForAll(list =>
                    {
                        // foreach (var list in spoTriples.Where(t => t.p == functionalProperty).GroupBy(t => t.s).Where(g => g.Count() > 1).Select(g => g.Select(t => t.o).ToList()))
                        // {
                        for (int i = 0; i < list.Count; i++)
                        {
                            for (int j = i + 1; j < list.Count; j++)
                            {
                                var newTriple = new TripleImpl { s = list[i], p = PrototypeLib.Vocabulary.OWL.sameAs.AbsoluteUri, o = list[j] };
                                triplesToAdd.Add(newTriple);
                            }
                        }
                    }
                    );
                }
                spoTriples.UnionWith(triplesToAdd);
            } while (spoTriples.Count > count);
            // InverseFunctionalProperty   
            // _logger.LogInformation("InverseFunctionalProperty"); 
            var inverseFunctionalProperties = spoTriples.Where(t => t.p == PrototypeLib.Vocabulary.OWL.InverseFunctionalProperty.AbsoluteUri).Select(t => t.s);//.GroupBy(g => g.s).ToDictionary(t => t.Key, t => t.Select(g => g.o).ToHashSet());
            do
            {// TODO: paralléliser cette boucle
                count = spoTriples.Count;
                // foreach (var triple in spoTriples.Where(t => t.p == PrototypeLib.Vocabulary.RDF.type.AbsoluteUri))
                var triplesToAdd = new List<TripleImpl>();
                foreach (var inverseFunctionalProperty in inverseFunctionalProperties)
                {
                    foreach (var list in spoTriples.Where(t => t.p == inverseFunctionalProperty).GroupBy(t => t.o).Where(g => g.Count() > 1).Select(g => g.Select(t => t.s).ToList()))
                    {
                        for (int i = 0; i < list.Count; i++)
                        {
                            for (int j = i + 1; j < list.Count; j++)
                            {
                                var newTriple = new TripleImpl { s = list[i], p = PrototypeLib.Vocabulary.OWL.sameAs.AbsoluteUri, o = list[j] };
                                triplesToAdd.Add(newTriple);
                            }
                        }
                    }
                }
                spoTriples.UnionWith(triplesToAdd);
            } while (spoTriples.Count > count);
            // hasKey
            // TODO: à faire pour haskey, maxCardi...
            // maxCardinality
            // maxQualifiedCardinality
            // sameAs
            // _logger.LogInformation("sameAs"); 
            var sameAsCouples = spoTriples.Where(t => t.p == PrototypeLib.Vocabulary.OWL.sameAs.AbsoluteUri).GroupBy(g => g.s).ToDictionary(t => t.Key, t => t.Select(g => g.o).ToHashSet());
            do
            {// TODO: paralléliser cette boucle
                count = spoTriples.Count;
                // foreach (var triple in spoTriples.Where(t => t.p == PrototypeLib.Vocabulary.RDF.type.AbsoluteUri))
                var triplesToAdd = new List<TripleImpl>();
                foreach (var sub in sameAsCouples.Keys)
                {
                    var objs = sameAsCouples[sub];
                    foreach (var obj in objs)
                    {
                        if (!sameAsCouples.ContainsKey(obj) || !sameAsCouples[obj].Contains(sub))
                        {
                            // lien inverse
                            var newTriple = new TripleImpl { s = obj, p = PrototypeLib.Vocabulary.OWL.sameAs.AbsoluteUri, o = sub };
                            triplesToAdd.Add(newTriple);
                        }
                        if (sameAsCouples.ContainsKey(obj))
                        {
                            // transititvité
                            foreach (var obj2 in sameAsCouples[obj])
                            {
                                var newTriple = new TripleImpl { s = sub, p = PrototypeLib.Vocabulary.OWL.sameAs.AbsoluteUri, o = obj2 };
                                triplesToAdd.Add(newTriple);
                            }
                        }
                    }
                }
                spoTriples.UnionWith(triplesToAdd);
            } while (spoTriples.Count > count);
            //     var query = "PREFIX xsd: <http://www.w3.org/2001/XMLSchema#> ASK "+namedGraph+" {" +
            //    "{ <" + uri1 + "> (<" + sameAs + ">|^<" + sameAs + ">)* <" + uri2 + "> . } UNION " +
            //    "{ ?p a <" + FunctionalProperty + "> . ?x ?p <" + uri1 + "> . ?x ?p <" + uri2 + "> . } UNION " +
            //    "{ ?p a <" + InverseFunctionalProperty + "> . <" + uri1 + "> ?p ?o . <" + uri2 + "> ?p ?o . } UNION " +
            //    "{ ?c <" + hasKey + "> ?u . <" + uri1 + "> a ?c . <" + uri1 + "> ?u ?z . <" + uri2 + "> a ?c . <" + uri2 + "> ?u ?z . } UNION " +
            //    "{ ?x <" + maxCardinality + "> '1'^^xsd:nonNegativeInteger . ?x <" + onProperty + "> ?p . ?u a ?x . ?u ?p <" + uri1 + "> . ?u ?p <" + uri2 + "> . } UNION " +
            //    "{ ?x <" + maxQualifiedCardinality + "> '1'^^xsd:nonNegativeInteger . ?x <" + onProperty + "> ?p . ?x <" + onClass + "> ?c . ?u a ?x . ?u ?p <" + uri1 + "> . <" + uri1 + "> a ?c . ?u ?p <" + uri2 + "> . <" + uri2 + "> a ?c . } UNION " +
            //    "{ ?x <" + maxQualifiedCardinality + "> '1'^^xsd:nonNegativeInteger . ?x <" + onProperty + "> ?p . ?x <" + onClass + "> <" + Thing + "> . ?u a ?x . ?u ?p <" + uri1 + "> . ?u ?p <" + uri2 + "> . } }";


            // _logger.LogInformation("ajout owl:Thing"); 
            var filter = new[] {
                Vocabulary.OWL.AllDifferent.AbsoluteUri,
                Vocabulary.OWL.AnnotationProperty.AbsoluteUri,
                Vocabulary.OWL.AllDisjointClasses.AbsoluteUri,
                Vocabulary.OWL.AllDisjointProperties.AbsoluteUri,
                Vocabulary.OWL.Class.AbsoluteUri,
                Vocabulary.OWL.DatatypeProperty.AbsoluteUri,
                Vocabulary.OWL.FunctionalProperty.AbsoluteUri,
                Vocabulary.OWL.InverseFunctionalProperty.AbsoluteUri,
                Vocabulary.OWL.IrreflexiveProperty.AbsoluteUri,
                Vocabulary.OWL.Nothing.AbsoluteUri,
                Vocabulary.OWL.ObjectProperty.AbsoluteUri,
                Vocabulary.OWL.SymmetricProperty.AbsoluteUri,
                Vocabulary.OWL.TransitiveProperty.AbsoluteUri,
                Vocabulary.RDFS.Class.AbsoluteUri,
                Vocabulary.RDFS.Container.AbsoluteUri,
                Vocabulary.RDFS.ContainerMembershipProperty.AbsoluteUri,
                Vocabulary.RDFS.Datatype.AbsoluteUri,
                Vocabulary.RDFS.Literal.AbsoluteUri,
                Vocabulary.OWL.Annotation.AbsoluteUri,
                Vocabulary.OWL.Axiom.AbsoluteUri,
                Vocabulary.OWL.DataRange.AbsoluteUri,
                Vocabulary.OWL.DeprecatedClass.AbsoluteUri,
                Vocabulary.OWL.DeprecatedProperty.AbsoluteUri,
                Vocabulary.OWL.NegativePropertyAssertion.AbsoluteUri,
                Vocabulary.OWL.Ontology.AbsoluteUri,
                Vocabulary.OWL.OntologyProperty.AbsoluteUri,
                Vocabulary.OWL.ReflexiveProperty.AbsoluteUri,
                Vocabulary.OWL.Restriction.AbsoluteUri,
                Vocabulary.RDF.Property.AbsoluteUri,
                Vocabulary.RDF.Alt.AbsoluteUri,
                Vocabulary.RDF.Bag.AbsoluteUri,
                Vocabulary.RDF.List.AbsoluteUri,
                Vocabulary.RDF.Seq.AbsoluteUri,
                Vocabulary.RDF.Statement.AbsoluteUri,
                Vocabulary.OWL.Thing.AbsoluteUri
            };

            var allTypes = spoTriples.AsParallel().Where(t => t.p == Vocabulary.RDF.type.AbsoluteUri).Select(t => t.o).ToHashSet();
            allTypes.ExceptWith(filter);
            // // var triplesSetToAdd = new ConcurrentBag<TripleTmp>();
            var triplesSetToAdd = spoTriples.AsParallel()
                .Where(t => t.p == Vocabulary.RDF.type.AbsoluteUri && allTypes.Contains(t.o))
                .Select(t => new TripleImpl { s = t.s, p = Vocabulary.RDF.type.AbsoluteUri, o = Vocabulary.OWL.Thing.AbsoluteUri }).ToList();
            // spoTriples.AsParallel().Where(t => t.p == Vocabulary.RDF.type.AbsoluteUri && allTypes.Contains(t.o)).Select(t => t.s).Distinct().ForAll(subject => {
            // // foreach (var subject in spoTriples.Select(t => t.s).Distinct())
            // // {
            //     // var types = spoTriples.Where(t => t.s == subject && t.p == Vocabulary.RDF.type.AbsoluteUri).Select(t => t.o).ToHashSet();
            //     // if (!types.Intersect(filter).Any())
            //         triplesSetToAdd.Add(new TripleTmp { s = subject, p = Vocabulary.RDF.type.AbsoluteUri, o = Vocabulary.OWL.Thing.AbsoluteUri });
            // }
            // );
            spoTriples.UnionWith(triplesSetToAdd);
        }

        public string[] classesToSelect1 { get; set; }
        public string[] classesToSelect2 { get; set; }
        public async Task<bool> Init(string translationsPath, string[] classesToSelectPar1, string[] classesToSelectPar2)
        {
            _logger.LogDebug("Start : Init");
            subScores = new ConcurrentBag<SubScore>();
            classesToSelect1 = classesToSelectPar1 != null ? classesToSelectPar1 : new string[0];
            classesToSelect2 = classesToSelectPar2 != null ? classesToSelectPar2 : new string[0];
            _logger.LogDebug($"Checking : {Endpoint1}");
            if (!string.IsNullOrWhiteSpace(Endpoint1) && !Tools.IsEndpointAlive(Endpoint1))
            {
                _logger.LogCritical($"Endpoint unreachable : {Endpoint1}");
                return false;
            }
            _logger.LogDebug($"Checking : {Endpoint2}");
            if (!string.IsNullOrWhiteSpace(Endpoint2) && !string.IsNullOrWhiteSpace(Endpoint1) && !Endpoint1.Equals(Endpoint2) && !Tools.IsEndpointAlive(Endpoint2))
            {
                _logger.LogCritical($"Endpoint unreachable : {Endpoint2}");
                return false;
            }

            if (!LoadSourceOrTarget(Endpoint1, RdfFilePath1, "source", NamedGraphUri1)) return false;
            if (!LoadSourceOrTarget(Endpoint2, RdfFilePath2, "target", NamedGraphUri2)) return false;
            var namedGraph1 = !NamedGraphUri1.IsNullOrWhiteSpace() ? $" FROM <{NamedGraphUri1}> " : string.Empty;
            var spoQuerySource = await source.SelectQuery("SELECT ?s ?p ?o" + namedGraph1 + " WHERE {?s ?p ?o.}");
            sourceTriples = spoQuerySource.Select(triple => new TripleImpl { s = Tools.GetUriOrValue(triple["s"]), p = Tools.GetUriOrValue(triple["p"]), o = Tools.GetUriOrValue(triple["o"]) }).ToHashSet();
            var sourceInstanceCount = sourceTriples.Select(x => x.s).Distinct().Count();
            var sourceTriplesCount = sourceTriples.Count();
            _logger.LogInformation($"stats source -> instanceCount: {sourceInstanceCount}");
            _logger.LogInformation($"stats source -> triplesCount: {sourceTriplesCount}");
            // On essaie de DL les ontologies :
            _logger.LogInformation("ontologies download...");
            var ontologies = new Graph();
            foreach (var g in sourceTriples.Where(t => t.p == PrototypeLib.Vocabulary.RDF.type.AbsoluteUri).Select(t => t.o).Distinct().Select(c => UriFactory.Create(c))
                .Where(u => u.Host != Vocabulary.OWL.NameSpace && u.Host != Vocabulary.FOAF.NameSpace && u.Host != Vocabulary.RDF.NameSpace && u.Host != Vocabulary.RDFS.NameSpace).GroupBy(u => u.Host))
            {
                var uri = g.FirstOrDefault();
                if (uri != null)
                {
                    try
                    {
                        ontologies.LoadFromUri(uri);
                    }
                    catch (System.Exception ex)
                    {
                        _logger.LogDebug(ex.ToString());
                    }
                }
            }
            _logger.LogInformation($"# of triples from ontologies to add : {ontologies.Triples.Count}");
            foreach (var t in ontologies.Triples)
            {
                sourceTriples.Add(new TripleImpl { s = Tools.GetUriOrValue(t.Subject), p = Tools.GetUriOrValue(t.Predicate), o = Tools.GetUriOrValue(t.Object) });
                // pas besoin d'ajouter à la KB cible car on ne s'en sert pas
                // targetTriples.Add(new TripleTmp{ s = Tools.GetUriOrValue(t.Subject), p = Tools.GetUriOrValue(t.Predicate), o = Tools.GetUriOrValue(t.Object)});
            }
            _logger.LogInformation($"# of source triples before : {sourceTriples.Count}");
            AugmentationTriplesAvecSemantiqueADeplacerParallel(sourceTriples);
            _logger.LogInformation($"# of source triples after : {sourceTriples.Count}");

            var sourceInstancesToMatchSet = new HashSet<string>();
            foreach (var classToSelect in classesToSelectPar1)
            {
                sourceInstancesToMatchSet.UnionWith(sourceTriples.Where(x => x.p == Vocabulary.RDF.type.AbsoluteUri && x.o == classToSelect).Select(x => x.s));
            }
            var sourceInstancesToMatchCount = sourceInstancesToMatchSet.Count;
            _logger.LogInformation($"stats source -> instancesToMatchCount: {sourceInstancesToMatchCount}");

            if (source.Equals(target))
            {
                _logger.LogInformation("source == target");
                targetTriples = new HashSet<TripleImpl>(sourceTriples);
            }
            else
            {
                var namedGraph2 = !NamedGraphUri2.IsNullOrWhiteSpace() ? $" FROM <{NamedGraphUri2}> " : string.Empty;
                var spoQueryTarget = await target.SelectQuery("SELECT ?s ?p ?o" + namedGraph2 + " WHERE {?s ?p ?o.}");
                targetTriples = spoQueryTarget.Select(triple => new TripleImpl { s = Tools.GetUriOrValue(triple["s"]), p = Tools.GetUriOrValue(triple["p"]), o = Tools.GetUriOrValue(triple["o"]) }).ToHashSet();
                var targetInstanceCount = targetTriples.Select(x => x.s).Distinct().Count();
                var targetTriplesCount = targetTriples.Count();
                _logger.LogInformation($"stats target -> instanceCount: {targetInstanceCount}");
                _logger.LogInformation($"stats target -> triplesCount: {targetTriplesCount}");
                _logger.LogInformation($"# of target triples before : {targetTriples.Count}");
                // TODO: I have commment line below because computation causes crach on very large database...
                AugmentationTriplesAvecSemantiqueADeplacerParallel(targetTriples);
                _logger.LogInformation($"# of target triples after : {targetTriples.Count}");
                var targetInstancesToMatchSet = new HashSet<string>();
                foreach (var classToSelect in classesToSelectPar2)
                {
                    targetInstancesToMatchSet.UnionWith(targetTriples.Where(x => x.p == Vocabulary.RDF.type.AbsoluteUri && x.o == classToSelect).Select(x => x.s));
                }
                var targetInstancesToMatchCount = targetInstancesToMatchSet.Count;
                _logger.LogInformation($"stats target -> instancesToMatchCount: {targetInstancesToMatchCount}");
            }

            /* For each InverseFunctional prop we want to had a sameAs link between the 2 dataset if its apply */
            var triplesToAdd = new List<TripleImpl>();
            foreach (var inverseFuncProp in sourceTriples.Where(t => t.p == Vocabulary.RDF.type.AbsoluteUri && t.o == Vocabulary.OWL.InverseFunctionalProperty.AbsoluteUri).Select(t => t.s))
            {
                var source = sourceTriples.Where(t => t.p == inverseFuncProp).GroupBy(t => t.o).ToDictionary(g => g.Key, g => g.Select(t => t.s).ToHashSet());
                var target = targetTriples.Where(t => t.p == inverseFuncProp).GroupBy(t => t.o).ToDictionary(g => g.Key, g => g.Select(t => t.s).ToHashSet());
                foreach (var commonObject in source.Keys.Intersect(target.Keys))
                {
                    foreach (var s in source[commonObject])
                    {
                        foreach (var o in target[commonObject])
                        {
                            // if (s == "http://big.csr.unibo.it/sabine-eng.owl#Antonis_Samaras" && o == "http://big.csr.unibo.it/sabine-ita.owl#Mario_Mauro")
                            // {
                            //     _logger.LogInformation("dsfsd");
                            // }
                            if (s != o)
                                triplesToAdd.Add(new TripleImpl { s = s, p = Vocabulary.OWL.sameAs.AbsoluteUri, o = o });
                        }
                    }
                }
            }
            // if (triplesToAdd.Any(t => t.s == "http://big.csr.unibo.it/sabine-eng.owl#Antonis_Samaras" && t.p == Vocabulary.OWL.sameAs.AbsoluteUri && t.o == "http://big.csr.unibo.it/sabine-ita.owl#Mario_Mauro"))
            // {
            //     _logger.LogInformation("dsfsd");
            // }
            sourceTriples.UnionWith(triplesToAdd); // pas la peine d'ajouter à targetTriples   
            targetTriples.Where(tr => tr.s == "http://data.doremus.org/expression/8d4933fb-409e-30c7-b9f5-3ca3056833f3").Select(tr => { _logger.LogInformation(tr.ToString()); return tr; }).ToArray();
            targetTriples.Where(tr => tr.s == "http://data.doremus.org/expression/cce87cdc-3df4-3eaa-8412-3d3a086a8d9f").Select(tr => { _logger.LogInformation(tr.ToString()); return tr; }).ToArray();
            sourceTriples.Where(tr => tr.s == "http://data.doremus.org/expression/25189f7e-ebcb-34fe-a935-91afcd8089bf").Select(tr => { _logger.LogInformation(tr.ToString()); return tr; }).ToArray();

            var graphTmp = new Graph();
            foreach (var triple in sourceTriples)
            {
                var s = Tools.ToINode(triple.s, graphTmp);
                var p = Tools.ToINode(triple.p, graphTmp);
                var o = Tools.ToINode(triple.o, graphTmp);
                graphTmp.Assert(new Triple(s, p, o));
            }
            ConceptDistances = await Tools.GetConceptDistances(new SparqlQueryable(graphTmp), NamedGraphUri1);

            _logger.LogInformation($"ntc...");
            ntc = sourceTriples.Where(t => t.p == PrototypeLib.Vocabulary.RDF.type.AbsoluteUri)
                .GroupBy(t => t.o).ToDictionary(g => g.Key, g => g.Select(t => t.s).Distinct().Count());

            _logger.LogInformation($"ntcp...");
            // Nombre de sujets de la classe C ayant le prédicat P
            ntcp = new Dictionary<string, Dictionary<string, int>>();
            var subjByClasses = sourceTriples.AsParallel().Where(t => t.p == PrototypeLib.Vocabulary.RDF.type.AbsoluteUri)
                .GroupBy(t => t.o).ToDictionary(g => g.Key, g => g.Select(t => t.s).ToHashSet());
            foreach (var pair in subjByClasses)
            {
                var className = pair.Key;
                var subjects = pair.Value;
                var dict = sourceTriples.AsParallel().Where(t => subjects.Contains(t.s)).GroupBy(t => t.p).ToDictionary(g => g.Key, g => g.Select(t => t.s).Distinct().Count());
                ntcp.Add(className, dict);
            }

            _logger.LogInformation($"ntcpo...");
            ntcpo = new Dictionary<string, Dictionary<string, int>>();
            var triplesBySubj = sourceTriples.AsParallel().GroupBy(t => t.s).ToDictionary(t => t.Key, t => t.ToHashSet());
            foreach (var className in subjByClasses.Keys)
            {
                var triples = new HashSet<TripleImpl>();
                foreach (var subj in subjByClasses[className])
                {
                    if (triplesBySubj.ContainsKey(subj))
                        triples.UnionWith(triplesBySubj[subj]);
                }
                var dict = triples.AsParallel().GroupBy(t => t.p).ToDictionary(g => g.Key, g => g.Select(t => t.o).Distinct().Count());
                ntcpo.Add(className, dict);
            }

            _logger.LogInformation($"nscpo...");
            nscpo = new Dictionary<string, Dictionary<string, Dictionary<string, int>>>();
            foreach (var className in subjByClasses.Keys)
            {
                var triples = new HashSet<TripleImpl>();
                foreach (var subj in subjByClasses[className])
                {
                    if (triplesBySubj.ContainsKey(subj))
                        triples.UnionWith(triplesBySubj[subj]);
                }
                var dict = triples.AsParallel().GroupBy(t => t.p).ToDictionary(g => g.Key, g => g.GroupBy(g2 => g2.o).ToDictionary(g2 => g2.Key, g2 => g2.Select(t => t.s).Distinct().Count()));
                nscpo.Add(className, dict);
            }


            _logger.LogInformation($"sameAsCouples...");
            var sameAsLinksSource = sourceTriples.Where(t => t.p == Vocabulary.OWL.sameAs.AbsoluteUri).ToHashSet();
            var sameAsLinksTarget = targetTriples.Where(t => t.p == Vocabulary.OWL.sameAs.AbsoluteUri).ToHashSet();
            var sameAsLinks = sameAsLinksSource
                .Union(sameAsLinksTarget).ToArray();
            sameAsCouples = sameAsLinks.AsParallel()
                .GroupBy(t => t.s).ToDictionary(g => g.Key, g => g.Select(t => t.o).ToHashSet());

            _logger.LogInformation($"translations...");
            translations = new ConcurrentDictionary<string, string>();
            if (System.IO.File.Exists(translationsPath))
            {
                var lines = await System.IO.File.ReadAllLinesAsync(translationsPath);
                foreach (var line in lines)
                {
                    var splitedLine = line.Split("\t");
                    translations.TryAdd(splitedLine[0], splitedLine[1]);
                }
            }

            initialTranslationsCount = translations.Count;


            _logger.LogInformation($"Loading subjects from source...");
            sourceSubjects = new List<string>();
            _logger.LogInformation($"Loading subjects from target...");
            targetSubjects = new List<string>();

            if (classesToSelect1.Any())
            {
                var sourceSub = new HashSet<string>();

                foreach (var className in classesToSelect1)
                {
                    if (className.StartsWith("http"))
                    {
                        _logger.LogInformation($"Class to select: {className}");
                        foreach (var sub in sourceTriples.Where(t => t.p == Vocabulary.RDF.type.AbsoluteUri && t.o == className).Select(t => t.s))
                        {
                            sourceSub.Add(sub);
                        }
                    }
                    else if (className.StartsWith("SELECT "))
                    {
                        // it is a sprql query
                        _logger.LogInformation($"Query used to select: {className}");
                        var result = source.Select(className);
                        foreach (var s in result.Select(x => Tools.GetUriOrValue(x["s"])))
                        {
                            sourceSub.Add(s);
                        }
                    }
                    else
                    {
                        _logger.LogCritical("You must provide either a class name or SELECT query in the 'classesToSelect' property");
                        throw new Exception("You must provide either a class name or SELECT query in the 'classesToSelect' property");
                    }
                }

                sourceSubjects.AddRange(sourceSub);
            }
            else
            {
                _logger.LogInformation($"No specific class to select");
                sourceSubjects.AddRange(sourceTriples.Select(t => t.s).Distinct());
            }
            if (classesToSelect2.Any())
            {
                var targetSub = new HashSet<string>();

                foreach (var className in classesToSelect2)
                {
                    if (className.StartsWith("http"))
                    {
                        _logger.LogInformation($"Class to select: {className}");
                        foreach (var sub in targetTriples.Where(t => t.p == Vocabulary.RDF.type.AbsoluteUri && t.o == className).Select(t => t.s))
                        {
                            targetSub.Add(sub);
                        }
                    }
                    else if (className.StartsWith("SELECT "))
                    {
                        // it is a sprql query
                        _logger.LogInformation($"Query used to select: {className}");
                        var result = target.Select(className);
                        foreach (var s in result.Select(x => Tools.GetUriOrValue(x["s"])))
                        {
                            targetSub.Add(s);
                        }
                    }
                    else
                    {
                        _logger.LogCritical("You must provide either a class name or SELECT query in the 'classesToSelect' property");
                        throw new Exception("You must provide either a class name or SELECT query in the 'classesToSelect' property");
                    }
                }

                targetSubjects.AddRange(targetSub);
            }
            else
            {
                _logger.LogInformation($"No specific class to select");
                targetSubjects.AddRange(targetTriples.Select(t => t.s).Distinct());
            }
            _logger.LogInformation($"{sourceSubjects.Count} subjects loaded from source...");
            _logger.LogInformation($"{targetSubjects.Count} subjects loaded from target...");

            // TODO: ici je vais initialiser une variable (dict) avec en clé nom de classe et en value un set des propriétés dont au moins une instance de la classe est suejt de cette prop.
            var subjectsByClass = sourceTriples.Where(x => x.p == Vocabulary.RDF.type.AbsoluteUri).GroupBy(x => x.o).ToDictionary(x => x.Key, x => x.Select(t => t.s).ToHashSet());
            PredicatesByClasses = new Dictionary<string, HashSet<string>>();
            foreach (var subjectsClass in subjectsByClass)
            {
                var className = subjectsClass.Key;
                var predicates = subjectsClass.Value.AsParallel().SelectMany(subj => sourceTriples.Where(x => x.s == subj).Select(x => x.p)).ToHashSet();
                PredicatesByClasses.Add(className, predicates);
            }

            _logger.LogDebug("End : Init");

            return true;
        }

        private bool LoadSourceOrTarget(string endpoint, string rdfFilePath, string name, string namedGraphUri)
        {
            if (name != "source" && name != "target") throw new Exception("name must be either 'source' or 'target'");
            if (!string.IsNullOrWhiteSpace(endpoint))
            {
                _logger.LogDebug($"{name} loaded with : {endpoint}");
                var sparqlRemoteEndpoint = new SparqlRemoteEndpoint(UriFactory.Create(endpoint));
                if (name == "source") source = new SparqlQueryable(sparqlRemoteEndpoint, namedGraphUri);
                else target = new SparqlQueryable(sparqlRemoteEndpoint, namedGraphUri);
            }
            else if (!string.IsNullOrWhiteSpace(rdfFilePath))
            {
                _logger.LogDebug($"{name} loaded with : {rdfFilePath}");
                var graph = new Graph();
                graph.LoadFromFile(rdfFilePath);
                if (name == "source") source = new SparqlQueryable(graph);
                else target = new SparqlQueryable(graph);
            }
            else
            {
                _logger.LogCritical($"No {name} configured");
                return false;
            }
            return true;
        }


        // private List<string> sourceSubjects {get;set;}
        private Dictionary<string, HashSet<string>> PredicatesByClasses { get; set; }
        private List<string> sourceSubjects { get; set; }
        private List<string> targetSubjects { get; set; }

        public (List<string>, List<string>) GetSubjects()
        {
            return (sourceSubjects, targetSubjects);
        }

        public Task SaveTranslations(string translationsPath)
        {
            if (translations.Any() && initialTranslationsCount < translations.Count)
            {
                _logger.LogInformation($"Saving translations in {translationsPath}");
                return System.IO.File.WriteAllLinesAsync(translationsPath, translations.Select(t => $"{t.Key}\t{t.Value}"));
            }
            return Task.CompletedTask;
        }




        #endregion

        #region Utilities

        private double GetNbSubjectsForConcept(SparqlQueryable endpoint, string conceptName)
        {
            if (ntc.ContainsKey(conceptName)) return ntc[conceptName];
            return 0d;
        }
        /// <summary>
        /// triples such as <?x :roleName ?o> and <?x a :conceptName>
        /// </summary>
        /// <param name="endpoint"></param>
        /// <param name="conceptName"></param>
        /// <param name="roleName"></param>
        /// <returns></returns>
        private double GetNbSubjectsForConceptWithRole(SparqlQueryable endpoint, string conceptName, string roleName)
        {
            if (ntcp.ContainsKey(conceptName) && ntcp[conceptName].ContainsKey(roleName)) return ntcp[conceptName][roleName];
            return 0d;
        }
        #endregion



        public async Task<SimilarityResult> SimilarityComputation(string uri1, string uri2)
        {
            // if (uri1 == "http://big.csr.unibo.it/sabine-eng.owl#Antonis_Samaras" && uri2 == "http://big.csr.unibo.it/sabine-ita.owl#Mario_Mauro")
            // {
            //     //sourceTriples.Any(t => t.s == "http://big.csr.unibo.it/sabine-eng.owl#Antonis_Samaras" && t.p == Vocabulary.OWL.sameAs.AbsoluteUri && t.o == "http://big.csr.unibo.it/sabine-ita.owl#Mario_Mauro")
            //     _logger.LogDebug(uri1);

            // }
            if (uri1.Equals(uri2) || sourceTriples.Any(t => t.s == uri1 && t.p == Vocabulary.OWL.sameAs.AbsoluteUri && t.o == uri2))
            {
                var sts = new SubScore
                {
                    s = uri1,
                    t = uri2,
                    w = 1d,
                    n = new List<SubSubScore> {
                    new SubSubScore {c = 1, p = Vocabulary.OWL.sameAs.AbsoluteUri, v = 1, o = 1, m=1}
                }
                };
                subScores.Add(sts);
                return new SimilarityResult
                {
                    Result = 1d,
                    Uri1 = uri1,
                    Uri2 = uri2,
                    IsSameAs = true
                };
            }
            var triples1 = sourceTriples.Where(t => t.s == uri1).ToArray();
            var triples2 = targetTriples.Where(t => t.s == uri2).ToArray();
            var typesIntersection = triples1.Where(t => t.p == Vocabulary.RDF.type.AbsoluteUri).Select(t => t.o)
                .Intersect(triples2.Where(t => t.p == Vocabulary.RDF.type.AbsoluteUri).Select(t => t.o)).ToHashSet();//types1.Intersect(types2).ToHashSet();

            var typesWithLevel = new Dictionary<string, int>();
            foreach (var type in typesIntersection)
            {
                var lvl = ConceptDistances.Keys.Contains(type.ToString()) ? (ConceptDistances[type.ToString()].Select(t => t.Value).Max()) : -1;
                typesWithLevel.Add(type, lvl);
            }
            // TODO: prendre en comte tous les types qui ont la meme distance max !!!
            var commonClass = typesWithLevel.Where(o => !o.Key.StartsWith(Vocabulary.OWL.NameSpace) && !o.Key.StartsWith(Vocabulary.RDFS.NameSpace) && !o.Key.StartsWith(Vocabulary.RDF.NameSpace) && !o.Key.StartsWith(Vocabulary.FOAF.NameSpace))
                .Any() ? typesWithLevel.Where(o => !o.Key.StartsWith(Vocabulary.OWL.NameSpace) && !o.Key.StartsWith(Vocabulary.RDFS.NameSpace) && !o.Key.StartsWith(Vocabulary.RDF.NameSpace) && !o.Key.StartsWith(Vocabulary.FOAF.NameSpace))
                    .OrderByDescending(t => t.Value).First().Key.ToString() : Vocabulary.OWL.Thing.AbsoluteUri;

            // // TODO: on peut essayer de pondérer par la classe : plus le niveau est bas, mieux c'est : on veut privilégier 2 scientifiques plutot qu'un scientifique et un guitarise par exemple !
            // if (uri1 == "http://big.csr.unibo.it/sabine-eng.owl#Antonis_Samaras" && uri2 == "http://big.csr.unibo.it/sabine-ita.owl#Antonis_Samaras")
            // {
            //     // c devrait être Politician :
            //     // http://big.csr.unibo.it/sabine.owl#Politician
            //     // http://big.csr.unibo.it/sabine.owl#Politician
            //     // TODO: il y a un bug, owl thing devrait être là et trouver comment utiliser le poids de la classe commune ??!!
            //     _logger.LogDebug(commonClass);
            //     // C'est ce couple qui devrait être trouvé !
            //     // var distThingThing = ConceptDistances["http://big.csr.unibo.it/sabine.owl#Politician"]["http://big.csr.unibo.it/sabine.owl#Politician"].ToString(); // TODO: ici ça plante alors que la distance devrait etre 0
            //     // _logger.LogDebug(distThingThing);

            // }



            var predicates = triples1.Select(t => t.p).Intersect(triples2.Select(t => t.p)).ToArray();

            _logger.LogDebug($"x1 : {uri1} // x2 : {uri2} // class : {commonClass} // # predicates : {predicates.Length}");

            var subscores = new List<SubSubScore>();
            // var score = 0d;PredicatesByClasses
            // foreach (var predicate in predicates.Where(p => p != Vocabulary.RDF.type.AbsoluteUri)) // TODO: modifier PDF car on prend plus en compte le type
            foreach (var predicate in PredicatesByClasses[commonClass].Where(p => p != Vocabulary.RDF.type.AbsoluteUri)) // TODO: modifier PDF car on prend plus en compte le type
            {
                // if (predicate == Vocabulary.OWL.sameAs.AbsoluteUri) {
                //     var cou = "";
                // }
                var objs1 = triples1.Where(t => t.p.Equals(predicate)).Select(t => t.o).ToArray();

                var objs2 = triples2.Where(t => t.p.Equals(predicate)).Select(t => t.o).ToArray();

                var similarities = await MostSimilarCommonObject(objs1.Select(n => Tools.ToINode(n)), objs2.Select(n => Tools.ToINode(n)), stringSimilarity, sameAsCouples, translations);

                var mostSimilarCommonObjectValue = similarities.Any() ? similarities.OrderByDescending(s => s.Item1).First() : (0, null);

                var ntcValue = GetNbSubjectsForConcept(source, commonClass);

                var ntcpValue = GetNbSubjectsForConceptWithRole(source, commonClass, predicate);

                // var weight = ntcpValue / ntcValue;

                var nscpoValue = mostSimilarCommonObjectValue.Item2 != null && nscpo.ContainsKey(commonClass) && nscpo[commonClass].ContainsKey(predicate) && nscpo[commonClass][predicate].ContainsKey(mostSimilarCommonObjectValue.Item2) ?
                    nscpo[commonClass][predicate][mostSimilarCommonObjectValue.Item2] : 0d;

                // var discriminant = 1d - (nscpoValue / ntcpValue);

                var subScore = new SubSubScore
                {
                    p = predicate,
                    c = ntcValue,
                    v = ntcpValue,
                    o = nscpoValue,
                    m = mostSimilarCommonObjectValue.Item1
                };
                subscores.Add(subScore);
            }
            var w = triples1.Select(t => t.p).Where(p => p != Vocabulary.RDF.type.AbsoluteUri).Intersect(triples2.Select(t => t.p).Where(p => p != Vocabulary.RDF.type.AbsoluteUri)).Count() / (double)(
                triples1.Select(t => t.p).Where(p => p != Vocabulary.RDF.type.AbsoluteUri).Distinct().Count() +
                triples2.Select(t => t.p).Where(p => p != Vocabulary.RDF.type.AbsoluteUri).Distinct().Count() +
                triples1.Select(t => t.p).Where(p => p != Vocabulary.RDF.type.AbsoluteUri).Intersect(triples2.Select(t => t.p).Where(p => p != Vocabulary.RDF.type.AbsoluteUri)).Count()
            );// TODO: modifier PDF car on prend plus en compte le type
            var scoreToSave = new SubScore { s = uri1, t = uri2, w = w, n = subscores }; // TODO:sauvegarder ça pour analyse ensuite            
            subScores.Add(scoreToSave);


            _logger.LogDebug($"w : {w}");
            var resultScore = aggregation.Aggregate(scoreToSave);
            // var resultScore = scores.Any() ? scores
            //     // .Select(sub => sub.score)
            //     .Average() * w : 0d;
            // var resultScore = score * w;

            _logger.LogDebug($"resultScore : {resultScore}");
            return new SimilarityResult
            {
                Result = resultScore,
                Uri1 = uri1,
                Uri2 = uri2,
                // SubScores = scores,
                IsSameAs = triples1.Any(t => t.p.Equals(Vocabulary.OWL.sameAs.AbsoluteUri) && t.o.Equals(uri2))
                    || triples2.Any(t => t.p.Equals(Vocabulary.OWL.sameAs) && t.o.Equals(uri1))
            };
        }




        public ConcurrentBag<SubScore> subScores { get; set; }

        /// <summary>
        /// Return 0 if there are no common objects between the two arrays. Return 1 if there is a perfect match.
        /// Else a value between 0 and 1 proportionnaly to the similarity.
        /// </summary>
        /// <param name="sourceObjects"></param>
        /// <param name="targetObjects"></param>
        /// <returns></returns>
        public static async Task<IEnumerable<(double score, string obj)>> MostSimilarCommonObject(IEnumerable<INode> sourceObjects, IEnumerable<INode> targetObjects, F23.StringSimilarity.Interfaces.IStringSimilarity stringSimilarity, Dictionary<string, HashSet<string>> sameAsCouples, ConcurrentDictionary<string, string> translations)
        {
            var similarities = new List<(double, string)>();
            var sw = new System.Diagnostics.Stopwatch();
            // on ordonne pour vérifier d'abord les URI puis les literaux sans langue et enfin ceux avec langue
            foreach (var sourceObject in sourceObjects.OrderBy(n => (n.NodeType == NodeType.Literal && string.IsNullOrEmpty((n as LiteralNode).Language)) ? (n as LiteralNode).Language : n.NodeType.ToString()))
            {
                var sourceType = sourceObject.NodeType;
                foreach (var targetObject in targetObjects.OrderBy(n => (n.NodeType == NodeType.Literal && string.IsNullOrEmpty((n as LiteralNode).Language)) ? (n as LiteralNode).Language : n.NodeType.ToString()))
                {
                    var targetType = targetObject.NodeType;
                    if (sourceType != targetType)
                    {
                        similarities.Add((0, sourceObject.ToSafeString()));
                        continue;
                    }
                    switch (sourceType)
                    {
                        case NodeType.Literal:
                            var lit1 = sourceObject as LiteralNode;
                            var lit2 = targetObject as LiteralNode;
                            // if (lit1.Value == "1" && lit1.DataType.AbsoluteUri == "http://www.w3.org/2001/XMLSchema#int")
                            // {
                            //     var s = lit1.ToString();
                            // }
                            similarities.Add((await SimilarityBetweenLiterals(lit1, lit2, stringSimilarity, translations), lit1.ToString())); // on utilise lit1.ToString() plutot quelit1.Value pour avoir le type avec sinon la valeur n'est pas trouvée dans le dict
                            break;
                        case NodeType.Uri:
                            await CompareUriNode(sourceObject, targetObject, sameAsCouples, stringSimilarity, similarities, translations);
                            break;
                        default:
                            similarities.Add((0, null));
                            break;
                    }
                    // if (similarities.Any(s => s.Item1 == 1d)) return similarities;//similarities.First(s => s.Item1 == 1d);
                }
            }
            return similarities.GroupBy(g => g.Item2).Select(x => x.OrderByDescending(e => e.Item1).First());
        }

        public static async Task CompareUriNode(INode sourceObject, INode targetObject, Dictionary<string, HashSet<string>> sameAsCouples, F23.StringSimilarity.Interfaces.IStringSimilarity stringSimilarity, List<(double, string)> similarities, ConcurrentDictionary<string, string> translations)
        {
            var uri1 = sourceObject as UriNode;
            var uri2 = targetObject as UriNode;
            // TODO: Est-ce qu'on introduit de la récursivité, cad similarities.Add(SimilarityComputation(uri1, uri2)) ???

            if (AreResourcesEquals(uri1, uri2, sameAsCouples)) similarities.Add((1, uri1.Uri.AbsoluteUri));
            else
            {
                // comparer les fragments ??
                /* 
                    EXEMPLE :
                    // récupération du fragment
                    "Leader_di_partito" <- <http://big.csr.unibo.it/sabine-ita.owl#Leader_di_partito>
                    // on ne garde que les chiffres et les lettres
                    "Leader di partito" <- "Leader_di_partito"
                    // On traduit en anglais
                    "Party leader" <- "Leader di partito"
                 */
                var f1 = Tools.RemoveNonAlphaNum(Tools.GetFragment(uri1));
                var f2 = Tools.RemoveNonAlphaNum(Tools.GetFragment(uri2));
                if (f1 == f2) similarities.Add((1, uri1.Uri.AbsoluteUri));
                else
                {
                    var s1 = await Translate(f1, translations);
                    var s2 = await Translate(f2, translations);
                    var sim = stringSimilarity.Similarity(s1, s2);
                    //similarities.Add(0);
                    similarities.Add((sim, uri1.Uri.AbsoluteUri));
                }
            }
        }




        private static async Task<string> Translate(string strToTranslate, ConcurrentDictionary<string, string> translations)
        {
            if (translations.ContainsKey(strToTranslate))
            {
                return translations[strToTranslate];
            }
            else
            {
                var json = await Tools.Translate(strToTranslate, "en");
                var strTranslated = Tools.GetTranslationValue(json);
                translations.TryAdd(strToTranslate, strTranslated);
                return strTranslated;
            }
        }
        private static async Task<string> Translate(string strToTranslate, string sourceLang, ConcurrentDictionary<string, string> translations)
        {
            if (translations.ContainsKey(strToTranslate))
            {
                return translations[strToTranslate];
            }
            else
            {
                var json = await Tools.Translate(strToTranslate, sourceLang, "en");
                var strTranslated = Tools.GetTranslationValue(json);
                translations.TryAdd(strToTranslate, strTranslated);
                return strTranslated;
            }
        }
        private static async Task<double> SimilarityBetweenLiterals(ILiteralNode n1, ILiteralNode n2, F23.StringSimilarity.Interfaces.IStringSimilarity stringSimilarity, ConcurrentDictionary<string, string> translations)
        {
            //var cosine = new F23.StringSimilarity.Cosine(1);
            if (n1.DataType != null && n2.DataType != null && n1.DataType.Equals(n2.DataType))
            {
                if (n1.Value.Equals(n2.Value))
                    return 1d;
                switch (n1.DataType.ToString())
                {
                    case "http://www.w3.org/2001/XMLSchema#string":
                        break;
                    case "http://www.w3.org/2001/XMLSchema#date":
                        if (AreDateEquals(n1.Value, n2.Value))
                            return 1d;
                        break;

                }
                /*
                <http://www.w3.org/1999/02/22-rdf-syntax-ns#langString>
                <http://www.w3.org/2001/XMLSchema#anyURI>
                <http://www.w3.org/2001/XMLSchema#boolean>
                <http://www.w3.org/2001/XMLSchema#date>
                <http://www.w3.org/2001/XMLSchema#double>
                <http://www.w3.org/2001/XMLSchema#float>
                <http://www.w3.org/2001/XMLSchema#gYear>
                <http://www.w3.org/2001/XMLSchema#gYearMonth>
                <http://www.w3.org/2001/XMLSchema#integer>
                <http://www.w3.org/2001/XMLSchema#nonNegativeInteger>
                <http://www.w3.org/2001/XMLSchema#positiveInteger>
                <http://www.w3.org/2001/XMLSchema#string>
                 */
            }
            else if (!string.IsNullOrEmpty(n1.Language) && !string.IsNullOrEmpty(n2.Language) && n1.Language != n2.Language)
            {
                string t1;
                if (n1.Language == "en")
                {
                    t1 = n1.Value;
                }
                else
                {
                    t1 = await Translate(n1.Value, n1.Language, translations);
                    // if (translations.ContainsKey(n1.Value))
                    // {
                    //     t1 = translations[n1.Value];
                    // }
                    // else
                    // {
                    //     var json = await Tools.Translate(n1.Value, n1.Language, "en");
                    //     t1 = Tools.GetTranslationValue(json);
                    //     translations.TryAdd(n1.Value, t1);
                    // }
                }
                string t2;
                if (n2.Language == "en")
                {
                    t2 = n2.Value;
                }
                else
                {
                    t2 = await Translate(n2.Value, n2.Language, translations);
                    // if (translations.ContainsKey(n2.Value))
                    // {
                    //     t2 = translations[n2.Value];
                    // }
                    // else
                    // {
                    //     var json = await Tools.Translate(n2.Value, n2.Language, "en");
                    //     t2 = Tools.GetTranslationValue(json);
                    //     translations.TryAdd(n2.Value, t2);
                    // }
                }
                var similarityTranslation = stringSimilarity.Similarity(t1, t2);
                return similarityTranslation;
            }
            // n1 n'a pas de langage mais n2 oui
            // ou n1 a un langage mais pas n2
            // ou les datatypes sont différents
            // ou l'un des deux a un datatype null
            // ou les deux sont des string
            // ou n1 et n2 ont la meme langue
            var similarity = stringSimilarity.Similarity(n1.Value, n2.Value);
            return similarity;
        }


        private static bool AreDateEquals(string s1, string s2)
        {
            DateTime dt1;
            if (DateTime.TryParse(s1, out dt1))
            {
                DateTime dt2;
                if (DateTime.TryParse(s2, out dt2))
                {
                    var ts = dt1 > dt2 ? dt1 - dt2 : dt2 - dt1;
                    var pourcentage = ts.Ticks * 100d / dt1.Ticks;
                    if (pourcentage < 0.01d)
                        return true;
                }
            }
            return false;
        }

        /// <summary>
        /// Return true if there is any semantic proof that the 2 uris are linked by a sameAs.
        /// </summary>
        /// <param name="uri1"></param>
        /// <param name="uri2"></param>
        /// <returns></returns>
        public static bool AreResourcesEquals(IUriNode uri1, IUriNode uri2, Dictionary<string, HashSet<string>> sameAsCouples)
        {
            if (uri1.Uri.AbsoluteUri.Equals(uri2.Uri.AbsoluteUri)) return true;
            return sameAsCouples.ContainsKey(uri1.Uri.AbsoluteUri) && sameAsCouples[uri1.Uri.AbsoluteUri].Contains(uri2.Uri.AbsoluteUri);
        }


    }

}