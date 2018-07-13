using System.Collections.Generic;

namespace PrototypeLib
{
    public class SimilarityResult
    {
        public double Result { get; set; }
        public string Uri1 { get; set; }
        public string Uri2 { get; set; }
        public bool IsSameAs { get; set; }
    }
}