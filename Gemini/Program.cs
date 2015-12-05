using Gemini.Util;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Gemini
{
    class Program
    {
        static void Main (string[] args)
        {
          //  if (args.Contains("-update"))
            {
                ModLoader.Update();
                GameLauncher.Run();
              //  ModLoader.LaunchWithMods();
            }
          //  ModLoader.Load();
        }
    }
}
