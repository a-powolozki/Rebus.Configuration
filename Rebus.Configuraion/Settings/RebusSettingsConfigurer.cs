using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Reflection;
using Microsoft.Extensions.Configuration;
using Rebus.Config;
using Rebus.Configuration.Settings.Assemblies;
using Rebus.Transport;

namespace Rebus.Configuration.Settings
{
    class RebusSettingsConfigurer
    {
        private IConfigurationSection RootConfigurationSection { get; }
        private RebusConfigurer MainConfigurer { get; }

        private IReadOnlyList<Assembly> Assemblies { get; }

        internal void ReadConfiguration()
        {
            ConfigureLogging();
            ConfigureTransport();
        }

        private void ConfigureLogging()
        {
            var loggingSection = RootConfigurationSection.GetSection("Logging");

            if (loggingSection?.GetChildren().Any() ?? false)
            {
                var methodCalls = GetMethodCalls(loggingSection).ToList();
                var loggingConfigurationMethods = FindLoggingConfigurationMethods();

                InvokeConfigurationMethods<RebusLoggingConfigurer>(
                    methodCalls,
                    loggingConfigurationMethods,
                    MainConfigurer.Logging);
            }
        }

        private IEnumerable<MethodInfo> FindLoggingConfigurationMethods()
        {
            bool LoggingConfigurationMethodFilter(ParameterInfo p) =>
                IsParameterOfExtensionMethod(p) && p.ParameterType == typeof(RebusLoggingConfigurer);

            var configurationMethods = FindConfigurationMethods(LoggingConfigurationMethodFilter);

            return configurationMethods;
        }

        private void ConfigureTransport()
        {
            var transportConfigurationSection = RootConfigurationSection.GetSection("Transport");

            if (transportConfigurationSection?.GetChildren().Any() ?? false)
            {
                var methodCalls = GetMethodCalls(transportConfigurationSection).ToList();

                var transportConfigurationMethods = FindTransportConfigurationMethods();

                InvokeConfigurationMethods<StandardConfigurer<ITransport>>(
                    methodCalls,
                    transportConfigurationMethods,
                    MainConfigurer.Transport);
            }
        }

        private IEnumerable<MethodInfo> FindTransportConfigurationMethods()
        {
            bool TransportConfigurationMethodFilter(ParameterInfo p)
            {
                if (!IsParameterOfExtensionMethod(p)) return false;

                if (!p.ParameterType.IsGenericType) return false;

                if (p.ParameterType == typeof(StandardConfigurer<ITransport>)) return true;

                var parameterTypeDefinition = p.ParameterType.GetGenericTypeDefinition();

                if (parameterTypeDefinition != typeof(StandardConfigurer<>)) return false;

                var firstParameterType = parameterTypeDefinition.GetGenericArguments().First();

                return typeof(ITransport).IsAssignableFrom(firstParameterType);
            }

            var transportConfigurationMethods = FindConfigurationMethods(TransportConfigurationMethodFilter);

            return transportConfigurationMethods;
        }

        private IEnumerable<MethodInfo> FindConfigurationMethods(Func<ParameterInfo, bool> filter, Func<MemberInfo, bool> methodFilter = null)
        {
            var allMatchedConfigurationMethods = Assemblies
                .SelectMany(a => a.ExportedTypes)
                .SelectMany(t => t.GetMethods())
                //.Where(IsExtensionMethod)
                .Where(m => IsExtensionMethod(m) &&
                            m.GetParameters().Any() &&
                            filter(m.GetParameters().First()) ||
                            methodFilter != null && methodFilter(m))
                .ToArray();

            return allMatchedConfigurationMethods;
        }

        private void InvokeConfigurationMethods<T>(
            IEnumerable<MethodInvocationInformation> methodCalls,
            IEnumerable<MethodInfo> configurationMethods,
            Func<Action<T>, object> invocationSource)
        {
            methodCalls.ToList().ForEach(methodCall =>
            {
                ExecuteMethodCallAsAction(methodCall, configurationMethods, invocationSource);
            });
        }

        private void ExecuteMethodCallAsAction<T>(MethodInvocationInformation methodCall,
            IEnumerable<MethodInfo> configurationMethods,
            Func<Action<T>, object> invocationSource)
        {
            var configurationMethod =
                FindConfigurationMethodForMethodCall(methodCall, configurationMethods);
            if (configurationMethod != null)
            {
                var configurationAction = GetConfigurationInvocationAction<T>(configurationMethod, methodCall);

                invocationSource(configurationAction);
            }
        }

        private void InvokeSubsequentConfigurationMethod(MethodInvocationInformation methodCall, object invocationSource)
        {
            if (invocationSource == null || methodCall == null) return;

            var configurationMethods = FindConfigurationMethods(
                p => p.ParameterType.IsInstanceOfType(invocationSource),
                m => m.DeclaringType == invocationSource.GetType());

            var configurationMethod = FindConfigurationMethodForMethodCall(
                methodCall,
                configurationMethods);

            if (configurationMethod == null) return;

            var configurationFunction =
                GetConfigurationInvocationFunction<object>(configurationMethod,
                    methodCall);

            var result = configurationFunction(invocationSource);

            methodCall.SubsequentConfigurations.ToList().ForEach(subsequentConfiguration =>
                InvokeSubsequentConfigurationMethod(subsequentConfiguration, result));
        }

