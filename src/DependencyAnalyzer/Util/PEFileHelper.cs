// Copyright (c) Microsoft Open Technologies, Inc. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System.Collections.Generic;
using System.IO;
using System.Reflection.Metadata;
using System.Reflection.PortableExecutable;

namespace DependencyAnalyzer.Util
{
    public class PEFileHelper
	{
        public static IList<string> GetReferences(string path)
        {
            var references = new List<string>();

            using (var stream = File.OpenRead(path))
            {
                var peReader = new PEReader(stream);
                var metadataReader = peReader.GetMetadataReader();

                foreach (var a in metadataReader.AssemblyReferences)
                {
                    var reference = metadataReader.GetAssemblyReference(a);
                    var referenceName = metadataReader.GetString(reference.Name);

                    references.Add(referenceName);
                }

                return references;
            }
        }
	}
}