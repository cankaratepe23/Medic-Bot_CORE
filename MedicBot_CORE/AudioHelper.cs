using FuzzySharp.Extractor;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.Serialization.Formatters.Binary;
using System.Text;

namespace MedicBot
{
    /// <summary>
    ///  Helper/store class which holds a 'database' of sorts for current audio entries and all the methods associated with managing said database.
    /// </summary>
    public static class AudioHelper
    {
        public const string NoCollectionName = "!!!!!!!!!!";

        private static Dictionary<string, AudioEntry> AudioEntries = new Dictionary<string, AudioEntry>();
        private static Dictionary<string, AudioEntry> Aliases = new Dictionary<string, AudioEntry>();
        private static Dictionary<string, AudioCollection> AudioCollections = new Dictionary<string, AudioCollection>();
        private static List<AudioEntry> UniversalIntros = new List<AudioEntry>();
        private static Dictionary<ulong, List<AudioEntry>> UserIntros = new Dictionary<ulong, List<AudioEntry>>();

        public static int MinimumScore { get; set; }
        public static string ResDirectory { get; set; }

        public static void CheckForErrors()
        {
            foreach (AudioEntry audioEntry in AudioEntries.Values)
            {
                if (!File.Exists(audioEntry.Path))
                {
                    if (File.Exists(audioEntry.Path.Substring(0, audioEntry.Path.Length - 4) + "wav"))
                    {
                        Console.WriteLine($"{audioEntry.Name} is a wav file, renaming.");
                        File.Move(audioEntry.Path.Substring(0, audioEntry.Path.Length - 4) + "wav", audioEntry.Path);
                    }
                    else
                    {
                        Console.ForegroundColor = ConsoleColor.Red;
                        Console.WriteLine($"COULD NOT FIND {audioEntry.Name}");
                        Console.ResetColor();
                        DeleteAudio(audioEntry);
                        Save();
                    }
                }
            }
        }

        public static void Save()
        {
            SaveStore();
            SaveSettings();
        }

        public static void Export()
        {
            using FileStream stream = File.Open("store.json", FileMode.Create);
            StreamWriter streamWriter = new StreamWriter(stream);
            streamWriter.Write(JsonConvert.SerializeObject(new object[] { AudioEntries, Aliases, AudioCollections, UniversalIntros, UserIntros }));
        }

        private static void SaveStore()
        {
            using Stream stream = File.Open("store.medicbot", FileMode.Create);
            BinaryFormatter formatter = new BinaryFormatter();
            formatter.Serialize(stream, new object[] { AudioEntries, Aliases, AudioCollections, UniversalIntros, UserIntros });
            stream.Flush();
        }

        private static void SaveSettings()
        {
            File.WriteAllText("settings.json", JsonConvert.SerializeObject(new object[] { MinimumScore, ResDirectory }));
        }

        public static void Load()
        {
            if (File.Exists("settings.json"))
            {
                dynamic settings = JsonConvert.DeserializeObject(File.ReadAllText("settings.json"));
                MinimumScore = (int)settings[0];
                ResDirectory = (string)settings[1];
            }
            else
            {
                GenerateSettings();
            }
            if (File.Exists("store.medicbot"))
            {
                Stream stream = File.Open("store.medicbot", FileMode.Open);
                BinaryFormatter binaryFormatter = new BinaryFormatter();
                object[] deserialized = (object[])binaryFormatter.Deserialize(stream);
                LoadFromDeserialized(deserialized);
                stream.Dispose();
            }
            else
            {
                GenerateStore();
            }
        }

        private static void LoadFromDeserialized(object[] deserialized)
        {
            AudioEntries = (Dictionary<string, AudioEntry>)deserialized[0];
            Aliases = (Dictionary<string, AudioEntry>)deserialized[1];
            AudioCollections = (Dictionary<string, AudioCollection>)deserialized[2];
            UniversalIntros = (List<AudioEntry>)deserialized[3];
            UserIntros = (Dictionary<ulong, List<AudioEntry>>)deserialized[4];

        }

        private static void GenerateStore()
        {
            AudioEntries = new Dictionary<string, AudioEntry>();
            foreach (string file in Directory.GetFiles(ResDirectory, "*.opus"))
            {
                AudioEntries.Add(Path.GetFileNameWithoutExtension(file), new AudioEntry(Path.GetFileNameWithoutExtension(file), Path.GetExtension(file), AudioEntry.AudioType.File, 0, ""));
            }
            SaveStore();
        }

