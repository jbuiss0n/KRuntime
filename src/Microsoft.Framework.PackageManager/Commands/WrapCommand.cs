// Copyright (c) Microsoft Open Technologies, Inc. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Runtime.Versioning;
using System.Xml.Linq;
using Microsoft.Framework.Runtime;
using Newtonsoft.Json.Linq;
using NuGet;

namespace Microsoft.Framework.PackageManager
{
    public class WrapCommand
    {
        private string _referenceResolverPath;
        private static readonly string ReferenceResolverFileName = "ReferenceResolver.xml";
        private static readonly string WrapperProjectVersion = "1.0.0-*";
        private static readonly char PathSeparator = '/';

        public string CsProjectPath { get; set; }
        public string Configuration { get; set; }
        public Reports Reports { get; set; }

        public bool ExecuteCommand()
        {
            // If a folder is given, use a .csproj file in it
            if (Directory.Exists(CsProjectPath))
            {
                CsProjectPath = Directory.EnumerateFiles(CsProjectPath, "*.csproj").FirstOrDefault();
            }

            if (!File.Exists(CsProjectPath))
            {
                Reports.Error.WriteLine("'{0}' doesn't exist".Red(), CsProjectPath);
                return false;
            }

            if (string.IsNullOrEmpty(Configuration))
            {
                Configuration = "debug";
            }

            CsProjectPath = Path.GetFullPath(CsProjectPath);

            var rootDir = ProjectResolver.ResolveRootDirectory(Path.GetDirectoryName(CsProjectPath));
            var wrapRoot = Path.Combine(rootDir, "wrap");

            var xDoc = ResolveReferences();
            if (xDoc == null)
            {
                return false;
            }

            foreach (var projectElement in xDoc.Root.Elements())
            {
                EmitProjectWrapper(projectElement, wrapRoot);
            }

            UpdateGlobalJson(rootDir);

            return true;
        }

        private XDocument ResolveReferences()
        {
            var msbuildExe = GetMSBuildExe();
            if (string.IsNullOrEmpty(msbuildExe))
            {
                return null;
            }

            // Put ReferenceResolver.xml into a temporary dir
            var tempDir = Path.Combine(Path.GetTempPath(), Path.GetRandomFileName());
            Directory.CreateDirectory(tempDir);
            _referenceResolverPath = Path.Combine(tempDir, ReferenceResolverFileName);
#if ASPNET50
            var assembly = GetType().Assembly;
#else
            var assembly = typeof(WrapCommand).GetTypeInfo().Assembly;
#endif
            var embeddedResourceName = "compiler/resources/" + ReferenceResolverFileName;
            using (var stream = assembly.GetManifestResourceStream(embeddedResourceName))
            {
                File.WriteAllText(_referenceResolverPath, stream.ReadToEnd());
            }

            var xDoc = new XDocument();
            xDoc.Add(new XElement("root"));
            var projectFiles = new List<string> { CsProjectPath };
            var intermediateResultFile = Path.Combine(tempDir, Path.GetRandomFileName());

            for (var i = 0; i != projectFiles.Count; i++)
            {
                var properties = new[]
                {
                    "/p:ProjectFilePath=\"\{projectFiles[i]}\"",
                    "/p:ResultFilePath=\"\{intermediateResultFile}\"",
                    "/p:Configuration=\{Configuration}",
                    "/p:DesignTimeBuild=true",
                    "/p:BuildProjectReferences=false",
                    "/p:_ResolveReferenceDependencies=true" // Dump entire assembly reference closure
                };

                var startInfo = new ProcessStartInfo()
                {
                    WorkingDirectory = Path.GetDirectoryName(projectFiles[i]),
                    UseShellExecute = false,
                    FileName = msbuildExe,
                    Arguments = string.Format("\"{0}\" {1}",
                        _referenceResolverPath, string.Join(" ", properties)),
                    RedirectStandardOutput = true
                };

                var process = Process.Start(startInfo);
                var stdOut = process.StandardOutput.ReadToEnd();
                process.WaitForExit();

                var projectXDoc = XDocument.Parse(File.ReadAllText(intermediateResultFile));
                projectXDoc.Root.SetAttributeValue("buildResult", process.ExitCode == 0);

                foreach (var item in GetItemsByType(projectXDoc.Root, type: "ProjectReference"))
                {
                    var relativePath = item.Attribute("evaluated").Value;
                    var fullPath = PathUtility.GetAbsolutePath(projectFiles[i], relativePath);
                    if (!projectFiles.Contains(fullPath))
                    {
                        projectFiles.Add(fullPath);
                    }
                }

                xDoc.Root.Add(projectXDoc.Root);
            }

            FileOperationUtils.DeleteFolder(tempDir);

            return xDoc;
        }

