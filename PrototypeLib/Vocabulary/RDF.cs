using System;
using VDS.RDF;

namespace PrototypeLib.Vocabulary
{
    public class RDF
    {
        public static readonly string NameSpace = "http://www.w3.org/1999/02/22-rdf-syntax-ns#";
        public static readonly Uri type = UriFactory.Create(NameSpace + "type");
        public static readonly Uri Property = UriFactory.Create(NameSpace + "Property");
        public static readonly Uri Statement = UriFactory.Create(NameSpace + "Statement");
        public static readonly Uri Bag = UriFactory.Create(NameSpace + "Bag");
        public static readonly Uri Seq = UriFactory.Create(NameSpace + "Seq");
        public static readonly Uri Alt = UriFactory.Create(NameSpace + "Alt");
        public static readonly Uri List = UriFactory.Create(NameSpace + "List");
    }
}