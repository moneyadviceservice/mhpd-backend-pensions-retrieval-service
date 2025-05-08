namespace PensionsRetrievalFunction.Models;

public static class Constants
{
    internal static class LogSource
    {
        public const string Queue = "Pensions Retrieval Message Queue";
        public const string Http = "Pensions Retrieval Http GET";
    }

    internal static class RetrievalStatus
    {
        internal const string New = "NEW";
        internal const string Requested = "RETRIEVAL_REQUESTED";
        internal const string Complete = "RETRIEVAL_COMPLETE";
        internal const string Failed = "RETRIEVAL_FAILED";
    }

    public static class ResponseType
    {
        public const string InvalidSessionId = "userSessionId missing or invalid";
        public const string InvalidCorrelationId = "mhpdCorrelationId invalid";
        public const string InvalidPayloadResponse = "Invalid pension retrieval payload";
    }
}
