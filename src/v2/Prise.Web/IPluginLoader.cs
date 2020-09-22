using System.IO;
using System.Threading.Tasks;
using System.Collections.Generic;
using Prise.DependencyInjection;

public interface IPluginLoader
{
    Task<IEnumerable<Prise.Core.AssemblyScanResult>> FindPlugins<T>(string pathToPlugins);
    IAsyncEnumerable<T> LoadPlugins<T>(Prise.Core.AssemblyScanResult plugin);
}

public class PluginLoader : IPluginLoader
{
    private readonly Prise.AssemblyScanning.IAssemblyScanner assemblyScanner;
    private readonly Prise.AssemblyLoading.IAssemblyLoader assemblyLoader;
    private readonly Prise.Activation.IPluginActivator pluginActivator;

    public PluginLoader(Prise.AssemblyScanning.IAssemblyScanner assemblyScanner,
                    Prise.AssemblyLoading.IAssemblyLoader assemblyLoader,
                    Prise.Activation.IPluginActivator pluginActivator)
    {
        this.assemblyScanner = assemblyScanner;
        this.assemblyLoader = assemblyLoader;
        this.pluginActivator = pluginActivator;
    }

    public async Task<IEnumerable<Prise.Core.AssemblyScanResult>> FindPlugins<T>(string pathToPlugins)
    {
        return (await this.assemblyScanner.Scan(new Prise.AssemblyScanning.AssemblyScannerOptions
        {
            StartingPath = pathToPlugins,
            PluginType = typeof(T)
        }));
    }

    public async IAsyncEnumerable<T> LoadPlugins<T>(Prise.Core.AssemblyScanResult plugin)
    {
        var hostFramework = Prise.Utils.HostFrameworkUtils.GetHostframeworkFromHost();
        var servicesForPlugin = new Microsoft.Extensions.DependencyInjection.ServiceCollection();

        var pathToAssembly = Path.Combine(plugin.AssemblyPath, plugin.AssemblyName);
        var pluginLoadContext = Prise.Core.PluginLoadContext.DefaultPluginLoadContext(pathToAssembly, typeof(T), hostFramework);
        // This allows the loading of netstandard plugins
        pluginLoadContext.IgnorePlatformInconsistencies = true;

        var pluginAssembly = await this.assemblyLoader.Load(pluginLoadContext);
        var pluginTypeSelector = new Prise.Core.DefaultPluginTypeSelector();

        var pluginTypes = pluginTypeSelector.SelectPluginTypes<T>(pluginAssembly);

        foreach(var pluginType in pluginTypes)
            yield return await this.pluginActivator.ActivatePlugin<T>(new Prise.Activation.DefaultPluginActivationOptions
            {
                PluginType = pluginType,
                PluginAssembly = pluginAssembly,
                ParameterConverter = DefaultFactories.DefaultParameterConverter(),
                ResultConverter = DefaultFactories.DefaultResultConverter(),
                HostServices = servicesForPlugin
            });
    }
}