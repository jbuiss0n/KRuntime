// Copyright (c) Microsoft Open Technologies, Inc. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using DependencyAnalyzer.Util;
using Microsoft.Framework.Runtime;

namespace DependencyAnalyzer.Commands
{
    /// <summary>
    /// Command to build the minimal TPA list
    /// </summary>
    public class BuildTpaCommand
    {
        private readonly IApplicationEnvironment _environment;
        private readonly string _assemblyFolder;
        private readonly string _outputFile;

        public BuildTpaCommand(IApplicationEnvironment environment, string assemblyFolder, string outputFile)
        {
            _environment = environment;
            _assemblyFolder = assemblyFolder;
            _outputFile = outputFile;
        }

        /// <summary>
        /// Execute the command 
        /// </summary>
        /// <returns>Returns 0 for success, otherwise 1.</returns>
        public int Execute()
        {
            var accessor = new CacheContextAccessor();
            var cache = new Cache(accessor);

            var finder = new DependencyFinder(accessor, cache, _environment, _assemblyFolder);

            var tpa = finder.GetContractDependencies("klr.core45.managed");

            using (var output = SafeTextWriter.CreateOutput(_outputFile))
            {
                foreach (var assembly in tpa)
                {
                    output.Writer.WriteLine(assembly);
                }
            }

            return 0;
        }
    }
}