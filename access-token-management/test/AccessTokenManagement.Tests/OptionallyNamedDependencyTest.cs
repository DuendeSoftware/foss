using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Cryptography.X509Certificates;
using System.Text;
using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;

namespace Duende.AccessTokenManagement
{
    public class OptionallyKeyedDependencyTest
    {
        [Fact]
        public void Uses_default_dependency_if_no_keyed_registered()
        {
            var sp = new ServiceCollection()
                .AddSingleton<IExampleDependency>(new ExampleDependency("default"))
                .AddTransient<OptionallyKeyedDependency<IExampleDependency>>()
                .BuildServiceProvider();

            var dependency = sp.GetRequiredService<OptionallyKeyedDependency<IExampleDependency>>().Dependency;
            dependency.Name.ShouldBe("default");

        }

        [Fact]
        public void Uses_keyed_dependency_if_provided()
        {
            var sp = new ServiceCollection()
                .AddSingleton<IExampleDependency>(new ExampleDependency("default"))
                .AddKeyedSingleton<IExampleDependency>(OptionallyKeyedDependency.Duende, new ExampleDependency("duende"))
                .AddTransient<OptionallyKeyedDependency<IExampleDependency>>()
                .BuildServiceProvider();

            var dependency = sp.GetRequiredService<OptionallyKeyedDependency<IExampleDependency>>().Dependency;
            dependency.Name.ShouldBe("duende");

        }

        [Fact]
        public void Can_override_key_for_keyed_dependency()
        {
            // This test looks if we can change the injectionkey for a single usage of OptionallyKeyedDependency,
            // not for all of them. By default, all usages of OptionallyKeyedDependency will use the key "duende".
            // This means you can override a dependency for ALL of duende's usages, but not for a single usage

            // For example, if you only want to inject a diffent cache in DistributedDPoPNonceStore, but not for DistributedClientCredentialsTokenCache
            
            var sp = new ServiceCollection()
                // Register a default example dependency (which shouldn't be resolved)
                .AddSingleton<IExampleDependency>(new ExampleDependency("default"))
                .AddKeyedSingleton<IExampleDependency>(OptionallyKeyedDependency.Duende, new ExampleDependency("duende"))
                .AddTransient<OptionallyKeyedDependency<IExampleDependency>>()

                // Now change the optionally keyed dependency
                .AddKeyedSingleton<IExampleDependency>("different", new ExampleDependency("different"))
                .AddKeyedTransient<OptionallyKeyedDependency<IExampleDependency>, CustomOptionallyKeyedDependency<IExampleDependency>>("customkey")
                .BuildServiceProvider();

            var dependency = sp.GetRequiredKeyedService<OptionallyKeyedDependency<IExampleDependency>>("customkey").Dependency;
            dependency.Name.ShouldBe("different");

        }

        /// <summary>
        /// You can change the key of a dependency by deriving from OptionallyKeyedDependency.
        /// </summary>
        /// <typeparam name="T"></typeparam>
        public class CustomOptionallyKeyedDependency<T>(
            T defaultDependency,

            // Here the key is overwritten
            [FromKeyedServices(("different"))] T? keyedDependency = default(T?))
            : OptionallyKeyedDependency<T>(defaultDependency, keyedDependency)
            where T : class;

        private interface IExampleDependency
        {
            public string Name { get; }
        }

        private record ExampleDependency(string Name) : IExampleDependency
        {

        }
}

    
}
