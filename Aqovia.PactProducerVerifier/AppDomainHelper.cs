using System;
using System.Globalization;
using System.IO;
using System.Reflection;
using System.Security.Policy;

// orginal code can be found here https://www.codeproject.com/Articles/42312/Loading-Assemblies-in-Separate-Directories-Into-a-.aspx
namespace Aqovia.PactProducerVerifier
{
    internal class AppDomainHelper : IDisposable
    {
        private AppDomain _childDomain;

        #region Public Methods
        /// <summary>
        /// Loads an assembly into a new AppDomain and returns it. 
        /// The new AppDomain is then Unloaded in the dispose method
        /// </summary>
        /// <param name="assemblyLocation">The Assembly file 
        /// location</param>
        /// <returns>The loaded assembly</returns>
        internal Assembly LoadAssembly(FileInfo assemblyLocation)
        {
            if (string.IsNullOrEmpty(assemblyLocation.Directory?.FullName))
            {
                throw new InvalidOperationException("Directory can't be null or empty.");
            }

            if (!Directory.Exists(assemblyLocation.Directory.FullName))
            {
                throw new InvalidOperationException(string.Format(CultureInfo.CurrentCulture,
                   "Directory not found {0}", assemblyLocation.Directory.FullName));
            }

            _childDomain = BuildChildDomain(AppDomain.CurrentDomain);
            var loaderType = typeof(AssemblyLoader);
            var loader = (AssemblyLoader)_childDomain.CreateInstanceFrom(
                    loaderType.Assembly.Location,
                    loaderType.FullName).Unwrap();

            var assembly = loader.LoadAssembly(assemblyLocation.FullName);

            return assembly;
        }
        #endregion

        #region Private Methods
        /// <summary>
        /// Creates a new AppDomain based on the parent AppDomains 
        /// Evidence and AppDomainSetup
        /// </summary>
        /// <param name="parentDomain">The parent AppDomain</param>
        /// <returns>A newly created AppDomain</returns>
        private static AppDomain BuildChildDomain(AppDomain parentDomain)
        {
            Evidence evidence = new Evidence(parentDomain.Evidence);
            AppDomainSetup setup = parentDomain.SetupInformation;
            return AppDomain.CreateDomain("DiscoveryRegion", evidence, setup);
        }
        #endregion

        /// <inheritdoc />
        /// <summary>
        /// Remotable AssemblyLoader, this class 
        /// inherits from <c>MarshalByRefObject</c> 
        /// to allow the CLR to marshall
        /// this object by reference across 
        /// AppDomain boundaries
        /// </summary>
        private class AssemblyLoader : MarshalByRefObject
        {
            /// <summary>
            /// ReflectionOnlyLoad of single Assembly based on 
            /// the assemblyPath parameter
            /// </summary>
            /// <param name="assemblyPath">The path to the Assembly</param>
            // ReSharper disable once MemberCanBeMadeStatic.Local
            internal Assembly LoadAssembly(string assemblyPath)
            {
                try
                {
                    return Assembly.ReflectionOnlyLoadFrom(assemblyPath);
                }
                catch (FileNotFoundException)
                {
                    /* Continue loading assemblies even if an assembly
                     * can not be loaded in the new AppDomain. */
                }
                return null;
            }
        }

        public void Dispose()
        {
            AppDomain.Unload(_childDomain);
        }
    }
}