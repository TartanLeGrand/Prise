using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;
using Prise.Activation;
using Prise.AssemblyLoading;
using Prise.AssemblyScanning;

using Prise.Proxy;
using Prise.Utils;

namespace Prise
{
    public class DefaultPluginLoader : IPluginLoader
    {
        private readonly IAssemblyScanner assemblyScanner;
        private readonly IPluginTypeSelector pluginTypeSelector;
        private readonly IAssemblyLoader assemblyLoader;
        private readonly IParameterConverter parameterConverter;
        private readonly IResultConverter resultConverter;
        private readonly IPluginActivator pluginActivator;
        public DefaultPluginLoader(
                            IAssemblyScanner assemblyScanner,
                            IPluginTypeSelector pluginTypeSelector,
                            IAssemblyLoader assemblyLoader,
                            IParameterConverter parameterConverter,
                            IResultConverter resultConverter,
                            IPluginActivator pluginActivator)
        {
            this.assemblyScanner = assemblyScanner;
            this.pluginTypeSelector = pluginTypeSelector;
            this.assemblyLoader = assemblyLoader;
            this.parameterConverter = parameterConverter;
            this.resultConverter = resultConverter;
            this.pluginActivator = pluginActivator;
        }

        public async Task<AssemblyScanResult> FindPlugin<T>(string pathToPlugins, string plugin)
        {
            return
                (await this.FindPlugins<T>(pathToPlugins))
                .FirstOrDefault(p => p.AssemblyPath.Split(Path.DirectorySeparatorChar).Last().Equals(plugin));
        }

        public Task<IEnumerable<AssemblyScanResult>> FindPlugins<T>(string pathToPlugins)
        {
            return this.assemblyScanner.Scan(new AssemblyScannerOptions
            {
                StartingPath = pathToPlugins,
                PluginType = typeof(T)
            });
        }

        public async Task<T> LoadPlugin<T>(AssemblyScanResult scanResult, string hostFramework = null, Action<PluginLoadContext> configureLoadContext = null)
        {
            hostFramework = hostFramework.ValueOrDefault(HostFrameworkUtils.GetHostframeworkFromHost());
            var servicesForPlugin = new ServiceCollection();

            var pathToAssembly = Path.Combine(scanResult.AssemblyPath, scanResult.AssemblyName);
            var pluginLoadContext = PluginLoadContext.DefaultPluginLoadContext(pathToAssembly, typeof(T), hostFramework);
            // This allows the loading of netstandard plugins
            pluginLoadContext.IgnorePlatformInconsistencies = true;

            configureLoadContext?.Invoke(pluginLoadContext);

            var pluginAssembly = await this.assemblyLoader.Load(pluginLoadContext);
            var pluginTypes = this.pluginTypeSelector.SelectPluginTypes<T>(pluginAssembly);
            var pluginType = pluginTypes.FirstOrDefault(p => p.Name.Equals(scanResult.PluginType.Name));
            if (pluginType == null)
                throw new PluginLoadException($"Did not found any plugins to load from {nameof(AssemblyScanResult)} {scanResult.AssemblyPath} {scanResult.AssemblyName}");

            return await this.pluginActivator.ActivatePlugin<T>(new DefaultPluginActivationOptions
            {
                PluginType = pluginType,
                PluginAssembly = pluginAssembly,
                ParameterConverter = this.parameterConverter,
                ResultConverter = this.resultConverter,
                HostServices = pluginLoadContext.HostServices
            });
        }

        public async Task<IEnumerable<T>> LoadPlugins<T>(AssemblyScanResult scanResult, string hostFramework = null, Action<PluginLoadContext> configureLoadContext = null)
        {
            var plugins = new List<T>();

            await foreach (var plugin in this.LoadPluginsAsAsyncEnumerable<T>(scanResult, hostFramework, configureLoadContext))
                plugins.Add(plugin);

            return plugins;
        }

        public async IAsyncEnumerable<T> LoadPluginsAsAsyncEnumerable<T>(AssemblyScanResult scanResult, string hostFramework = null, Action<PluginLoadContext> configureLoadContext = null)
        {
            hostFramework = hostFramework.ValueOrDefault(HostFrameworkUtils.GetHostframeworkFromHost());
            var pathToAssembly = Path.Combine(scanResult.AssemblyPath, scanResult.AssemblyName);
            var pluginLoadContext = PluginLoadContext.DefaultPluginLoadContext(pathToAssembly, typeof(T), hostFramework);
            // This allows the loading of netstandard plugins
            pluginLoadContext.IgnorePlatformInconsistencies = true;

            configureLoadContext?.Invoke(pluginLoadContext);

            var pluginAssembly = await this.assemblyLoader.Load(pluginLoadContext);
            var pluginTypes = this.pluginTypeSelector.SelectPluginTypes<T>(pluginAssembly);

            foreach (var pluginType in pluginTypes)
                yield return await this.pluginActivator.ActivatePlugin<T>(new DefaultPluginActivationOptions
                {
                    PluginType = pluginType,
                    PluginAssembly = pluginAssembly,
                    ParameterConverter = this.parameterConverter,
                    ResultConverter = this.resultConverter,
                    HostServices = pluginLoadContext.HostServices
                });
        }
    }
}