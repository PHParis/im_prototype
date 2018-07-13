using System;
using VDS.RDF;

namespace PrototypeLib.Vocabulary
{
    public class RDFS
    {
        public static readonly string NameSpace = "http://www.w3.org/2000/01/rdf-schema#";
        public static readonly Uri subPropertyOf = UriFactory.Create(NameSpace + "subPropertyOf");
        public static readonly Uri label = UriFactory.Create(NameSpace + "label");
        public static readonly Uri domain = UriFactory.Create(NameSpace + "domain");
        public static readonly Uri range = UriFactory.Create(NameSpace + "range");
        public static readonly Uri Resource = UriFactory.Create(NameSpace + "Resource");
        public static readonly Uri Class = UriFactory.Create(NameSpace + "Class");
        public static readonly Uri isDefinedBy = UriFactory.Create(NameSpace + "isDefinedBy");
        public static readonly Uri comment = UriFactory.Create(NameSpace + "comment");
        public static readonly Uri subClassOf = UriFactory.Create(NameSpace + "subClassOf");
        public static readonly Uri seeAlso = UriFactory.Create(NameSpace + "seeAlso");
        public static readonly Uri Container = UriFactory.Create(NameSpace + "Container");
        public static readonly Uri ContainerMembershipProperty = UriFactory.Create(NameSpace + "ContainerMembershipProperty");
        public static readonly Uri member = UriFactory.Create(NameSpace + "member");
        public static readonly Uri Datatype = UriFactory.Create(NameSpace + "Datatype");
        public static readonly Uri Literal = UriFactory.Create(NameSpace + "Literal");
    }
}