// -----------------------------------------------------------------------------
// PROJECT   : Avant Garde
// COPYRIGHT : Andy Thomas
// LICENSE   : GPLv3
// HOMEPAGE  : https://kuiper.zone/avantgarde-avalonia/
// -----------------------------------------------------------------------------

using System;
using System.Diagnostics;
using System.IO;
using System.Text.Json;

namespace AvantGarde.Settings
{
    /// <summary>
    /// Base class for JSON settings with common load and save methods.
    /// </summary>
    public abstract class JsonSettings
    {
        static JsonSettings()
        {
            try
            {
                // Create config directory
                var conf = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "AvantGarde");
                Directory.CreateDirectory(conf);
                ConfigDirectory = conf;
            }
            catch (Exception e)
            {
                Debug.WriteLine(e);
            }
        }

        /// <summary>
        /// Get settings directory. Null if directory creation failed.
        /// </summary>
        public static readonly string? ConfigDirectory;

        /// <summary>
        /// Read the file and assigns properties to this instance.
        /// Requires implementation in subtype. Returns true on success (does not throw).
        /// </summary>
        public abstract bool Read();

        /// <summary>
        /// Writes the file. Returns true on success (does not throw).
        /// </summary>
        public bool Write()
        {
            if (ConfigDirectory != null)
            {
                try
                {
                    var type = GetType();
                    var path = Path.Combine(ConfigDirectory, type.Name + ".json");
                    using var fs = File.Create(path);

                    var opts = new JsonSerializerOptions
                        { WriteIndented = true, PropertyNameCaseInsensitive = true };

                    JsonSerializer.Serialize(fs, this, type, opts);
                    return true;
                }
                catch(Exception e)
                {
                    Debug.WriteLine(e);
                }
            }

            return false;
        }

        /// <summary>
        /// Deletes app configuration directory. Does not throw. Possible use for uninstall?
        /// </summary>
        public void Remove()
        {
            if (ConfigDirectory != null)
            {
                try
                {
                    Directory.Delete(ConfigDirectory);
                }
                catch (Exception e)
                {
                    Debug.WriteLine(e);
                }
            }
        }

        /// <summary>
        /// Internal implementation. Returns an instance of T or null on failure.
        /// </summary>
        protected T? Read<T>()
            where T : JsonSettings, new()
        {
            if (ConfigDirectory != null)
            {
                try
                {
                    var path = Path.Combine(ConfigDirectory, GetType().Name + ".json");
                    using var fs = File.OpenRead(path);

                    var opts = new JsonSerializerOptions
                        { WriteIndented = true, PropertyNameCaseInsensitive = true };

                    return JsonSerializer.Deserialize<T>(fs, opts);
                }
                catch(Exception e)
                {
                    Debug.WriteLine(e);
                }
            }

            return null;
        }

    }

}