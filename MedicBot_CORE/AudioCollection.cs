using System;
using System.Collections.Generic;

namespace MedicBot
{
    [Serializable]
    public class AudioCollection
    {
        public string Name { get; set; }
        public Dictionary<string, AudioEntry> AudioEntries = new Dictionary<string, AudioEntry>();
        public Dictionary<string, AudioEntry> Aliases = new Dictionary<string, AudioEntry>();
    }
}