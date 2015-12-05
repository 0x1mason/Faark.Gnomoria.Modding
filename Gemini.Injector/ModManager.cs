using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace Gemini.Util
{
    class ModManager
    {
       // public const string config_file_name = "GnomoriaModConfig.xml";
        public const string OriginalExecutable = "Gnomoria.exe";
        public const string ModdedExecutable = "GnomoriaModded.dll";
        public const string OriginalLibrary = "gnomorialib.dll";
        public const string ModdedLibrary = "gnomorialibModded.dll";
        public const string ModController = "Gemini.Injector.dll";
        public static readonly string[] Dependencies = new string[] { /*"Gemini.Injector.dll", */"Gnomoria.exe", "gnomorialib.dll", "SevenZipSharp.dll" };

        public static System.IO.DirectoryInfo GameDirectory
        {
            get
            {
                return IniFile.Instance.GameDirectory;
            }
        }

        public static string GamePath
        {
            get
            {
                return System.IO.Path.Combine(GameDirectory.FullName, OriginalExecutable);
            }
        }
    }
}
