namespace PrototypeLib
{
    /// <summary>
    /// Implementation of an RDF triple.
    /// </summary>
    public class TripleImpl
    {
        public string s { get; set; }
        public string p { get; set; }
        public string o { get; set; }

        public override int GetHashCode()
        {
            return this.s.GetHashCode() ^ this.p.GetHashCode() ^ this.o.GetHashCode();
        }
        public override bool Equals(object other)
        {
            if (other == null)
                return false;
            if (!(other is TripleImpl))
                return false;
            var b2 = other as TripleImpl;
            return this.s == b2.s && this.p == b2.p && this.o == b2.o;
        }
        public override string ToString()
        {
            return $"<{s}, {p}, {o}>";
        }
    }
}