using System;
using VDS.RDF;
using VDS.RDF.Query;
using System.Linq;
using System.Collections.Generic;
using static System.Console;
using VDS.RDF.Parsing;
using System.Text;
using System.Threading.Tasks;
using Newtonsoft.Json;

namespace PrototypeLib
{
    /// <summary>
    /// Class containing tools and helpers. TODO: clean this mess
    /// </summary>
    public static class Tools
    {
        public static IGraph GetContextualGraph(this SparqlRemoteEndpoint endpoint, string subject)
            => endpoint.Construct("CONSTRUCT { <" + subject + "> ?p1 ?o1 . ?s2 ?p2 <" + subject + "> . } WHERE { { <" + subject + "> ?p1 ?o1 . } UNION { ?s2 ?p2 <" + subject + "> . } }");

        public static string GetUriOrValue(INode node)
        {
            if (node is UriNode)
            {
                return (node as UriNode).Uri.AbsoluteUri;
            }
            if (node is LiteralNode)
            {
                return node.ToString();
                // return (node as LiteralNode).Value;
            }
            return node.ToString();
        }

        public static IEnumerable<Alignment> GetRefalignResources(string filePath)
        {
            if (!System.IO.File.Exists(filePath)) throw new System.IO.FileNotFoundException(filePath);
            var refalignGraph = new Graph();
            refalignGraph.LoadFromFile(filePath);
            if (refalignGraph.IsEmpty) throw new Exception($"Graph empty : {filePath}");
            // var res = refalignGraph.Triples.Select(t => t.Predicate).Distinct();//.GetTriplesWithPredicate(UriFactory.Create("http://knowledgeweb.semanticweb.org/heterogeneity/alignment#entity1"));
            // foreach (var item in res)
            // {
            //     WriteLine(item);
            // }
            return refalignGraph.Select("SELECT ?p1 ?p2 WHERE { ?s (<http://knowledgeweb.semanticweb.org/heterogeneity/alignment#entity1>|<http://knowledgeweb.semanticweb.org/heterogeneity/alignmententity1>) ?p1 . ?s (<http://knowledgeweb.semanticweb.org/heterogeneity/alignment#entity2>|<http://knowledgeweb.semanticweb.org/heterogeneity/alignmententity2>) ?p2 . }")
            .Select(r => new Alignment
            {
                Source = ((UriNode)r["p1"]).Uri.ToString(), 
                Target = ((UriNode)r["p2"]).Uri.ToString()
            });
        }
        

        public static bool IsNullOrWhiteSpace(this string s) => String.IsNullOrWhiteSpace(s);

        public static async Task<SparqlResultSet> SelectQuery(this string endpointUri, string sparqlQuery)
        {
            WriteLine($"Querying : {endpointUri} whith \"{sparqlQuery}\"");
            SparqlResultSet results = new SparqlResultSet();
            using (var client = new System.Net.Http.HttpClient())
            {
                try
                {
                    client.Timeout = new TimeSpan(23, 23, 59, 59, 59);
                    var httpQuery = Tools.UrlEncode(sparqlQuery);
                    var response = await client.GetAsync(endpointUri + "?query=" + httpQuery);
                    response.EnsureSuccessStatusCode();
                    ISparqlResultsHandler handler = new VDS.RDF.Parsing.Handlers.ResultSetHandler(results);

                    String ctype = response.Content.Headers.ContentType.ToString();

                    if (ctype.Contains(";"))
                    {
                        ctype = ctype.Substring(0, ctype.IndexOf(";"));
                    }

                    ISparqlResultsReader resultsParser = MimeTypesHelper.GetSparqlParser(ctype);
                    resultsParser.Load(handler, new System.IO.StreamReader(response.Content.ReadAsStreamAsync().Result));

                }
                catch (System.Net.Http.HttpRequestException e)
                {
                    Console.WriteLine("\nException Caught !");
                    WriteLine($"endpoint : {endpointUri}");
                    WriteLine($"sparqlQuery : {sparqlQuery}");
                    Console.WriteLine("Message :{0} ", e.Message);
                }
                return results;
            }
        }
        public static Task<SparqlResultSet> SelectQuery(this SparqlQueryable endpoint, string sparqlQuery)
        {
            if (endpoint.IsEndpoint())
            {
                return endpoint.GetUri().AbsoluteUri.SelectQuery(sparqlQuery);
            }
            return Task.Factory.StartNew( () => endpoint.GetGraph().Select(sparqlQuery) );
            
        }