        private string GetMSBuildExe()
        {
#if ASPNET50
            var programFilesPath = Environment.GetFolderPath(Environment.SpecialFolder.ProgramFilesX86);
#else
            var programFilesPath = Environment.GetEnvironmentVariable("PROGRAMFILES(X86)");
#endif
            // On 32-bit Windows
            if (string.IsNullOrEmpty(programFilesPath))
            {
#if ASPNET50
                programFilesPath = Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles);
#else
                programFilesPath = Environment.GetEnvironmentVariable("PROGRAMFILES");
#endif
            }

            var msbuildExe = Path.Combine(programFilesPath, "MSBuild", "14.0", "Bin", "MSBuild.exe");
            if (!File.Exists(msbuildExe))
            {
                Reports.Error.WriteLine(string.Format("Unable to locate {0}".Red().Bold(), msbuildExe));
                return string.Empty;
            }
            return msbuildExe;
        }

        private static void UpdateGlobalJson(string rootDir)
        {
            var globalJsonPath = Path.Combine(rootDir, GlobalSettings.GlobalFileName);
            var rootObj = LoadOrCreateJson(globalJsonPath);

            if (rootObj["sources"] == null)
            {
                rootObj["sources"] = new JArray();
            }

            var sourcesArray = rootObj["sources"] as JArray;
            if (sourcesArray == null)
            {
                throw new InvalidDataException(
                    string.Format("The value of 'sources' in {0} must be an array", globalJsonPath));
            }

            if (!sourcesArray.Any(x => string.Equals(x.Value<string>(), "wrap", StringComparison.OrdinalIgnoreCase)))
            {
                sourcesArray.Add("wrap");
                File.WriteAllText(globalJsonPath, rootObj.ToString());
            }
        }

        private void EmitProjectWrapper(XElement projectElement, string wrapRoot)
        {
            var projectFile = Path.GetFullPath(projectElement.Attribute("projectFile").Value);
            if (!string.Equals("true", projectElement.Attribute("buildResult").Value))
            {
                Reports.Error.WriteLine("Failed to resolve references for '{0}'".Red(), projectFile);
                return;
            }

            var outputAssemblyPath = GetOutputAssemblyPath(projectElement);

            // Name of the wrapper project is output assembly name, instead of .csproj file name
            var projectName = Path.GetFileNameWithoutExtension(outputAssemblyPath);
            var targetProjectJson = Path.Combine(wrapRoot, projectName, Runtime.Project.ProjectFileName);
            var targetFramework = GetTargetFramework(projectElement);

            Reports.Information.WriteLine("Wrapping project '{0}' for '{1}'", projectName, targetFramework);
            Reports.Information.WriteLine("  Source {0}", projectFile.Bold());
            Reports.Information.WriteLine("  Target {0}", targetProjectJson.Bold());

            var projectJson = LoadOrCreateProjectJson(targetProjectJson);

            var relativeCsProjectPath = PathUtility.GetRelativePath(targetProjectJson, projectFile, PathSeparator);
            AddWrappedProjectPath(projectJson, relativeCsProjectPath, targetFramework);

            // Add 'assembly' and 'pdb' to 'bin' section of the target framework
            var relativeAssemblyPath = PathUtility.GetRelativePath(targetProjectJson, outputAssemblyPath, PathSeparator);

            Reports.Information.WriteLine("  Adding bin paths for '{0}'", targetFramework);
            Reports.Information.WriteLine("    Assembly: {0}", relativeAssemblyPath.Bold());
            Reports.Information.WriteLine("    Pdb: {0}", Path.ChangeExtension(relativeAssemblyPath, ".pdb").Bold());
            AddFrameworkBinPaths(projectJson, relativeAssemblyPath, targetFramework, addPdbPath: true);

            var projectDir = Path.GetDirectoryName(projectFile);
            var nugetPackages = ResolveNuGetPackages(projectDir);
            var nugetPackagePaths = nugetPackages.Select(x => x.Path);

            // Add nuget dependency to 'dependencies' section of the target framework
            foreach(var package in nugetPackages)
            {
                Reports.Information.WriteLine("  Adding package dependency '{0}.{1}'",
                    package.Identity, package.Version);
                AddNuGetDependency(projectJson, package, targetFramework);
            }

            // Add dependency projects to 'dependencies' section of the target framework
            foreach(var itemElement in GetItemsByType(projectElement, type: "ProjectReference"))
            {
                var relativePath = itemElement.Attribute("evaluated").Value;
                var referenceProjectFile = PathUtility.GetAbsolutePath(projectFile, relativePath);
                var outputName = GetReferenceProjectOutputName(projectElement, referenceProjectFile);
                Reports.Information.WriteLine("  Adding project dependency '{0}.{1}'",
                    outputName, WrapperProjectVersion);
                AddProjectDependency(projectJson, outputName, targetFramework);
            }

            // Create wrapper projects for assembly references
            // and add wrapper projects as project references
            foreach(var itemElement in GetItemsByType(projectElement, type: "ReferencePath"))
            {
                if (IsAssemblyFromProjectReference(itemElement)||
                    IsFrameworkAssembly(itemElement) ||
                    IsAssemblyFromNuGetPackage(itemElement, nugetPackagePaths))
                {
                    continue;
                }

                // This assembly becomes a project reference
                var assemblyPath = itemElement.Attribute("evaluated").Value;
                var assemblyProjectName = Path.GetFileNameWithoutExtension(assemblyPath);

                EmitAssemblyWrapper(wrapRoot, targetFramework, assemblyPath);

                Reports.Information.WriteLine("  Adding project dependency '{0}.{1}'",
                    assemblyProjectName, WrapperProjectVersion);
                AddProjectDependency(projectJson, assemblyProjectName, targetFramework);
            }

            PathUtility.EnsureParentDirectory(targetProjectJson);
            File.WriteAllText(targetProjectJson, projectJson.ToString());

            Reports.Information.WriteLine();
        }

