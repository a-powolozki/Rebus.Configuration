using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;

namespace Rebus.Configuration.Settings.Assemblies
{
    sealed class DllScanningAssemblyFinder : AssemblyFinder
    {
        public override IReadOnlyList<AssemblyName> FindAssembliesContainingName(string nameToFind)
        {
            var probeDirs = new List<string>();

            if (!string.IsNullOrEmpty(AppDomain.CurrentDomain.BaseDirectory))
            {
                probeDirs.Add(AppDomain.CurrentDomain.BaseDirectory);

#if PRIVATE_BIN
                var privateBinPath = AppDomain.CurrentDomain.SetupInformation.PrivateBinPath;
                if (!string.IsNullOrEmpty(privateBinPath))
                {
                    foreach (var path in privateBinPath.Split(';'))
                    {
                        if (Path.IsPathRooted(path))
                        {
                            probeDirs.Add(path);
                        }
                        else
                        {
                            probeDirs.Add(Path.Combine(AppDomain.CurrentDomain.BaseDirectory, path));
                        }
                    }
                }
#endif
            }
            else
            {
                probeDirs.Add(Path.GetDirectoryName(typeof(AssemblyFinder).Assembly.Location));
            }

            return probeDirs.Where(Directory.Exists).SelectMany(probeDir =>
                {
                    var files = Directory.GetFiles(probeDir, "*.dll");
                    var matchedFiles = files
                    .Where(oap => IsCaseInsensitiveMatch(Path.GetFileNameWithoutExtension(oap), nameToFind));
                    var assemblies = matchedFiles.Select(TryGetAssemblyNameFrom)
                        .Where(e => e != null);
                    return assemblies;
                })
                .ToList()
                .AsReadOnly();

            AssemblyName TryGetAssemblyNameFrom(string path)
            {
                try
                {
                    return AssemblyName.GetAssemblyName(path);
                }
                catch (BadImageFormatException)
                {
                    return null;
                }
            }
        }
    }
}