        public static bool IsEndpointAlive(string endpointUri)
        {  
            var sparqlEndpoint = new SparqlRemoteEndpoint(new Uri(endpointUri));
            return sparqlEndpoint.Ask("ASK {?s ?p ?o .}");
        }

        
        public static String UrlEncode(String value)
        {
            if (!IsUnsafeUrlString(value))
            {
                return value;
            }
            else
            {
                char c, d, e;
                StringBuilder output = new StringBuilder();
                for (int i = 0; i < value.Length; i++)
                {
                    c = value[i];
                    if (!IsSafeCharacter(c))
                    {
                        if (c == '%')
                        {
                            if (i <= value.Length - 2)
                            {
                                d = value[i + 1];
                                e = value[i + 2];
                                if (IriSpecsHelper.IsHexDigit(d) && IriSpecsHelper.IsHexDigit(e))
                                {
                                    // Has valid hex digits after it so continue encoding normally
                                    output.Append(c);
                                }
                                else
                                {
                                    // Need to encode a bare percent character
                                    output.Append(PercentEncode(c));
                                }
                            }
                            else
                            {
                                // Not enough characters after a % to use as a valid escape so encode the percent
                                output.Append(PercentEncode(c));
                            }
                        }
                        else
                        {
                            // Contains an unsafe character so percent encode
                            output.Append(PercentEncode(c));
                        }
                    }
                    else
                    {
                        // No need to encode safe characters
                        output.Append(c);
                    }
                }
                return output.ToString();
            }
        }
        private static bool IsSafeCharacter(char c)
        {
            // Safe characters which should not be percent encoded per RFC 3986 Section 2.3
            // http://tools.ietf.org/html/rfc3986#section-2.3
            // Alpha (65-90 and 97-122), Digits (48-57), Hyphen (45), Period (46), Underscore (95) and Tilde (126)
            if ((c >= 65 && c <= 90) || (c >= 97 && c <= 122) || (c >= 48 && c <= 57) || c == 45 || c == 46 || c == 95 || c == 126)
            {
                return true;
            }
            else
            {
                return false;
            }
        }
        private static bool IsUnsafeUrlString(String value)
        {
            char c, d, e;
            for (int i = 0; i < value.Length; i++)
            {
                c = value[i];
                if (!IsSafeCharacter(c))
                {
                    if (c == '%')
                    {
                        if (i <= value.Length - 2)
                        {
                            d = value[i + 1];
                            e = value[i + 2];
                            if (IriSpecsHelper.IsHexDigit(d) && IriSpecsHelper.IsHexDigit(e))
                            {
                                i += 2;
                                continue;
                            }
                            else
                            {
                                // Expected two hex digits after a % as an escape
                                return true;
                            }
                        }
                        else
                        {
                            // Not enough characters after a % to use as a valid escape
                            return true;
                        }
                    }
                    else
                    {
                        // Contains an unsafe character
                        return true;
                    }
                }
            }

            // All Characters OK
            return false;
        }
        private static String PercentEncode(char c)
        {
            if (c <= 255)
            {
                // Can be encoded in a single percent encode
                if (c <= 127)
                {
                    return "%" + ((int)c).ToString("X2");
                }
                else
                {
                    byte[] codepoints = Encoding.UTF8.GetBytes(new char[] { c });
                    StringBuilder output = new StringBuilder();
                    foreach (byte b in codepoints)
                    {
                        output.Append("%");
                        output.Append(((int)b).ToString("X2"));
                    }
                    return output.ToString();
                }
            }
            else
            {
                // Unicode character so requires more than one percent encode
                byte[] codepoints = Encoding.UTF8.GetBytes(new char[] { c });
                StringBuilder output = new StringBuilder();
                foreach (byte b in codepoints)
                {
                    output.Append("%");
                    output.Append(((int)b).ToString("X2"));
                }
                return output.ToString();
            }
        }