        private static void GenerateSettings()
        {
            MinimumScore = 80;
            ResDirectory = Path.Combine(Directory.GetCurrentDirectory(), "res");
            SaveSettings();
        }

        public static void AddAudio(AudioEntry audioToAdd)
        {
            AudioEntries.Add(audioToAdd.Name, audioToAdd);
        }

        public static void DeleteAudio(AudioEntry audioToDelete)
        {
            try
            {
                File.Move(audioToDelete.Path, Path.Combine(ResDirectory, "trash", audioToDelete.FileName));
            }
            catch (FileNotFoundException)
            {
                Console.ForegroundColor = ConsoleColor.Red;
                Console.WriteLine($"{audioToDelete.Path} could not be found.The file has been removed from the database but please check that it has been deleted properly.");
                Console.ResetColor();
            }
            AudioEntries.Remove(audioToDelete.Name);
            foreach (string alias in audioToDelete.Aliases)
            {
                Aliases.Remove(alias);
            }

            foreach (AudioCollection collection in AudioCollections.Values)
            {
                if (collection.AudioEntries.ContainsValue(audioToDelete))
                {
                    collection.AudioEntries.Remove(audioToDelete.Name);
                    foreach (string alias in audioToDelete.Aliases)
                    {
                        collection.Aliases.Remove(alias);
                    }
                }
            }
        }

        public static void AddToAlias(string alias, AudioEntry entry)
        {
            Aliases[alias] = entry;
        }

        public static List<AudioCollection> GetAudioCollections()
        {
            return AudioCollections.Values.ToList();
        }

        public static void AddToCollection(string collection, AudioEntry entry)
        {
            AudioCollections.TryGetValue(collection, out AudioCollection col);
            if (col == null)
            {
                AudioCollections[collection] = new AudioCollection();
                col = AudioCollections[collection];
            }
            col.AudioEntries.Add(entry.Name, entry);
            foreach (string alias in entry.Aliases)
            {
                col.Aliases.Add(alias, entry);
            }
            entry.Collections.Add(collection);
        }

        public static void RemoveFromCollection(string collection, AudioEntry entry)
        {
            if (AudioCollections.TryGetValue(collection, out AudioCollection col))
            {
                col.AudioEntries.Remove(entry.Name);
                foreach (string alias in entry.Aliases)
                {
                    col.Aliases.Remove(alias);
                }
                entry.Collections.Remove(collection);
            }
            else
            {
                throw new CollectionNotFoundException(collection);
            }
        }

        public static string GetIntroDump()
        {
            StringBuilder sb = new StringBuilder();
            List<AudioEntry> universalIntros = GetUniversalIntros();
            sb.AppendLine("Universal Intros:");
            sb.AppendJoin("\n", universalIntros);
            sb.AppendLine();
            foreach (ulong key in UserIntros.Keys)
            {
                sb.AppendFormat("Intros for {0}", key);
                sb.AppendLine();
                sb.AppendJoin("\n", UserIntros[key]);
            }
            return sb.ToString();
        }

        public static List<AudioEntry> GetUserIntros(ulong userId)
        {
            if (UserIntros.ContainsKey(userId))
            {
                return UserIntros[userId];
            }
            else
            {
                return null;
            }
        }

        public static void AddToUserIntros(ulong userId, AudioEntry audioEntry)
        {
            if (UserIntros.TryGetValue(userId, out List<AudioEntry> intros))
            {
                intros.Add(audioEntry);
            }
            else
            {
                UserIntros.Add(userId, new List<AudioEntry>() { audioEntry });
            }
        }

        public static void AddToUserIntros(ulong userId, IEnumerable<AudioEntry> audioEntries)
        {
            if (UserIntros.TryGetValue(userId, out List<AudioEntry> intros))
            {
                intros.AddRange(audioEntries);
            }
            else
            {
                UserIntros.Add(userId, new List<AudioEntry>(audioEntries.Count()));
                UserIntros[userId].AddRange(audioEntries);
            }
        }

        public static void RemoveFromUserIntros(ulong userId, AudioEntry audioEntry)
        {
            UserIntros[userId].Remove(audioEntry);
        }

        public static void AddToUniversalIntros(AudioEntry audioEntry)
        {
            UniversalIntros.Add(audioEntry);
        }