        private Action<T> GetConfigurationInvocationAction<T>(MethodInfo configurationMethod,
            MethodInvocationInformation methodCall)
        {
            void ConfigurationAction(T o)
            {
                var invocationParameters = GetMethodParametersFromMethodCallForConfigurationMethod(methodCall, configurationMethod);
                object invocationTarget = null;
                if (IsExtensionMethod(configurationMethod))
                {
                    invocationParameters.Insert(0, o);
                }
                else
                {
                    invocationTarget = o;
                }

                var result = configurationMethod.Invoke(invocationTarget, invocationParameters.ToArray());

                methodCall.SubsequentConfigurations.ToList().ForEach(subsequentConfiguration =>
                    InvokeSubsequentConfigurationMethod(subsequentConfiguration, result));
            }

            return ConfigurationAction;
        }

        private Func<T, object> GetConfigurationInvocationFunction<T>(MethodInfo configurationMethod,
            MethodInvocationInformation methodCall)
        {
            object ConfigurationFunction(T o)
            {
                var invocationParameters = GetMethodParametersFromMethodCallForConfigurationMethod(methodCall, configurationMethod);
                object invocationInstance = null;
                if (IsExtensionMethod(configurationMethod))
                {
                    invocationParameters.Insert(0, o);
                }
                else
                {
                    invocationInstance = o;
                }
                return configurationMethod.Invoke(invocationInstance, invocationParameters.ToArray());
            }

            return ConfigurationFunction;
        }

        private List<object> GetMethodParametersFromMethodCallForConfigurationMethod(
            MethodInvocationInformation methodCall,
            MethodInfo configurationMethod)
        {
            var result = new List<object>(methodCall.Parameters.Count + 1);
            var parameters = configurationMethod
                .GetParameters();

            if (IsExtensionMethod(configurationMethod))
            {
                parameters = parameters.Skip(1).ToArray();
            }

            var methodParameters = parameters
                .Select(parameter => methodCall.Parameters.Single(p =>
                        p.Key == parameter.Name &&
                        (p.Value != null && parameter.ParameterType.IsInstanceOfType(p.Value) || p.Value == null)
                    ).Value);

            result.AddRange(methodParameters);

            return result;
        }

        private MethodInfo FindConfigurationMethodForMethodCall(
            MethodInvocationInformation methodCall,
            IEnumerable<MethodInfo> methodInfos)
        {
            var result = methodInfos.FirstOrDefault(m =>
                m.Name.Equals(methodCall.MethodName, StringComparison.OrdinalIgnoreCase) &&
                (IsExtensionMethod(m) &&
                 m.GetParameters().Length == methodCall.Parameters.Count + 1 &&
                 ParametersMatched(m.GetParameters().Skip(1).ToArray(), methodCall.Parameters) ||
                !IsExtensionMethod(m) &&
                m.GetParameters().Length == methodCall.Parameters.Count &&
                ParametersMatched(m.GetParameters(), methodCall.Parameters)));

            return result;
        }

        private bool ParametersMatched(ParameterInfo[] parameters, IDictionary<string, object> arguments)
        {
            return parameters.All(parameter =>
                arguments.TryGetValue(parameter.Name, out var parameterValue) &&
                ParameterValueMatched(parameter, parameterValue));
        }

        private bool ParameterValueMatched(ParameterInfo parameter, object value)
        {
            var valueType = value.GetType();

            return parameter.ParameterType == valueType ||
                   parameter.ParameterType.IsAssignableFrom(valueType);
        }

        private IEnumerable<MethodInvocationInformation> GetMethodCalls(IConfigurationSection section)
        {
            var children = section.GetChildren().ToList();

            if (!children.Any()) return Enumerable.Empty<MethodInvocationInformation>();

            var items = children
                .Where(child => child.Value != null)
                .Select(child => new
                {
                    Name = child.Value,
                    Args = new Dictionary<string, object>(),
                    Subs = Enumerable.Empty<MethodInvocationInformation>()
                })
                .Concat(children
                    .Where(child => child.Value == null)
                    .Select(child => new
                    {
                        Name = child.GetSection("Name").Value ?? throw new InvalidOperationException(),
                        Args = child.GetSection("Args").GetChildren()
                            .Select(argument => new { argument.Key, argument.Value })
                            .ToDictionary(e => e.Key, e => (object)e.Value),
                        Subs = GetMethodCalls(child.GetSection("Subs"))
                    }));

            return items.Select(e => new MethodInvocationInformation(e.Name, e.Args, e.Subs));
        }

        private static bool IsParameterOfExtensionMethod(ParameterInfo parameter)
        {
            if (parameter.Member.MemberType != MemberTypes.Method) return false;
            var method = (MethodInfo)parameter.Member;
            return IsExtensionMethod(method);
        }

        private static bool IsExtensionMethod(MethodInfo method)
        {
            return method.IsStatic && method.IsPublic;
        }

        public RebusSettingsConfigurer(RebusConfigurer configurer, IConfigurationSection rootConfigurationSection)
        {
            MainConfigurer = configurer;
            RootConfigurationSection = rootConfigurationSection;
            var assemblyNames =
                new DllScanningAssemblyFinder().FindAssembliesContainingName("Rebus").ToArray();
            Assemblies = new ReadOnlyCollection<Assembly>(
                assemblyNames.Select(asmn =>
                 {
                     try
                     {
                         return Assembly.Load(asmn);
                     }
                     catch
                     {
                         return null;
                     }
                 }).Where(e => e != null).ToList());
        }
    }
}