        public static IGraph LoadOnto(IGraph graph, IEnumerable<IUriNode> predicates)
        {
            if (predicates != null && predicates.Any())
            {
                var firstPredicate = predicates.First();
                if (!graph.GetTriplesWithSubject(firstPredicate).Any())
                {
                    graph.LoadFromUri(firstPredicate.Uri);
                }
                return LoadOnto(graph, predicates.Skip(1));
            }
            return graph;
        }

       
        public static bool Ask(this SparqlRemoteEndpoint endpoint, string askQuery)
        {
            try
            {
                var result = endpoint.QueryWithResultSet(askQuery);//VDS.RDF.Query.RdfQueryException
                return result != null ? result.Result : false;
            }
            catch (Exception ex)
            {
                if (ex is System.Net.WebException || ex is VDS.RDF.Query.RdfQueryException || ex is VDS.RDF.Query.RdfQueryTimeoutException)
                {
                    WriteLine(ex);
                }
                else
                {
                    throw;
                }
            }
            return false;
        }
       
        public static SparqlResultSet Select(this IGraph graph, string selectQuery)
        {
            return (SparqlResultSet)graph.ExecuteQuery(selectQuery);
        }
        public static SparqlResultSet Select(this SparqlRemoteEndpoint endpoint, string selectQuery)
        {
            return endpoint.QueryWithResultSet(selectQuery);
        }


        public static IGraph Construct(this SparqlRemoteEndpoint endpoint, string constructQuery)
        {
            try
            {
                return endpoint.QueryWithResultGraph(constructQuery);
            }
            catch (Exception ex)
            {
                if (ex is System.Net.WebException || ex is VDS.RDF.Query.RdfQueryException)
                {
                    WriteLine(ex);
                }
                else
                {
                    throw;
                }
            }
            return new Graph();
        }

        
        /// <summary>
        /// Parse a json result from Translate function to abtain the translation
        /// </summary>
        /// <param name="json"></param>
        /// <returns></returns>
        public static string GetTranslationValue(string json)
        {
            var array = (Newtonsoft.Json.Linq.JArray)JsonConvert.DeserializeObject(json);
            var results = new List<string>();
            foreach (var phrase in array.First)
            {
                results.Add(phrase.First().ToString().Trim());
            }
            var result = string.Join(" ", results).Trim();
            return result;
        }

        /// <summary>
        /// send a request to translate.googleapis.com to translate sourceText from sourceLang to targetLang
        /// </summary>
        /// <param name="sourceText"></param>
        /// <param name="sourceLang"></param>
        /// <param name="targetLang"></param>
        /// <returns></returns>
        public static async Task<string> Translate(string sourceText, string sourceLang, string targetLang)
        {
            var requestQuery = "https://translate.googleapis.com/translate_a/single?client=gtx&sl=" 
            + sourceLang + "&tl=" + targetLang + "&dt=t&q=" + Tools.UrlEncode(sourceText);
            string content = null;
            using (var client = new System.Net.Http.HttpClient())
            {
                try
                {
                    client.Timeout = new TimeSpan(23, 23, 59, 59, 59);
                    var response = await client.GetAsync(requestQuery);
                    response.EnsureSuccessStatusCode();

                    content = await response.Content.ReadAsStringAsync();

                }
                catch (System.Net.Http.HttpRequestException e)
                {
                    Console.WriteLine(e);
                    throw new Exception(e.ToString());
                }
            }
            return content;
        }

        public static async Task<string> Translate(string sourceText, string targetLang)
        {
            var requestQuery = "https://translate.googleapis.com/translate_a/single?client=gtx&sl=auto&tl=" + targetLang + "&dt=t&q=" + Tools.UrlEncode(sourceText);
            string content = null;
            using (var client = new System.Net.Http.HttpClient())
            {
                try
                {
                    client.Timeout = new TimeSpan(23, 23, 59, 59, 59);
                    var response = await client.GetAsync(requestQuery);
                    response.EnsureSuccessStatusCode();

                    content = await response.Content.ReadAsStringAsync();

                }
                catch (System.Net.Http.HttpRequestException e)
                {
                    Console.WriteLine(e);
                    throw new Exception(e.ToString());
                }
            }
            return content;
        }


