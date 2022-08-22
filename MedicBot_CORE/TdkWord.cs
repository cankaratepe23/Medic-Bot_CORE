using DSharpPlus.Entities;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Text;

namespace MedicBot
{
    class TdkWord
    {
        public List<DefinitionGroup> DefinitionGroups { get; set; }
        public Spelling SpellingDetails { get; set; }

        public override string ToString()
        {
            return this.DefinitionGroups.FirstOrDefault().Word;
        }

        public string GetSuffix()
        {
            return SpellingDetails.Suffix;
        }

        public Uri GetAudioUrl()
        {
            return new Uri($"https://sozluk.gov.tr/ses/{SpellingDetails.AudioCode}.wav");
        }

        public string GetTtsString()
        {
            StringBuilder builder = new StringBuilder();
            var defGrp = this.DefinitionGroups.FirstOrDefault();
            var def = defGrp.Definitions.FirstOrDefault();
            if (!string.IsNullOrWhiteSpace(defGrp.Etymology))
            {
                builder.AppendFormat("{0}. ", defGrp.Etymology);
            }

            if (def.DefinitionProperties != null && def.DefinitionProperties.Count != 0)
            {
                builder.AppendJoin(", ", def.DefinitionProperties.Select(p => p.Name).ToList());
                builder.Append(". ");
            }

            builder.AppendFormat("{0}. ", def.Content);

            if (def.DefinitionExamples != null && def.DefinitionExamples.Count != 0)
            {
                DefinitionExample example = def.DefinitionExamples.FirstOrDefault();
                builder.Append(example.Content);
                if (example.Author != null && example.Author.Count != 0)
                {
                    builder.AppendFormat(" {0}", example.Author.FirstOrDefault().FullName);
                }
            }
            return builder.ToString();
        }

        public static TdkWord GetWordOfTheDay()
        {
            using WebClient client = new WebClient();
            JObject wordOfTheDay = (JObject)JsonConvert.DeserializeObject(client.DownloadString("https://sozluk.gov.tr/icerik"));
            string word = (string)((JValue)wordOfTheDay.SelectToken("kelime[0].madde")).Value;
            return GetWord(word);
        }

        public static TdkWord GetWord(string word)
        {
            using WebClient client = new WebClient();
            JArray wordDetails = (JArray)JsonConvert.DeserializeObject(client.DownloadString($"https://sozluk.gov.tr/gts?ara={word}"));
            JArray wordSpellAndPronounce = (JArray)JsonConvert.DeserializeObject(client.DownloadString($"https://sozluk.gov.tr/yazim?ara={word}"));
            var wotd = new TdkWord()
            {
                DefinitionGroups = wordDetails.ToObject<List<DefinitionGroup>>(),
                SpellingDetails = wordSpellAndPronounce[0].ToObject<Spelling>()
            };
            return wotd;
        }

        public DiscordEmbed GetEmbed()
        {
            DiscordEmbedBuilder builder = new DiscordEmbedBuilder();
            string title = this.ToString();
            if (!string.IsNullOrWhiteSpace(GetSuffix()))
            {
                title += GetSuffix();
            }
            builder.WithTitle(title);
            builder.WithUrl("https://sozluk.gov.tr/?kelime=" + this.ToString());
            builder.WithColor(new DiscordColor("ea1c23"));
            int groupOrder = 0;
            int maxGroup = 3;
            int maxDef = 5;
            foreach (DefinitionGroup group in this.DefinitionGroups)
            {
                string groupName = $"({++groupOrder})";
                string groupValue = "--------------------------------------------------------------------------";
                if (!string.IsNullOrWhiteSpace(group.Etymology))
                {
                    groupValue = "-----   " + group.Etymology + "   ";
                    for (int i = 0; i < 70 - (groupValue.Length); i++)
                    {
                        groupValue += "-";
                    }
                }
                builder.AddField($"**{groupName}**", groupValue);
                int definitionOrder = 0;
                foreach (Definition definition in group.Definitions)
                {
                    string defName = (++definitionOrder).ToString() + ". ";
                    if (definition.DefinitionProperties != null && definition.DefinitionProperties.Count != 0)
                    {
                        defName += "_";
                        foreach (DefinitionProperty property in definition.DefinitionProperties)
                        {
                            defName += property.Name + ", ";
                        }
                        defName = defName.Remove(defName.Length - 2, 2);
                        defName += "_ ";
                    }
                    if (definition.DefinitionExamples != null && definition.DefinitionExamples.Count != 0)
                    {
                        DefinitionExample example = definition.DefinitionExamples.FirstOrDefault();
                        defName += "" + example.Content; // TODO
                        if (example.Author != null && example.Author.Count != 0)
                        {
                            defName += " - " + example.Author.FirstOrDefault().FullName;
                        }
                    }

                    string defValue = definition.Content;
                    builder.AddField(defName, defValue);
                    if (definitionOrder == maxDef)
                    {
                        break;
                    }
                }
                if (groupOrder == maxGroup)
                {
                    break;
                }
            }

            return builder.Build();
        }
    }

    public class Spelling
    {
        [JsonProperty("ekler")]
        public string Suffix { get; set; }

        [JsonProperty("seskod")]
        public string AudioCode { get; set; }
    }

    public class DefinitionGroup
    {
        [JsonProperty("madde")]
        public string Word { get; set; }

        [JsonProperty("anlam_say")]
        public int DefinitionCount { get; set; }

        [JsonProperty("taki")]
        public string Suffix { get; set; }

        [JsonProperty("cogul_mu")]
        [JsonConverter(typeof(CustomBooleanConverter))]
        public bool IsPlural { get; set; }

        [JsonProperty("ozel_mi")]
        [JsonConverter(typeof(CustomBooleanConverter))]
        public bool IsSpecial { get; set; }

        [JsonProperty("lisan")]
        public string Etymology { get; set; }

        [JsonProperty("anlamlarListe")]
        public List<Definition> Definitions { get; set; }
    }

    public class Definition
    {
        [JsonProperty("fiil")]
        [JsonConverter(typeof(CustomBooleanConverter))]
        public bool IsVerb { get; set; }

        [JsonProperty("anlam")]
        public string Content { get; set; }

        [JsonProperty("orneklerListe")]
        public List<DefinitionExample> DefinitionExamples { get; set; }

        [JsonProperty("ozelliklerListe")]
        public List<DefinitionProperty> DefinitionProperties { get; set; }
    }

    public class DefinitionProperty
    {
        [JsonProperty("tam_adi")]
        public string Name { get; set; }
    }

    public class DefinitionExample
    {
        [JsonProperty("ornek")]
        public string Content { get; set; }

        [JsonProperty("yazar")]
        public List<ExampleAuthor> Author { get; set; }
    }

    public class ExampleAuthor
    {
        [JsonProperty("tam_adi")]
        public string FullName { get; set; }
    }
}