        private string GetReferenceProjectOutputName(XElement projectElement, string referenceProjectFile)
        {
            var refProjectElement = projectElement.Parent.Elements("project")
                .Where(x => string.Equals(referenceProjectFile, x.Attribute("projectFile").Value,
                    StringComparison.OrdinalIgnoreCase))
                .SingleOrDefault();

            var refProjectOutputFile = GetOutputAssemblyPath(refProjectElement);
            return Path.GetFileNameWithoutExtension(refProjectOutputFile);
        }

        private static bool IsAssemblyFromNuGetPackage(XElement itemElement, IEnumerable<string> nugetPackagePaths)
        {
            var assemblyPath = itemElement.Attribute("evaluated").Value;
            return nugetPackagePaths.Any(x => PathUtility.IsChildOfDirectory(dir: x, candidate: assemblyPath));
        }

        private static bool IsAssemblyFromProjectReference(XElement itemElement)
        {
            var metadataElements = itemElement.Elements("metadata");
            return metadataElements
                .Any(x => string.Equals("ProjectReferenceOriginalItemSpec", x.Attribute("name").Value));
        }

        private static bool IsFrameworkAssembly(XElement itemElement)
        {
            var resolvedFrom = GetMetadataValue(itemElement, "ResolvedFrom");
            if (string.Equals("{TargetFrameworkDirectory}", resolvedFrom) ||
                string.Equals("ImplicitlyExpandDesignTimeFacades", resolvedFrom))
            {
                return true;
            }

            var frameworkFile = GetMetadataValue(itemElement, "FrameworkFile", throwsIfNotFound: false);
            if (string.Equals("true", frameworkFile))
            {
                return true;
            }

            var isSystemReference = GetMetadataValue(itemElement, "IsSystemReference", throwsIfNotFound: false);
            if (string.Equals("True", isSystemReference))
            {
                return true;
            }

            return false;
        }