        public static IEnumerable<Triple> GetMaxContextualGraph(this IGraph graph, UriNode uriNode, HashSet<UriNode> processed)
        {
            var triples = graph.GetTriples(uriNode).ToList();
            var triplesToAdd = new List<Triple>();
            foreach (var triple in triples)
            {
                var subject = triple.Subject.NodeType == NodeType.Uri ? (UriNode)triple.Subject : null;
                if (!processed.Contains(subject))
                {
                    processed.Add(subject);
                    triplesToAdd.AddRange(GetMaxContextualGraph(graph, subject, processed));
                }
                var obj = triple.Object.NodeType == NodeType.Uri ? (UriNode)triple.Object : null;
                if (obj != null && !processed.Contains(obj))
                {
                    processed.Add(obj);
                    triplesToAdd.AddRange(GetMaxContextualGraph(graph, obj, processed));
                }
            }
            triples.AddRange(triplesToAdd);
            return triples.Distinct();
        }

        public static IEnumerable<Triple> GetMaxContextualGraph(this SparqlRemoteEndpoint endpoint, UriNode uriNode, HashSet<UriNode> processed)
        {
            var triples = endpoint.GetTriples(uriNode).ToList();
            var triplesToAdd = new List<Triple>();
            foreach (var triple in triples)
            {
                var subject = triple.Subject.NodeType == NodeType.Uri ? (UriNode)triple.Subject : null;
                if (!processed.Contains(subject))
                {
                    processed.Add(subject);
                    triplesToAdd.AddRange(GetMaxContextualGraph(endpoint, subject, processed));
                }
                var obj = triple.Object.NodeType == NodeType.Uri ? (UriNode)triple.Object : null;
                if (obj != null && !processed.Contains(obj))
                {
                    processed.Add(obj);
                    triplesToAdd.AddRange(GetMaxContextualGraph(endpoint, obj, processed));
                }
            }
            triples.AddRange(triplesToAdd);
            return triples.Distinct();
        }
        public static IEnumerable<Triple> GetMaxContextualGraph(this SparqlRemoteEndpoint endpoint, Uri uri, HashSet<UriNode> processed)
        {
            var triples = endpoint.GetTriples(uri).ToList();
            var triplesToAdd = new List<Triple>();
            foreach (var triple in triples)
            {
                var subject = triple.Subject.NodeType == NodeType.Uri ? (UriNode)triple.Subject : null;
                if (!processed.Contains(subject))
                {
                    processed.Add(subject);
                    triplesToAdd.AddRange(GetMaxContextualGraph(endpoint, subject.Uri, processed));
                }
                var obj = triple.Object.NodeType == NodeType.Uri ? (UriNode)triple.Object : null;
                if (obj != null && !processed.Contains(obj))
                {
                    processed.Add(obj);
                    triplesToAdd.AddRange(GetMaxContextualGraph(endpoint, obj.Uri, processed));
                }
            }
            triples.AddRange(triplesToAdd);
            return triples.Distinct();
        }

