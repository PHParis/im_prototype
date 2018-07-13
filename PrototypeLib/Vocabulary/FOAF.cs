using System;
using VDS.RDF;

namespace PrototypeLib.Vocabulary
{
    public class FOAF
    {
        public static readonly string NameSpace = "http://xmlns.com/foaf/0.1/";
        public static readonly Uri name = UriFactory.Create(NameSpace + "name");
    }
}