        public static void AddToUniversalIntros(IEnumerable<AudioEntry> audioEntries)
        {
            UniversalIntros.AddRange(audioEntries);
        }

        public static void RemoveFromUniversalIntros(AudioEntry audioEntry)
        {
            UniversalIntros.Remove(audioEntry);
        }

        public static List<AudioEntry> GetUniversalIntros()
        {
            return UniversalIntros;
        }

        //TODO Add microtransaction/permission system to collections
        /// <summary>
        /// Fuzzy finds an audio entry with the given name/collection:name pair.
        /// </summary>
        /// <param name="audioName">Audio name (key) or collection:name pair.</param>
        /// <returns>The audio entry with the highest search score that passes the minimum score.</returns>
        /// <exception cref="CollectionNotFoundException"></exception>
        public static AudioEntry FindAudio(string audioName, bool exact = false)
        {
            Dictionary<string, AudioEntry> searchDictEntries = AudioEntries;
            Dictionary<string, AudioEntry> searchDictAliases = Aliases;

            if (audioName.Contains(":"))
            {
                string[] quantized = audioName.Split(':');
                string collectionName = quantized[0];
                audioName = quantized[1].Trim();
                if (AudioCollections.TryGetValue(collectionName, out AudioCollection audioCollection))
                {
                    searchDictEntries = audioCollection.AudioEntries;
                    searchDictAliases = audioCollection.Aliases;
                }
                else
                {
                    throw new CollectionNotFoundException();
                }
            }

            if (audioName == "random" || audioName == "")
            {
                return searchDictEntries.Values.ElementAt(new Random().Next(searchDictEntries.Count));
            }

            if (searchDictEntries.TryGetValue(audioName, out AudioEntry returnVal) || searchDictAliases.TryGetValue(audioName, out returnVal))
            {
                return returnVal;
            }

            if (exact)
            {
                throw new AudioEntryNotFoundException();
            }

            ExtractedResult<string> result = null;
            if ((result = FuzzySharp.Process.ExtractOne(audioName, searchDictEntries.Keys)) != null && result.Score >= MinimumScore)
            {
                return searchDictEntries.Values.ElementAt(result.Index);
            }
            else if ((result = FuzzySharp.Process.ExtractOne(audioName, searchDictAliases.Keys)) != null && result.Score >= MinimumScore)
            {
                return searchDictAliases.Values.ElementAt(result.Index);
            }
            else
            {
                throw new AudioEntryNotFoundException();
            }
        }

        public static bool AudioExists(string audioName, bool exact)
        {
            try
            {
                FindAudio(audioName, exact);
                return true;
            }
            catch (AudioEntryNotFoundException)
            {
                return false;
            }
        }

        /// <summary>
        /// Returns sorted list of audio entries containing the search string. Search is done inside a collection if given a collection:name pair.
        /// </summary>
        /// <param name="searchString"></param>
        /// <returns>Sorted list of all audio entries containing the search string.</returns>
        /// <exception cref="CollectionNotFoundException"></exception>
        /// <exception cref="NoResultsFoundException"></exception>
        public static List<AudioEntry> FindAll(string searchString)
        {
            if (searchString == null || string.IsNullOrWhiteSpace(searchString))
            {
                return AudioEntries.Values.ToList();
            }
            Dictionary<string, AudioEntry> searchDictEntries = AudioEntries;
            Dictionary<string, AudioEntry> searchDictAliases = Aliases;

            if (searchString.Contains(":"))
            {
                string[] quantized = searchString.Split(':');
                string collectionName = quantized[0];
                searchString = quantized[1].Trim();
                if (AudioCollections.TryGetValue(collectionName, out AudioCollection audioCollection))
                {
                    searchDictEntries = AudioCollections[collectionName].AudioEntries;
                    searchDictAliases = AudioCollections[collectionName].Aliases;
                }
                else
                {
                    throw new CollectionNotFoundException();
                }
            }

            List<AudioEntry> result = searchDictEntries.Where(a => a.Key.Contains(searchString)).Select(a => a.Value).ToList();
            List<AudioEntry> aliasResult = searchDictAliases.Where(a => a.Key.Contains(searchString)).Select(a => a.Value).ToList();
            foreach (AudioEntry audio in aliasResult)
            {
                if (!result.Contains(audio))
                {
                    result.Add(audio);
                }
            }
            if (result.Count == 0)
            {
                throw new NoResultsFoundException();
            }
            result.Sort();
            return result;
        }

    }
}