        public static INode ToINode(string node, IGraph g)
        {
            INode n;
            if (node.StartsWith("http"))
            {
                n = g.CreateUriNode(UriFactory.Create(node));
            }
            else if (node.Contains("^^"))
            {
                // lit ac datatype
                var dt = node.Split("^^")[1];
                Uri nodeType;
                switch (dt)
                {
                    case XmlSpecsHelper.XmlSchemaDataTypeDate:
                        nodeType = UriFactory.Create(XmlSpecsHelper.XmlSchemaDataTypeDate);
                        break;
                    case XmlSpecsHelper.XmlSchemaDataTypeString:
                        nodeType = UriFactory.Create(XmlSpecsHelper.XmlSchemaDataTypeString);
                        break;
                    case XmlSpecsHelper.XmlSchemaDataTypeAnyUri:
                        nodeType = UriFactory.Create(XmlSpecsHelper.XmlSchemaDataTypeAnyUri);
                        break;
                    case XmlSpecsHelper.XmlSchemaDataTypeBase64Binary:
                        nodeType = UriFactory.Create(XmlSpecsHelper.XmlSchemaDataTypeBase64Binary);
                        break;
                    case XmlSpecsHelper.XmlSchemaDataTypeBoolean:
                        nodeType = UriFactory.Create(XmlSpecsHelper.XmlSchemaDataTypeBoolean);
                        break;
                    case XmlSpecsHelper.XmlSchemaDataTypeByte:
                        nodeType = UriFactory.Create(XmlSpecsHelper.XmlSchemaDataTypeByte);
                        break;
                    case XmlSpecsHelper.XmlSchemaDataTypeDateTime:
                        nodeType = UriFactory.Create(XmlSpecsHelper.XmlSchemaDataTypeDateTime);
                        break;
                    case XmlSpecsHelper.XmlSchemaDataTypeDayTimeDuration:
                        nodeType = UriFactory.Create(XmlSpecsHelper.XmlSchemaDataTypeDayTimeDuration);
                        break;
                    case XmlSpecsHelper.XmlSchemaDataTypeDecimal:
                        nodeType = UriFactory.Create(XmlSpecsHelper.XmlSchemaDataTypeDecimal);
                        break;
                    case XmlSpecsHelper.XmlSchemaDataTypeDouble:
                        nodeType = UriFactory.Create(XmlSpecsHelper.XmlSchemaDataTypeDouble);
                        break;
                    case XmlSpecsHelper.XmlSchemaDataTypeDuration:
                        nodeType = UriFactory.Create(XmlSpecsHelper.XmlSchemaDataTypeDuration);
                        break;
                    case XmlSpecsHelper.XmlSchemaDataTypeFloat:
                        nodeType = UriFactory.Create(XmlSpecsHelper.XmlSchemaDataTypeFloat);
                        break;
                    case XmlSpecsHelper.XmlSchemaDataTypeHexBinary:
                        nodeType = UriFactory.Create(XmlSpecsHelper.XmlSchemaDataTypeHexBinary);
                        break;
                    case XmlSpecsHelper.XmlSchemaDataTypeInt:
                        nodeType = UriFactory.Create(XmlSpecsHelper.XmlSchemaDataTypeInt);
                        break;
                    case XmlSpecsHelper.XmlSchemaDataTypeInteger:
                        nodeType = UriFactory.Create(XmlSpecsHelper.XmlSchemaDataTypeInteger);
                        break;
                    case XmlSpecsHelper.XmlSchemaDataTypeLong:
                        nodeType = UriFactory.Create(XmlSpecsHelper.XmlSchemaDataTypeLong);
                        break;
                    case XmlSpecsHelper.XmlSchemaDataTypeNegativeInteger:
                        nodeType = UriFactory.Create(XmlSpecsHelper.XmlSchemaDataTypeNegativeInteger);
                        break;
                    case XmlSpecsHelper.XmlSchemaDataTypeNonNegativeInteger:
                        nodeType = UriFactory.Create(XmlSpecsHelper.XmlSchemaDataTypeNonNegativeInteger);
                        break;
                    case XmlSpecsHelper.XmlSchemaDataTypeNonPositiveInteger:
                        nodeType = UriFactory.Create(XmlSpecsHelper.XmlSchemaDataTypeNonPositiveInteger);
                        break;
                    case XmlSpecsHelper.XmlSchemaDataTypePositiveInteger:
                        nodeType = UriFactory.Create(XmlSpecsHelper.XmlSchemaDataTypePositiveInteger);
                        break;
                    case XmlSpecsHelper.XmlSchemaDataTypeShort:
                        nodeType = UriFactory.Create(XmlSpecsHelper.XmlSchemaDataTypeShort);
                        break;
                    case XmlSpecsHelper.XmlSchemaDataTypeTime:
                        nodeType = UriFactory.Create(XmlSpecsHelper.XmlSchemaDataTypeTime);
                        break;
                    case XmlSpecsHelper.XmlSchemaDataTypeUnsignedByte:
                        nodeType = UriFactory.Create(XmlSpecsHelper.XmlSchemaDataTypeUnsignedByte);
                        break;
                    case XmlSpecsHelper.XmlSchemaDataTypeUnsignedInt:
                        nodeType = UriFactory.Create(XmlSpecsHelper.XmlSchemaDataTypeUnsignedInt);
                        break;
                    case XmlSpecsHelper.XmlSchemaDataTypeUnsignedLong:
                        nodeType = UriFactory.Create(XmlSpecsHelper.XmlSchemaDataTypeUnsignedLong);
                        break;
                    case XmlSpecsHelper.XmlSchemaDataTypeUnsignedShort:
                        nodeType = UriFactory.Create(XmlSpecsHelper.XmlSchemaDataTypeUnsignedShort);
                        break;
                    case "http://www.w3.org/2001/XMLSchema#gYear":
                        nodeType = UriFactory.Create("http://www.w3.org/2001/XMLSchema#gYear");
                        break;
                    default:
                        nodeType = UriFactory.Create(XmlSpecsHelper.XmlSchemaDataTypeString);
                        break;
                }
                n = g.CreateLiteralNode(node.Split("^^")[0], nodeType);
            }
            else if (node.Contains("@"))
            {
                // str ac lang
                n = g.CreateLiteralNode(node.Split("@")[0], node.Split("@")[1]);
            }
            else
            {
                // on ne s'embete pas : literal sans type et sans lang
                n = g.CreateLiteralNode(node, UriFactory.Create(XmlSpecsHelper.XmlSchemaDataTypeString));
            }
            return n;
        }

