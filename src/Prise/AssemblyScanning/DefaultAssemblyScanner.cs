using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;

using Prise.Utils;

namespace Prise.AssemblyScanning
{
    public class DefaultAssemblyScanner : IAssemblyScanner
    {
        protected IList<IDisposable> disposables;
        protected Func<string, IMetadataLoadContext> metadataLoadContextFactory;
        protected IDirectoryTraverser directoryTraverser;

        public DefaultAssemblyScanner(Func<string, IMetadataLoadContext> metadataLoadContextFactory, Func<IDirectoryTraverser> directoryTraverser)
        {
            this.disposables = new List<IDisposable>();
            this.metadataLoadContextFactory = metadataLoadContextFactory.ThrowIfNull(nameof(metadataLoadContextFactory));
            this.directoryTraverser = directoryTraverser.ThrowIfNull(nameof(directoryTraverser))();
        }

        public virtual Task<IEnumerable<AssemblyScanResult>> Scan(IAssemblyScannerOptions options)
        {
            if (options == null)
                throw new ArgumentNullException($"{typeof(IAssemblyScannerOptions).Name} {nameof(options)}");

            var startingPath = options.StartingPath ?? throw new ArgumentException($"{nameof(options.StartingPath)}");
            var typeToScan = options.PluginType ?? throw new ArgumentException($"{nameof(options.PluginType)}");
            var fileTypes = options.FileTypes;

            if (!Path.IsPathRooted(startingPath))
                throw new AssemblyScanningException($"startingPath {startingPath} is not rooted, this must be a absolute path!");

            if (fileTypes == null)
                fileTypes = new List<string> { "*.dll" };

            var results = new List<AssemblyScanResult>();
            foreach (var directory in this.directoryTraverser.TraverseDirectories(startingPath))
            {
                foreach (var assemblyFilePath in this.directoryTraverser.TraverseFiles(directory, fileTypes))
                {
                    foreach (var implementation in GetImplementationsOfTypeFromAssembly(typeToScan, assemblyFilePath))
                        if (implementation != null)
                            results.Add(new AssemblyScanResult
                            {
                                ContractType = typeToScan,
                                AssemblyName = Path.GetFileName(assemblyFilePath),
                                AssemblyPath = Path.GetDirectoryName(assemblyFilePath),
                                PluginType = implementation
                            });
                }
            }

            return Task.FromResult(results.AsEnumerable());
        }

        private IEnumerable<string> ExcludeRuntimesFolder(IEnumerable<string> files) => files.Where(f => !f.Contains($"{Path.DirectorySeparatorChar}runtimes{Path.DirectorySeparatorChar}"));

        private IEnumerable<Type> GetImplementationsOfTypeFromAssembly(Type type, string assemblyFullPath)
        {
            var context = this.metadataLoadContextFactory(assemblyFullPath);
            var assembly = context.LoadFromAssemblyName(Path.GetFileNameWithoutExtension(assemblyFullPath));
            this.disposables.Add(context);

            return assembly.Types
                        .Where(t => t.CustomAttributes
                            .Any(c => c.AttributeType.Name == typeof(Prise.Plugin.PluginAttribute).Name
                            && (c.NamedArguments.First(a => a.MemberName == "PluginType").TypedValue.Value as Type).Name == type.Name
                            && (c.NamedArguments.First(a => a.MemberName == "PluginType").TypedValue.Value as Type).Namespace == type.Namespace))
                        .OrderBy(t => t.Name)
                        .ToList();
        }

        protected bool disposed = false;
        protected virtual void Dispose(bool disposing)
        {
            if (!this.disposed && disposing)
            {
                if (this.disposables != null && this.disposables.Any())
                    foreach (var disposable in this.disposables)
                        disposable.Dispose();
                GC.Collect(); // collects all unused memory
                GC.WaitForPendingFinalizers(); // wait until GC has finished its work
            }
            this.disposed = true;
        }

        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }
    }
}