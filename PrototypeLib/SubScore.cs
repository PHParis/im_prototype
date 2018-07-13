using System.Collections.Generic;
using System.Linq;

namespace PrototypeLib
{
    public class SubScore
    {
        /// <summary>
        /// s1 (source uri)
        /// </summary>
        /// <returns></returns>
        public string s { get; set; }
        /// <summary>
        /// s2 (target uri)
        /// </summary>
        /// <returns></returns>
        public string t { get; set; }
        public double w { get; set; }
        /// <summary>
        /// scores (liste des scores)
        /// </summary>
        /// <returns></returns>
        public List<SubSubScore> n { get; set; }
        public int nbrCommonPredicates { get; set; }
        public int nbrDifferentPredicatesSource { get; set; }
        public int nbrDifferentPredicatesTarget { get; set; }

        public override int GetHashCode()
        {
            return (this.s + this.t).GetHashCode();
        }

        public override bool Equals(object obj)
        {
            if (obj == null)
                return false;
            if (!(obj is SubScore))
                return false;
            var other = obj as SubScore;
            return (this.s == other.s && this.t == other.t);
        }

        public override string ToString()
        {
            return $"s:{s} // t:{t} // w:{w} //  commonPred:{nbrCommonPredicates} //  difSource:{nbrDifferentPredicatesSource} //  difTarg:{nbrDifferentPredicatesTarget} // \n" + string.Join("\n", n.Select(x => $"{x.p}:\t{x.m}\t{x.c}\t{x.v}\t{x.o}"));
        }
    }
}