        public static async Task<Dictionary<string, Dictionary<string, int>>> GetConceptDistances(SparqlQueryable endpoint, string NamedGraphUri1)
        {
            var namedGraph = !NamedGraphUri1.IsNullOrWhiteSpace() ? $" FROM <{NamedGraphUri1}> " : string.Empty;
            var query = "PREFIX rdfs: <http://www.w3.org/2000/01/rdf-schema#> select ?sub ?super (count(?mid) as ?distance)" + namedGraph + " WHERE { { SELECT DISTINCT ?mid WHERE { ?s rdfs:subClassOf ?mid . } } ?sub rdfs:subClassOf* ?mid . ?mid rdfs:subClassOf+ ?super . } group by ?sub ?super  order by ?sub ?super";
            var sparqlResultSet = await endpoint.SelectQuery(query);
            var result = new Dictionary<string, Dictionary<string, int>>();
            // TODO: vérifier ce résultat
            // _logger.LogInformation($"# of results for concept distances : {sparqlResultSet.Count}");
            foreach (var sparqlResult in sparqlResultSet)
            {
                if (!sparqlResult.Variables.Contains("sub") || !(sparqlResult["sub"] is UriNode))
                {
                    continue;
                }
                else if (!sparqlResult.Variables.Contains("super") || !(sparqlResult["super"] is UriNode))
                {
                    continue;
                }
                else if (!sparqlResult.Variables.Contains("distance"))
                {
                    continue;
                }
                var sub = sparqlResult["sub"] as UriNode;
                var super = sparqlResult["super"] as UriNode;
                var distance = sparqlResult["distance"] as LiteralNode;
                int distanceValue;
                if (!int.TryParse(distance.Value, out distanceValue)) distanceValue = 0;
                var subUri = sub.Uri.ToString();
                var superUri = super.Uri.ToString();
                Dictionary<string, int> dict;
                if (result.Keys.Contains(subUri))
                {
                    dict = result[subUri];
                }
                else
                {
                    dict = new Dictionary<string, int>();
                    result.Add(subUri, dict);
                }
                if (!dict.Keys.Contains(superUri))
                {
                    dict.Add(superUri, distanceValue);
                }
            }
            return result;
        }

        public static IEnumerable<Triple> GetTriples(this SparqlRemoteEndpoint endpoint, UriNode uriNode)
        {
            return endpoint.GetContextualGraph(uriNode.Uri.ToString()).Triples;
        }
        

        public static IEnumerable<Triple> GetTriples(this SparqlRemoteEndpoint endpoint, Uri uri)
        {
            return endpoint.GetContextualGraph(uri.ToString()).Triples;
        }

        public static IEnumerable<Triple> GetContextualGraph(this SparqlRemoteEndpoint endpoint, UriNode uriNode, HashSet<UriNode> processed, int rank)
        {
            if (rank == 0) return new List<Triple>();
            rank--;
            var triples = endpoint.GetTriples(uriNode).ToList();
            var triplesToAdd = new List<Triple>();
            foreach (var triple in triples)
            {
                var subject = triple.Subject.NodeType == NodeType.Uri ? (UriNode)triple.Subject : null;
                if (!processed.Contains(subject))
                {
                    processed.Add(subject);
                    triplesToAdd.AddRange(GetContextualGraph(endpoint, subject, processed, rank));
                }
                var obj = triple.Object.NodeType == NodeType.Uri ? (UriNode)triple.Object : null;
                if (obj != null && !processed.Contains(obj))
                {
                    processed.Add(obj);
                    triplesToAdd.AddRange(GetContextualGraph(endpoint, obj, processed, rank));
                }
            }
            triples.AddRange(triplesToAdd);
            return triples.Distinct();
        }

