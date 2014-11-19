// Copyright (c) Microsoft Open Technologies, Inc. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System.Collections.Generic;
using System.IO;
using System.Linq;
using Microsoft.Framework.Runtime;
using NuGet;

namespace DependencyAnalyzer.Util
{
    public class DependencyFinder
    {
        private readonly ICache                  _cache;
        private readonly ICacheContextAccessor   _accessor;
        private readonly IApplicationEnvironment _environment;
        private readonly string                  _assemblyFolder;

        public DependencyFinder(ICacheContextAccessor cacheContextAccessor, ICache cache, IApplicationEnvironment environment, string assemblyFolder)
        {
            _accessor = cacheContextAccessor;
            _cache = cache;
            _environment = environment;
            _assemblyFolder = assemblyFolder;
        }

        public HashSet<string> GetContractDependencies(string assemblyName)
        {
            var used = new HashSet<string>();

            var dir = Path.GetDirectoryName(_environment.ApplicationBasePath);

            var path = Path.Combine(dir, assemblyName);

            // TODO: hardcoded?
            var framework = VersionUtility.ParseFrameworkName("aspnetcore50");

            var hostContext = new ApplicationHostContext(
                                serviceProvider: null,
                                projectDirectory: path,
                                packagesDirectory: null,
                                configuration: "Debug",
                                targetFramework: framework,
                                cache: _cache,
                                cacheContextAccessor: _accessor,
                                namedCacheDependencyProvider: new NamedCacheDependencyProvider());

            hostContext.DependencyWalker.Walk(hostContext.Project.Name, hostContext.Project.Version, framework);

            var manager = (ILibraryManager)hostContext.ServiceProvider.GetService(typeof(ILibraryManager));

            foreach (var library in manager.GetLibraries())
            {
                foreach (var loadableAssembly in library.LoadableAssemblies)
                {
                    used.Add(loadableAssembly.Name);

                    PackageAssembly assembly;
                    if (hostContext.NuGetDependencyProvider.PackageAssemblyLookup.TryGetValue(loadableAssembly.Name, out assembly))
                    {
                        used.AddRange(WalkAll(assembly.Path));
                    }
                }
            }

            return used;
        }

        private IList<string> WalkAll(string rootPath)
        {
            var result = new HashSet<string>();
            var stack = new Stack<string>();

            stack.Push(rootPath);
            while (stack.Count > 0)
            {
                var path = stack.Pop();

                if (!result.Add(Path.GetFileNameWithoutExtension(path)))
                {
                    continue;
                }

                foreach (var reference in PEFileHelper.GetReferences(path))
                {
                    var newPath = Path.Combine(_assemblyFolder, reference + ".dll");

                    if (!File.Exists(newPath))
                    {
                        continue;
                    }

                    stack.Push(newPath);
                }
            }

            return result.ToList();
        }
    }
}