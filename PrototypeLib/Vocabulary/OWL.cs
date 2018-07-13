using System;
using VDS.RDF;

namespace PrototypeLib.Vocabulary
{
    public class OWL
    {
        public static readonly string NameSpace = "http://www.w3.org/2002/07/owl#";
        public static readonly Uri equivalentProperty = UriFactory.Create(NameSpace + "equivalentProperty");
        public static readonly Uri sameAs = UriFactory.Create(NameSpace + "sameAs");
        public static readonly Uri AllDifferent = UriFactory.Create(NameSpace + "AllDifferent");
        public static readonly Uri members = UriFactory.Create(NameSpace + "members");
        public static readonly Uri distinctMembers = UriFactory.Create(NameSpace + "distinctMembers ");
        public static readonly Uri FunctionalProperty = UriFactory.Create(NameSpace + "FunctionalProperty");
        public static readonly Uri InverseFunctionalProperty = UriFactory.Create(NameSpace + "InverseFunctionalProperty");
        public static readonly Uri IrreflexiveProperty = UriFactory.Create(NameSpace + "IrreflexiveProperty");
        public static readonly Uri SymmetricProperty = UriFactory.Create(NameSpace + "SymmetricProperty");
        public static readonly Uri AsymmetricProperty = UriFactory.Create(NameSpace + "AsymmetricProperty");
        public static readonly Uri TransitiveProperty = UriFactory.Create(NameSpace + "TransitiveProperty");
        public static readonly Uri propertyChainAxiom = UriFactory.Create(NameSpace + "propertyChainAxiom");
        public static readonly Uri propertyDisjointWith = UriFactory.Create(NameSpace + "propertyDisjointWith");
        public static readonly Uri AllDisjointProperties = UriFactory.Create(NameSpace + "AllDisjointProperties");
        public static readonly Uri inverseOf = UriFactory.Create(NameSpace + "inverseOf");
        public static readonly Uri hasKey = UriFactory.Create(NameSpace + "hasKey");
        public static readonly Uri sourceIndividual = UriFactory.Create(NameSpace + "sourceIndividual");
        public static readonly Uri assertionProperty = UriFactory.Create(NameSpace + "assertionProperty");
        public static readonly Uri targetIndividual = UriFactory.Create(NameSpace + "targetIndividual");
        public static readonly Uri intersectionOf = UriFactory.Create(NameSpace + "intersectionOf");
        public static readonly Uri Thing = UriFactory.Create(NameSpace + "Thing");
        public static readonly Uri Class = UriFactory.Create(NameSpace + "Class");
        public static readonly Uri Nothing = UriFactory.Create(NameSpace + "Nothing");
        public static readonly Uri AnnotationProperty = UriFactory.Create(NameSpace + "AnnotationProperty");
        public static readonly Uri unionOf = UriFactory.Create(NameSpace + "unionOf");
        public static readonly Uri complementOf = UriFactory.Create(NameSpace + "complementOf");
        public static readonly Uri someValuesFrom = UriFactory.Create(NameSpace + "someValuesFrom");
        public static readonly Uri onProperty = UriFactory.Create(NameSpace + "onProperty");
        public static readonly Uri allValuesFrom = UriFactory.Create(NameSpace + "allValuesFrom");
        public static readonly Uri hasValue = UriFactory.Create(NameSpace + "hasValue");
        public static readonly Uri maxCardinality = UriFactory.Create(NameSpace + "maxCardinality");
        public static readonly Uri maxQualifiedCardinality = UriFactory.Create(NameSpace + "maxQualifiedCardinality");
        public static readonly Uri oneOf = UriFactory.Create(NameSpace + "oneOf");
        public static readonly Uri equivalentClass = UriFactory.Create(NameSpace + "equivalentClass");
        public static readonly Uri disjointWith = UriFactory.Create(NameSpace + "disjointWith");
        public static readonly Uri AllDisjointClasses = UriFactory.Create(NameSpace + "AllDisjointClasses");
        public static readonly Uri ObjectProperty = UriFactory.Create(NameSpace + "ObjectProperty");
        public static readonly Uri DatatypeProperty = UriFactory.Create(NameSpace + "DatatypeProperty");
        public static readonly Uri onClass = UriFactory.Create(NameSpace + "onClass");
        public static readonly Uri differentFrom = UriFactory.Create(NameSpace + "differentFrom");
        public static readonly Uri NamedIndividual = UriFactory.Create(NameSpace + "NamedIndividual");
        public static readonly Uri Annotation = UriFactory.Create(NameSpace + "Annotation");
        public static readonly Uri Axiom = UriFactory.Create(NameSpace + "Axiom");
        public static readonly Uri DataRange = UriFactory.Create(NameSpace + "DataRange");
        public static readonly Uri DeprecatedClass = UriFactory.Create(NameSpace + "DeprecatedClass");
        public static readonly Uri DeprecatedProperty = UriFactory.Create(NameSpace + "DeprecatedProperty");
        public static readonly Uri NegativePropertyAssertion = UriFactory.Create(NameSpace + "NegativePropertyAssertion");
        public static readonly Uri Ontology = UriFactory.Create(NameSpace + "Ontology");
        public static readonly Uri OntologyProperty = UriFactory.Create(NameSpace + "OntologyProperty");
        public static readonly Uri ReflexiveProperty = UriFactory.Create(NameSpace + "ReflexiveProperty");
        public static readonly Uri Restriction = UriFactory.Create(NameSpace + "Restriction");
        // TODO: il manque des propriétés
    }
}