using Faark.Gnomoria.Modding;
using System;
using System.Collections.Generic;
using System.Data;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;
using Faark.Util;
using IniParser;
using IniParser.Model;
using System.IO;
using System.Diagnostics;

namespace Gemini.Util
{
    public class ModLoader
    {
        private static ModEnvironmentConfiguration current_config;
        private static DataTable table;
        private static List<Tuple<IMod, bool, DataRow>> found_valid_mods_data;
        private static bool is_build_required = true;
        
        public static List<Assembly> Load ()
        {
            EnsureDepenciesAreLoaded();
            var assembliesToLoad = GetAssembliesToLoad();
            var loaded = new List<Assembly>();

            foreach (var a in assembliesToLoad)
            {
                loaded.Add(Assembly.LoadFrom(a));
            }

            return loaded;
        }

        private static bool IsModType(Type type)
        {
            return typeof(IMod).IsAssignableFrom(type) && !(typeof(Faark.Gnomoria.Modding.SupportMod).IsAssignableFrom(type));
        }

        public static List<string> GetAssembliesToLoad ()
        {
            var assemblyNames = new List<string>();
            var modFiles = IniFile.Instance.ModDirectory.GetFiles("*.dll");

            AppDomain testDomain = AppDomain.CreateDomain("TestDomain", null);

            foreach (var file in modFiles)
            {
                Assembly assembly = null;

                try
                {
                    assembly = testDomain.Load(file.FullName);
                    //assembly = Assembly.LoadFrom(file.FullName);

                    foreach (var type in assembly.GetTypes())
                    {
                        if (IsModType(type))
                        {
                            IMod mod = (IMod) Activator.CreateInstance(type);

                            if (IniFile.Instance.Mods.ContainsKey(mod.Name) &&
                                IniFile.Instance.Mods[mod.Name])
                            {
                                assemblyNames.Add(file.FullName);
                                break;
                            }
                        }
                    }
                } catch (ReflectionTypeLoadException)
                {
                    // TODO: Handle classes failed to load
                } catch (BadImageFormatException)
                {
                    // TODO: Handle wrong bitness
                } catch (Exception err)
                {
                    // TODO: Handle
                }
            }

            AppDomain.Unload(testDomain);

            return assemblyNames;
        }

        public static List<IMod> ModsToRun ()
        {
            List<IMod> mods = new List<IMod>();
            var assemblies = GetAssembliesToLoad();

            foreach (var file in assemblies)
            {
                var assembly = Assembly.LoadFrom(file);

                foreach (var type in assembly.GetTypes().Where(t => IsModType(t)))
                {
                    IMod mod = (IMod) Activator.CreateInstance(type);

                    if (IniFile.Instance.Mods.ContainsKey(mod.Name) &&
                        IniFile.Instance.Mods[mod.Name])
                    {
                        mods.Add(mod);
                    }
                }
            }

            return mods;
        }

        private static void EnsureDepenciesAreLoaded ()
        {
            //Make sure all assemblys are loaded & can be used by/referenced our mods 
            foreach (var dep in ModManager.Dependencies)
            {
                var fullLoc = new System.IO.FileInfo(dep).FullName.ToUpper();
                var allLocs = AppDomain.CurrentDomain.GetAssemblies().Select(ass => ass.Location.ToUpper()).ToArray();
                if (AppDomain.CurrentDomain.GetAssemblies().Where(ass => ass.Location.ToUpper() == fullLoc).Count() <= 0)
                {
                    Assembly.LoadFrom(dep);
                }
            }
        }


        public static void Update ()
        {
            // check for any new mods
            var mods = SearchForInstalledMods();

            // check if the game version has changed
            FileVersionInfo fi = FileVersionInfo.GetVersionInfo(ModManager.GamePath);
            IniFile.Instance.GameVersion = fi.ProductVersion;

            // if changes have occurred, rebuild the modded binaries
            if (IniFile.Instance.HasChanged)
            {
                IniFile.Instance.Write();
                BuildModdedBinaries(mods);
            }
        }

        // need to combine this with GetAssembliesToLoad
        private static IMod[] SearchForInstalledMods ()
        {
            var searcher = new LocalModsFinder();
            List<IMod> mods = new List<IMod>();

            searcher.OnModFound += new EventHandler<EventArgs<IMod>>((send, args) =>
            {
                IMod mod = args.Argument;
                
                // multiple instances are ignored
                if (!IniFile.Instance.Mods.ContainsKey(mod.Name))
                {
                    IniFile.Instance.Mods[mod.Name] = true;
                    mods.Add(mod);
                }
            });

            searcher.RunSync(ModManager.GameDirectory);

            return mods.ToArray();
        }


        private static void BuildModdedBinaries (IMod[] mods)
        {
            EnsureDepenciesAreLoaded();
            ModManager.GameDirectory.ContainingFile(ModManager.ModdedExecutable).Delete();
            ModManager.GameDirectory.ContainingFile(ModManager.ModdedLibrary).Delete();
            ModdedBinaryBuilder.Build(mods, new IMod[]{},false);
        }
    }
}
