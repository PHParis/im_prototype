using System;
using System.Collections.Generic;
using System.Linq;
using VDS.RDF;
using VDS.RDF.Query;
using System.Threading.Tasks;

namespace PrototypeLib
{
    /// <summary>
    /// Class to handle querying on SPARQL endpoint or dotnetRDF.Graph instances in a uniform manner.
    /// </summary>
    public class SparqlQueryable
    {
        private readonly SparqlRemoteEndpoint endpoint;

        private readonly string namedGraphUri;
        private readonly IGraph graph;

        public SparqlQueryable(SparqlRemoteEndpoint endpoint, string namedGraphUri)
        {
            this.endpoint = endpoint;
            this.graph = null;
            this.namedGraphUri = namedGraphUri;
        }

        public SparqlQueryable(IGraph graph)
        {
            this.graph = graph;
            this.endpoint = null;
        }  

        public SparqlResultSet Select(string selectQuery)
        {
            return graph != null ? graph.Select(selectQuery) : endpoint.Select(selectQuery);
        }

        public bool IsEndpoint()
        {
            return endpoint != null;
        }

        public Uri GetUri()
        {
            return endpoint.Uri;
        }

        public IGraph GetGraph()
        {
            return graph != null ? graph : new Graph();
        }

        public string GetNamedGraphUri()
        {
            return namedGraphUri;
        }

        public static SparqlQueryable Create(string fileOrUri, string namedGraphUri)
        {
            if (System.IO.File.Exists(fileOrUri))
            {
                var graph = new NonIndexedGraph();

                graph.LoadFromFile(fileOrUri);
                return new SparqlQueryable(graph);
            }
            var endpoint = new SparqlRemoteEndpoint(UriFactory.Create(fileOrUri));
            return new SparqlQueryable(endpoint, namedGraphUri);
        }


        public override int GetHashCode()
        {
            return this.IsEndpoint().GetHashCode() ^ (this.IsEndpoint() ? this.endpoint.Uri.GetHashCode() : this.graph.GetHashCode());
        }
        public override bool Equals(object other)
        {
            if (other == null)
                return false;
            if (!(other is SparqlQueryable))
                return false;
            var b2 = other as SparqlQueryable;
            if (this.IsEndpoint() != b2.IsEndpoint())
                return false;
            if (this.IsEndpoint())
                return this.endpoint.Uri.AbsoluteUri.Equals(b2.endpoint.Uri.AbsoluteUri) && 
                    (this.namedGraphUri == null ? (b2.GetNamedGraphUri() == null) : this.namedGraphUri.Equals(b2.GetNamedGraphUri()));
            if (this.graph.Triples.Count != b2.graph.Triples.Count)
                return false;
            return this.graph.Triples
                .All(t1 => b2.graph.Triples.Any(t2 => t1.Subject.ToString().Equals(t2.Subject.ToString())
                && t1.Predicate.ToString().Equals(t2.Predicate.ToString())
                && t1.Object.ToString().Equals(t2.Object.ToString())));
        }
    }
}