namespace Mostlylucid.VoiceForm.Config;

public static class ConfigExtensions
{
    public static TConfig ConfigurePOCO<TConfig>(this IServiceCollection services, IConfigurationSection configuration)
        where TConfig : class, new()
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(configuration);

        var config = new TConfig();
        configuration.Bind(config);
        services.AddSingleton(config);
        return config;
    }

    public static TConfig ConfigurePOCO<TConfig>(this IServiceCollection services, ConfigurationManager configuration)
        where TConfig : class, IConfigSection, new()
    {
        var sectionName = TConfig.Section;
        var section = configuration.GetSection(sectionName);
        return services.ConfigurePOCO<TConfig>(section);
    }
}