        private void EmitAssemblyWrapper(string wrapRoot, FrameworkName targetFramework, string assemblyPath)
        {
            var projectName = Path.GetFileNameWithoutExtension(assemblyPath);
            var targetProjectJson = Path.Combine(wrapRoot, projectName, Runtime.Project.ProjectFileName);

            Reports.Information.WriteLine("  Wrapping project '{0}' for '{1}'", projectName, targetFramework);
            Reports.Information.WriteLine("    Source {0}", assemblyPath.Bold());
            Reports.Information.WriteLine("    Target {0}", targetProjectJson.Bold());

            var projectJson = LoadOrCreateProjectJson(targetProjectJson);

            // Add 'assembly' to 'bin' section of the target framework
            var relativeAssemblyPath = PathUtility.GetRelativePath(targetProjectJson, assemblyPath, PathSeparator);
            AddFrameworkBinPaths(projectJson, relativeAssemblyPath, targetFramework, addPdbPath: false);

            PathUtility.EnsureParentDirectory(targetProjectJson);
            File.WriteAllText(targetProjectJson, projectJson.ToString());
        }

        private static JObject LoadOrCreateJson(string jsonFilePath)
        {
            if (File.Exists(jsonFilePath))
            {
                return JObject.Parse(File.ReadAllText(jsonFilePath));
            }
            else
            {
                return new JObject();
            }
        }

        private static JObject LoadOrCreateProjectJson(string jsonFilePath)
        {
            var projectJson = LoadOrCreateJson(jsonFilePath);
            var version = projectJson["version"]?.Value<string>();
            if (string.IsNullOrEmpty(version))
            {
                projectJson["version"] = WrapperProjectVersion;
            }
            return projectJson;
        }

        private static IEnumerable<XElement> GetItemsByType(XElement projectElement, string type)
        {
            return projectElement.Elements("item").Where(x => x.Attribute("itemType").Value == type);
        }

        private string GetOutputAssemblyPath(XElement projectElement)
        {
            const string projectOutputItemType = "BuiltProjectOutputGroupKeyOutput";
            var itemElement = GetItemsByType(projectElement, type: projectOutputItemType).SingleOrDefault();
            if (itemElement == null)
            {
                throw new InvalidDataException(string.Format("'{0}' is missing from MSBuild output", projectOutputItemType));
            }
            var outputAssemblyPath = itemElement.Attribute("evaluated").Value;

            // The assembly path was mistakenly infered by MSBuild, which used parent dir of
            //ReferenceResolver.xml as base dir. So we need to correct it here.
            var projectDir = PathUtility.EnsureTrailingSlash(
                Path.GetDirectoryName(projectElement.Attribute("projectFile").Value));
            var resolverParentDir = PathUtility.EnsureTrailingSlash(Path.GetDirectoryName(_referenceResolverPath));
            return outputAssemblyPath.Replace(resolverParentDir, projectDir);
        }

        private static FrameworkName GetTargetFramework(XElement projectElement)
        {
            var propertyElements = projectElement.Elements().Where(x => x.Name == "property");
            var targetFrameworkMonikerElement = propertyElements
                .Where(x => x.Attribute("name").Value == "TargetFrameworkMoniker")
                .SingleOrDefault();
            return new FrameworkName(targetFrameworkMonikerElement?.Attribute("evaluated")?.Value);
        }

        private static void AddNuGetDependency(JObject projectJson, NuGetPackage nugetPackage,
            FrameworkName targetFramework)
        {
            var frameworksObj = GetOrAddJObject(projectJson, "frameworks");
            var targetFrameworkObj = GetOrAddJObject(frameworksObj, GetShortFrameworkName(targetFramework));
            var dependenciesObj = GetOrAddJObject(targetFrameworkObj, "dependencies");
            dependenciesObj[nugetPackage.Identity] = nugetPackage.Version;
        }

        private static void AddProjectDependency(JObject projectJson, string projectName, FrameworkName targetFramework)
        {
            var frameworksObj = GetOrAddJObject(projectJson, "frameworks");
            var targetFrameworkObj = GetOrAddJObject(frameworksObj, GetShortFrameworkName(targetFramework));
            var dependenciesObj = GetOrAddJObject(targetFrameworkObj, "dependencies");
            dependenciesObj[projectName] = WrapperProjectVersion;
        }

        private static string GetShortFrameworkName(FrameworkName targetFramework)
        {
            const string portablePrefix = "portable-";
            var shortName = VersionUtility.GetShortFrameworkName(targetFramework);

            // Project.json doesn't accept framework names with portable prefix (e.g. "portable-net45+sl50+win+wpa81")
            // So we strip off the prefix here
            if (shortName.StartsWith(portablePrefix))
            {
                shortName = shortName.Substring(portablePrefix.Length);
            }

            return shortName;
        }

