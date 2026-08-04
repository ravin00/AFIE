namespace AFIE.FeatureEngineering.Models;

public class EventHubOptions
{
    public string FullyQualifiedNamespace { get; set; } = "";
    public string EventHubName { get; set; } = "";

    public string ConsumerGroup { get; set; } = "";

    public string BlobStorageUrl { get; set; } = "";
    public string BlobContainerName { get; set; } = "";
}