        public static IEnumerable<Triple> GetContextualGraph(this SparqlRemoteEndpoint endpoint, Uri uri, HashSet<string> processed, int rank)
        {
            if (rank == 0 || processed.Contains(uri.ToString())) return new List<Triple>();
            rank--;
            var triples = endpoint.GetTriples(uri).ToList();
            if (rank > 0)
            {
                var triplesToAdd = new List<Triple>();
                foreach (var triple in triples)
                {
                    var subject = (UriNode)triple.Subject;
                    if (!processed.Contains(subject.Uri.ToString()))
                    {
                        processed.Add(subject.Uri.ToString());
                        triplesToAdd.AddRange(GetContextualGraph(endpoint, subject.Uri, processed, rank));
                    }
                    var obj = triple.Object.NodeType == NodeType.Uri ? (UriNode)triple.Object : null;
                    if (obj != null && !processed.Contains(obj.Uri.ToString()))
                    {
                        processed.Add(obj.Uri.ToString());
                        triplesToAdd.AddRange(GetContextualGraph(endpoint, obj.Uri, processed, rank));
                    }
                }
                triples.AddRange(triplesToAdd);
            }
            // else
            // {
            //     triples.Select(t => processed.Add(t.Subject.ToString())).ToList();
            //     triples.Where(t => t.Object.NodeType == NodeType.Uri).Select(t => processed.Add(t.Object.ToString())).ToList();
            // }
            return triples;
        }
        
      
        
        public static IGraph GetContextualGraphV2(this SparqlRemoteEndpoint endpoint, string uri, HashSet<string> processed, int rank)
        {
            if (rank <= 0) return new Graph();
            rank--;
            var graph = endpoint.GetContextualGraph(uri);
            processed.Add(uri);
            var graphToMerge = new List<IGraph>();
            foreach (var uriString in graph.Triples.GetAllUriNodeToString())
            {
                if (processed.Contains(uriString)) continue;
                var tmpGraph = endpoint.GetContextualGraphV2(uriString, processed, rank);
                if (!tmpGraph.IsEmpty)
                {
                    graphToMerge.Add(tmpGraph);
                }
            }
            foreach (var g in graphToMerge)
            {
                graph.Merge(g);
            }
            return graph;
        }

        public static string RemoveNonAlphaNum(string str)
        {
            var rgx = new System.Text.RegularExpressions.Regex("[^a-zA-Z0-9 -]");
            var result = rgx.Replace(str, " ").Trim();
            return result;
        }
        
        public static IEnumerable<string> GetAllUriNodeToString(this IEnumerable<Triple> triples)
        {
            return triples.SelectMany(t => t.Object.NodeType == NodeType.Uri ? new[] { t.Subject.ToString(), t.Object.ToString() } : new[] { t.Subject.ToString() } ).Distinct();
        }
        public static IEnumerable<Triple> GetContextualGraph(this IGraph graph, UriNode uriNode, HashSet<string> processed, int rank)
        {
            if (rank == 0) return Enumerable.Empty<Triple>();
            processed.Add(uriNode.ToString());
            rank--;
            var triples = graph.GetTriples(uriNode).ToList();
            var triplesToAdd = new List<Triple>();
            foreach (var triple in triples)
            {
                var subject = triple.Subject.NodeType == NodeType.Uri ? (UriNode)triple.Subject : null;
                if (!processed.Contains(subject.ToString()))
                {
                    processed.Add(subject.ToString());
                    triplesToAdd.AddRange(GetContextualGraph(graph, subject, processed, rank));
                }
                var obj = triple.Object.NodeType == NodeType.Uri ? (UriNode)triple.Object : null;
                if (obj != null && !processed.Contains(obj.ToString()))
                {
                    processed.Add(obj.ToString());
                    triplesToAdd.AddRange(GetContextualGraph(graph, obj, processed, rank));
                }
            }
            triples.AddRange(triplesToAdd);
            return triples.Distinct();
        }
        
        
        public static string GetFragment(UriNode node)
        {
            var uri = node.Uri;
            return string.IsNullOrWhiteSpace(uri.Fragment) ? uri.Segments.LastOrDefault() : uri.Fragment;
        }
             

        public static INode ToINode(string node)
        {
            using (var g = new Graph())
            {
                return Tools.ToINode(node, g);
            }
            
        }
    }
}
