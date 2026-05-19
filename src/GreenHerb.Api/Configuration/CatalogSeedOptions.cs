namespace GreenHerb.Api.Configuration;

public sealed class CatalogSeedOptions
{
    public const string SectionName = "CatalogSeed";

    public bool? OnStartup { get; init; }
}