        private static void AddWrappedProjectPath(JObject projectJson, string relativeCsProjectPath, FrameworkName targetFramework)
        {
            var frameworksObj = GetOrAddJObject(projectJson, "frameworks");
            var targetFrameworkObj = GetOrAddJObject(frameworksObj, GetShortFrameworkName(targetFramework));
            targetFrameworkObj["wrappedProject"] = relativeCsProjectPath;
        }

        private static void AddFrameworkBinPaths(JObject projectJson, string assemblyPath, FrameworkName targetFramework, bool addPdbPath)
        {
            var frameworksObj = GetOrAddJObject(projectJson, "frameworks");
            var targetFrameworkObj = GetOrAddJObject(frameworksObj, GetShortFrameworkName(targetFramework));
            var binObj = GetOrAddJObject(targetFrameworkObj, "bin");
            binObj["assembly"] = assemblyPath;

            if (addPdbPath)
            {
                var pdbPath = Path.ChangeExtension(assemblyPath, ".pdb");
                binObj["pdb"] = pdbPath;
            }
        }

        private static JObject GetOrAddJObject(JObject parent, string propertyName)
        {
            var obj = parent[propertyName] as JObject;
            if (obj == null)
            {
                obj = new JObject();
                parent[propertyName] = obj;
            }
            return obj;
        }

        private static IEnumerable<NuGetPackage> ResolveNuGetPackages(string projectDir)
        {
            var configFile = Path.Combine(projectDir, "packages.config");
            var packages = new List<NuGetPackage>();

            if (File.Exists(configFile))
            {
                var packagesDir = ResolvePackagesDirectory(projectDir);
                var xDoc = XDocument.Parse(File.ReadAllText(configFile));
                var packageElements = xDoc.Root.Elements()
                    .Where(x => string.Equals(x.Name.ToString(), "package", StringComparison.OrdinalIgnoreCase));
                foreach (var packageElement in packageElements)
                {
                    var id = packageElement.Attribute("id").Value;
                    var version = packageElement.Attribute("version").Value;
                    packages.Add(new NuGetPackage
                    {
                        Identity = id,
                        Version = version,
                        TargetFramework = packageElement.Attribute("targetFramework").Value,
                        Path = GetNuGetPackagePath(packagesDir, id, version)
                    });
                }
            }

            return packages;
        }

        private static string GetNuGetPackagePath(string packagesDir, string id, string version)
        {
            // Layout of NuGet packages used by csprojs is:
            // C:\Solution\packages\{PackageName}.{Version}\
            // which is different from the layout used by kprojs:
            // C:\Solution\packages\{PackageName}\{Version}\
            return Path.Combine(packagesDir, id + "." + version);
        }

        private static string ResolvePackagesDirectory(string projectDir)
        {
            var rootDir = ProjectResolver.ResolveRootDirectory(projectDir);
            var settings = SettingsUtils.ReadSettings(
                solutionDir: rootDir,
                nugetConfigFile: null,
                fileSystem: new PhysicalFileSystem(projectDir),
                machineWideSettings: new CommandLineMachineWideSettings());
            var packagesDir = settings.GetRepositoryPath();

            // If 'repositoryPath' is not specified in NuGet.config, use {SolutionRoot}/packages as default
            if (string.IsNullOrEmpty(packagesDir))
            {
                packagesDir = Path.Combine(rootDir, "packages");
            }

            return Path.GetFullPath(packagesDir);
        }

        private static string GetMetadataValue(XElement itemElement, string metadataName, bool throwsIfNotFound = true)
        {
            var metadataElement = itemElement.Elements("metadata")
                .SingleOrDefault(x => string.Equals(metadataName, x.Attribute("name")?.Value));
            var metadataValue = metadataElement?.Attribute("evaluated")?.Value;
            if (throwsIfNotFound && string.IsNullOrEmpty(metadataValue))
            {
                throw new InvalidDataException(
                    string.Format("Cannot get value of metadata '{0}' in the following item element:{1}{2}",
                        metadataName, Environment.NewLine, itemElement.ToString()));
            }
            return metadataValue;
        }

        private class NuGetPackage
        {
            public string Identity { get; set; }
            public string Version { get; set; }
            public string TargetFramework { get; set; }
            public string Path { get; set; }
        }
    }
}
