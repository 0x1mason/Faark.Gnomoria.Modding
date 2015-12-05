using IniParser;
using IniParser.Model;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Threading.Tasks;

namespace Gemini.Util
{
    /// <summary>
    /// A simple wrapper for the ini config file.
    /// </summary>
    internal class IniFile
    {
        private const string FILE_NAME = "gemini.ini";
        private FileIniDataParser _parser;
        private IniData _data;

        /// <summary>
        /// Initializes a new instance of the <see cref="IniFile"/> class.
        /// </summary>
        /// <exception cref="System.IO.DirectoryNotFoundException"></exception>
        private IniFile()
        {
            _parser = new FileIniDataParser();
            _parser.Parser.Configuration.CommentString = "#";
            _data = _parser.ReadFile(FILE_NAME);

            GameDirectory = new DirectoryInfo(_data["Game"]["Root"]);

            if (!GameDirectory.Exists)
            {
                throw new DirectoryNotFoundException(GameDirectory.FullName + " does not exist.");
            }

            ModDirectory = new DirectoryInfo(_data["Game"]["Mods"]);

            if (!ModDirectory.Exists)
            {
                throw new DirectoryNotFoundException(ModDirectory.FullName + " does not exist.");
            }
        }

        private string ComputeHash ()
        {
            using (var md5 = MD5.Create())
            {
                using (var stream = StreamFromString(_data.ToString()))
                {
                    return Convert.ToBase64String(md5.ComputeHash(stream));
                }
            }
        }

        private MemoryStream StreamFromString (string value)
        {
            return new MemoryStream(Encoding.UTF8.GetBytes(value ?? ""));
        }

        /// <summary>
        /// Writes this instance to file.
        /// </summary>
        internal void Write ()
        {
            if (HasChanged)
            {
                _data["Gemini"]["Checksum"] = ComputeHash();
                _parser.WriteFile(FILE_NAME, _data);
            }
        }

        /// <summary>
        /// Gets a value indicating whether this instance has changed.
        /// </summary>
        /// <value>
        /// <c>true</c> if this instance has changed; otherwise, <c>false</c>.
        /// </value>
        internal bool HasChanged
        {
            get
            {
                return _data["Gemini"]["Checksum"] != ComputeHash();
            }
        }

        /// <summary>
        /// Gets the game directory.
        /// </summary>
        /// <value>
        /// The game directory.
        /// </value>
        internal DirectoryInfo GameDirectory
        {
            get;
            private set;
        }

        /// <summary>
        /// Gets the mod directory.
        /// </summary>
        /// <value>
        /// The mod directory.
        /// </value>
        internal DirectoryInfo ModDirectory
        {
            get;
            private set;
        }

        internal string GameVersion
        {
            get
            {
                return _data["Gemini"]["GameVersion"];
            }

            set
            {
                _data["Gemini"]["GameVersion"] = value;
            }
        }

        /// <summary>
        /// Gets the mods.
        /// </summary>
        /// <value>
        /// The mods.
        /// </value>
        internal Dictionary<string, bool> Mods
        {
            get
            {
                if (_mods.Count == 0)
                {                    
                    foreach (var kvp in _data["Mods"])
                    {
                        _mods.Add(kvp.KeyName, kvp.Value.ToLowerInvariant() == "E");
                    }
                }

                return _mods;
            }
        }
        private Dictionary<string, bool> _mods = new Dictionary<string, bool>();

        /// <summary>
        /// Gets the singleton instance of the file.
        /// </summary>
        /// <value>
        /// The instance.
        /// </value>
        internal static IniFile Instance
        {
            get
            {
                if (_instance == null)
                {
                    _instance = new IniFile();
                }

                return _instance;
            }
        }
        private static IniFile _instance;
    }
}
