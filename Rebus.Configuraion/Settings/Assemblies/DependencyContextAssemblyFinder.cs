﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using Microsoft.Extensions.DependencyModel;

namespace Rebus.Configuration.Settings.Assemblies
{
    sealed class DependencyContextAssemblyFinder : AssemblyFinder
    {
        readonly DependencyContext _dependencyContext;

        public DependencyContextAssemblyFinder(DependencyContext dependencyContext)
        {
            _dependencyContext = dependencyContext ?? throw new ArgumentNullException(nameof(dependencyContext));
        }

        public override IReadOnlyList<AssemblyName> FindAssembliesContainingName(string nameToFind)
        {
            var query = from library in _dependencyContext.RuntimeLibraries
                        from assemblyName in library.GetDefaultAssemblyNames(_dependencyContext)
                        where IsCaseInsensitiveMatch(assemblyName.Name, nameToFind)
                        select assemblyName;

            return query.ToList().AsReadOnly();
        }
    }
}
