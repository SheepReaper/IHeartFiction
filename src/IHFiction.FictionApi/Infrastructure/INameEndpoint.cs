namespace IHFiction.FictionApi.Infrastructure;

internal interface INameEndpoint<TSelf> where TSelf : INameEndpoint<TSelf>
{
    // static contract
    [System.Diagnostics.CodeAnalysis.SuppressMessage("Major Code Smell", "S2743:Static fields should not be used in generic types",
        Justification = "We intentionally want deriving classes to have unique values")]
    static abstract string EndpointName { get; }
}
